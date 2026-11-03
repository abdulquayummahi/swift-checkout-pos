using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SwiftCheckoutPOS.DAL.Context;

namespace SwiftCheckoutPOS.Tests.Integration.Infrastructure;

/// <summary>
/// Guarded SwiftCheckoutPOS_Test fixture. The connection is validated before any SQL. A catalog is only
/// ever created, migrated, reset or rebuilt when it carries this fixture's marker; an unmarked database stops.
/// When the migration set changes, the marked test catalog is dropped and rebuilt (owner-approved, test DB only).
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    public const string Owner = "SwiftCheckoutPOS.Tests.Integration.BP10.v1";
    private SqlConnection? lease;
    public TestConnectionGuard Guard { get; private set; } = null!;
    public DbContextOptions<ApplicationDbContext> Options(params IInterceptor[] interceptors) =>
        SchemaScript.Options(Guard, interceptors);
    public ApplicationDbContext Context(params IInterceptor[] interceptors) => new(Options(interceptors));

    public async Task InitializeAsync()
    {
        Guard = TestConnectionGuard.FromEnvironment(); // before any connection
        var hash = SchemaScript.MigrationsHash(Guard);
        try
        {
            var state = await InspectAsync(hash);
            if (state == CatalogState.MarkedOutdated) await DropMarkedCatalogAsync();
            if (state is CatalogState.Absent or CatalogState.MarkedOutdated) await CreateAndMigrateAsync(hash);

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
                    throw new InvalidOperationException("Another test run owns the fixture. Stop; do not reset concurrently.");
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

    private enum CatalogState { Absent, MarkedCurrent, MarkedOutdated }

    private async Task<CatalogState> InspectAsync(string hash)
    {
        await using (var master = new SqlConnection(Guard.ForProvisioning()))
        {
            await master.OpenAsync();
            await using var exists = master.CreateCommand();
            exists.CommandText = "SELECT DB_ID(@catalog);";
            exists.Parameters.Add("@catalog", SqlDbType.NVarChar, 128).Value = TestConnectionGuard.Catalog;
            if (await exists.ExecuteScalarAsync() is DBNull) return CatalogState.Absent;
        }
        await using var db = new SqlConnection(Guard.ForTestDatabase());
        await db.OpenAsync();
        await VerifyCatalogAsync(db);
        var (tableExists, owner, storedHash, ready) = await ReadMarkerAsync(db);
        // Never adopt or drop a database this fixture did not create.
        if (!tableExists || owner != Owner)
            RequireOwnership(false, null, null, false, hash);
        return storedHash == hash && ready ? CatalogState.MarkedCurrent : CatalogState.MarkedOutdated;
    }

    private async Task DropMarkedCatalogAsync()
    {
        SqlConnection.ClearAllPools();
        await using var master = new SqlConnection(Guard.ForProvisioning());
        await master.OpenAsync();
        await using var drop = master.CreateCommand();
        // Fixed catalog name only; reached only after the fixture marker was verified above.
        drop.CommandText = """
            ALTER DATABASE [SwiftCheckoutPOS_Test] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE [SwiftCheckoutPOS_Test];
            """;
        await drop.ExecuteNonQueryAsync();
    }

    private async Task CreateAndMigrateAsync(string hash)
    {
        await using (var master = new SqlConnection(Guard.ForProvisioning()))
        {
            await master.OpenAsync();
            await using var create = master.CreateCommand();
            create.CommandText = "CREATE DATABASE [SwiftCheckoutPOS_Test];";
            await create.ExecuteNonQueryAsync();
        }
        await using (var db = new SqlConnection(Guard.ForTestDatabase()))
        {
            await db.OpenAsync();
            await VerifyCatalogAsync(db);
            await using var marker = db.CreateCommand();
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
        }
        await using (var context = Context()) await context.Database.MigrateAsync();
        await using (var db = new SqlConnection(Guard.ForTestDatabase()))
        {
            await db.OpenAsync();
            await using var ready = db.CreateCommand();
            ready.CommandText = "UPDATE dbo.BP10_TestFixture SET Ready=1 WHERE Id=1;";
            await ready.ExecuteNonQueryAsync();
        }
    }

    /// <summary>Deletes all synthetic rows from every model table (future tables included) in one transaction.</summary>
    public void Reset()
    {
        if (lease is null) throw new InvalidOperationException("Fixture is not initialized.");
        VerifyCatalogAsync(lease).GetAwaiter().GetResult();
        RequireMarkerAsync(lease, SchemaScript.MigrationsHash(Guard)).GetAwaiter().GetResult();
        using var transaction = lease.BeginTransaction();
        using var command = lease.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DECLARE @sql nvarchar(max) = N'';
            SELECT @sql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' NOCHECK CONSTRAINT ALL;'
              FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
              WHERE t.name NOT IN (N'BP10_TestFixture', N'__EFMigrationsHistory');
            EXEC (@sql); SET @sql = N'';
            SELECT @sql += N'DELETE FROM ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N';'
              FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
              WHERE t.name NOT IN (N'BP10_TestFixture', N'__EFMigrationsHistory');
            EXEC (@sql); SET @sql = N'';
            SELECT @sql += N'ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N' WITH CHECK CHECK CONSTRAINT ALL;'
              FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
              WHERE t.name NOT IN (N'BP10_TestFixture', N'__EFMigrationsHistory');
            EXEC (@sql);
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

    private static async Task<(bool, string?, string?, bool)> ReadMarkerAsync(SqlConnection connection)
    {
        await using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT OBJECT_ID(N'dbo.BP10_TestFixture', N'U');";
        if (await exists.ExecuteScalarAsync() is DBNull) return (false, null, null, false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Owner,SchemaHash,Ready FROM dbo.BP10_TestFixture WHERE Id=1;";
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return (true, null, null, false);
        return (true, reader.GetString(0), reader.GetString(1), reader.GetBoolean(2));
    }

    private static async Task RequireMarkerAsync(SqlConnection connection, string hash)
    {
        await VerifyCatalogAsync(connection);
        var (tableExists, owner, storedHash, ready) = await ReadMarkerAsync(connection);
        RequireOwnership(tableExists, owner, storedHash, ready, hash);
    }

    public async Task DisposeAsync()
    {
        if (lease is not null) await lease.DisposeAsync(); // session lock released, marked DB retained
    }
}

[CollectionDefinition("SqlServer", DisableParallelization = true)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture> { }
