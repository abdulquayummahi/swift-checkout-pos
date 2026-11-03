using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Repositories;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Schema;

/// <summary>deploy/database/schema-idempotent.sql builds the full current schema on an empty database and can run twice.</summary>
[Collection("SqlServer")]
public sealed class SchemaScriptFileTests(SqlServerFixture fixture)
{
    [Fact]
    [Trait("Fixes", "IMM-03")]
    public void Approved_ShippedSchemaScriptCreatesCurrentSchemaAndIsRepeatable()
    {
        const string name = "SwiftCheckoutPOS_Test_ScriptCheck";
        var cs = new SqlConnectionStringBuilder(fixture.Guard.ForTestDatabase()) { InitialCatalog = name, Pooling = false }.ConnectionString;
        var repo = new BackupRepository(cs);
        repo.DropThisDatabase();
        var script = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Schema", "schema-idempotent.sql"));
        var batches = System.Text.RegularExpressions.Regex.Split(script, @"^\s*GO\s*$", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Where(b => !string.IsNullOrWhiteSpace(b)).ToList();
        try
        {
            using (var master = new SqlConnection(new SqlConnectionStringBuilder(cs) { InitialCatalog = "master" }.ConnectionString))
            {
                master.Open();
                new SqlCommand($"CREATE DATABASE [{name}];", master).ExecuteNonQuery();
            }
            for (var run = 0; run < 2; run++)
            {
                using var db = new SqlConnection(cs);
                db.Open();
                foreach (var batch in batches) new SqlCommand(batch, db) { CommandTimeout = 120 }.ExecuteNonQuery();
            }
            using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(cs).Options);
            Assert.Empty(context.Database.GetPendingMigrations());
            Assert.Equal(0, context.Sales.Count());
            Assert.Equal(0, context.SalePayments.Count());
        }
        finally
        {
            repo.DropThisDatabase();
        }
    }
}
