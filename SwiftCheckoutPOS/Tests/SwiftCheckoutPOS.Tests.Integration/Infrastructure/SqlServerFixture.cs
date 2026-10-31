using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SwiftCheckoutPOS.DAL.Context;

namespace SwiftCheckoutPOS.Tests.Integration.Infrastructure;

public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string Owner = "SwiftCheckoutPOS.Tests.Integration.BP10.v1";
    private SqlConnection? lease;
    public TestConnectionGuard Guard { get; private set; } = null!;
    public DbContextOptions<ApplicationDbContext> Options(params IInterceptor[] interceptors) =>
        SchemaScript.Options(Guard, interceptors);
    public ApplicationDbContext Context(params IInterceptor[] interceptors) => new(Options(interceptors));

    public async ValueTask InitializeAsync()
    {
        Guard = TestConnectionGuard.FromEnvironment(); // before any connection
        var script = SchemaScript.ReadReviewed(Guard); // before any SQL, compare reviewed/current model
        var hash = SchemaScript.Hash(script);
        try
        {
            var created = false;
            await using (var master = new SqlConnection(Guard.ForProvisioning()))
            {
                await master.OpenAsync();
                await using var exists = master.CreateCommand();
                exists.CommandText = "SELECT DB_ID(@catalog);";
                exists.Parameters.Add("@catalog", SqlDbType.NVarChar, 128).Value = TestConnectionGuard.Catalog;
                if (await exists.ExecuteScalarAsync() is DBNull)
                {
                    await using var create = master.CreateCommand();
                    create.CommandText = "CREATE DATABASE [SwiftCheckoutPOS_Test];";
                    await create.ExecuteNonQueryAsync();
                    created = true;
                }
            }

            lease = new SqlConnection(Guard.ForTestDatabase());
            await lease.OpenAsync();
            await VerifyCatalogAsync(lease);
            await using (var acquire = lease.CreateCommand())
            {
                acquire.CommandText = """
                    DECLARE @result int;
                    EXEC @result = sys.sp_getapplock @Resource=N'SwiftCheckoutPOS.BP10.Fixture',
                        @LockMode=N'Exclusive', @LockOwner=N'Session', @LockTimeout=0;
                    SELECT @result;
                    """;
                if (Convert.ToInt32(await acquire.ExecuteScalarAsync()) < 0)
                    throw new InvalidOperationException("Another BP-10 test run owns the fixture. Stop; do not reset concurrently.");
            }

            if (created)
            {
                // Never executed for an existing catalog, even if empty.
                await using var marker = lease.CreateCommand();
                marker.CommandText = """
                    CREATE TABLE dbo.BP10_TestFixture (
                        Id int NOT NULL CONSTRAINT PK_BP10_TestFixture PRIMARY KEY CHECK (Id=1),
                        Owner nvarchar(100) NOT NULL,
                        SchemaHash char(64) NOT NULL,
                        Ready bit NOT NULL
                    );
                    INSERT dbo.BP10_TestFixture (Id,Owner,SchemaHash,Ready) VALUES (1,@owner,@hash,0);
                    """;
                marker.Parameters.Add("@owner", SqlDbType.NVarChar, 100).Value = Owner;
                marker.Parameters.Add("@hash", SqlDbType.Char, 64).Value = hash;
                await marker.ExecuteNonQueryAsync();
                foreach (var batch in Regex.Split(script, @"^GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(batch)) continue;
                    await using var apply = lease.CreateCommand();
                    apply.CommandText = batch; // reviewed EF-generated DDL; no user interpolation
                    await apply.ExecuteNonQueryAsync();
                }
                await using var ready = lease.CreateCommand();
                ready.CommandText = "UPDATE dbo.BP10_TestFixture SET Ready=1 WHERE Id=1;";
                await ready.ExecuteNonQueryAsync();
            }
            await RequireMarkerAsync(lease, hash);
        }
        catch (SqlException error)
        {
            if (lease is not null) await lease.DisposeAsync();
            lease = null;
            throw new InvalidOperationException($"Guarded test database setup failed (SQL code {error.Number}); connection details suppressed.");
        }
        catch
        {
            if (lease is not null) await lease.DisposeAsync();
            lease = null;
            throw;
        }
    }

    public void Reset()
    {
        if (lease is null) throw new InvalidOperationException("Fixture is not initialized.");
        VerifyCatalogAsync(lease).GetAwaiter().GetResult();
        // Marker ownership and schema must match before every reset; no auto-adoption.
        RequireMarkerAsync(lease, SchemaScript.Hash(SchemaScript.ReadReviewed(Guard))).GetAwaiter().GetResult();
        using var transaction = lease.BeginTransaction();
        using var command = lease.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DELETE FROM dbo.SaleItems;
            DELETE FROM dbo.Sales;
            DELETE FROM dbo.LedgerEntries;
            DELETE FROM dbo.SystemSettings;
            DELETE FROM dbo.Products;
            DELETE FROM dbo.Customers;
            DELETE FROM dbo.TillReconciliations;
            DELETE FROM dbo.Users;
            DELETE FROM dbo.Vendors;
            DELETE FROM dbo.Categories;
            """;
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    internal static void RequireOwnership(bool tableExists, string? owner, string? hash, bool ready, string expectedHash)
    {
        if (!tableExists || owner != Owner || hash != expectedHash || !ready)
            throw new InvalidOperationException("SwiftCheckoutPOS_Test is unmarked or its fixture marker/schema differs. Stop; no reset/adoption is permitted.");
    }

    private static async Task VerifyCatalogAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT DB_NAME();";
        if ((string?)await command.ExecuteScalarAsync() != TestConnectionGuard.Catalog)
            throw new InvalidOperationException("Unexpected database identity; stopped before writes.");
    }

    private static async Task RequireMarkerAsync(SqlConnection connection, string hash)
    {
        await VerifyCatalogAsync(connection);
        await using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT OBJECT_ID(N'dbo.BP10_TestFixture', N'U');";
        if (await exists.ExecuteScalarAsync() is DBNull)
        {
            RequireOwnership(false, null, null, false, hash);
            return;
        }
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Owner,SchemaHash,Ready FROM dbo.BP10_TestFixture WHERE Id=1;";
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) RequireOwnership(true, null, null, false, hash);
        RequireOwnership(true, reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), hash);
    }

    public async ValueTask DisposeAsync()
    {
        if (lease is not null) await lease.DisposeAsync(); // session lock released, marked DB retained
    }
}

[CollectionDefinition("SqlServer", DisableParallelization = true)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture> { }
