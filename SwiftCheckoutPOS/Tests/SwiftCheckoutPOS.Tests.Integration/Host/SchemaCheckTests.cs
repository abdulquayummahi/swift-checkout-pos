using Microsoft.Data.SqlClient;
using SwiftCheckoutPOS.DAL.Repositories;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;
using SwiftCheckoutPOS.UI.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Host;

[Collection("SqlServer")]
public sealed class SchemaCheckTests
{
    private readonly SqlServerFixture fixture;
    public SchemaCheckTests(SqlServerFixture fixture) { this.fixture = fixture; fixture.Reset(); }

    [Fact]
    [Trait("Fixes", "IMM-03")]
    public void Approved_StartupRefusesMissingOrOldDevelopmentDatabase()
    {
        var test = fixture.Guard.ForTestDatabase();
        Assert.Equal(MaintenanceCommands.SchemaState.Current, MaintenanceCommands.Inspect(test).State);
        Assert.True(MaintenanceCommands.SchemaReady(test));

        string Other(string name) => new SqlConnectionStringBuilder(test) { InitialCatalog = name }.ConnectionString;
        var missing = Other("SwiftCheckoutPOS_Test_DoesNotExist");
        Assert.Equal(MaintenanceCommands.SchemaState.Missing, MaintenanceCommands.Inspect(missing).State);
        Assert.False(MaintenanceCommands.SchemaReady(missing));

        const string legacyName = "SwiftCheckoutPOS_Test_LegacySchema";
        var legacy = Other(legacyName);
        var repo = new BackupRepository(legacy);
        repo.DropThisDatabase();
        try
        {
            using (var master = new SqlConnection(Other("master")))
            {
                master.Open();
                using var create = new SqlCommand($"CREATE DATABASE [{legacyName}];", master);
                create.ExecuteNonQuery();
            }
            using (var db = new SqlConnection(legacy))
            {
                db.Open();
                using var table = new SqlCommand("CREATE TABLE dbo.Sales (SaleId int NOT NULL PRIMARY KEY);", db); // old schema shape
                table.ExecuteNonQuery();
            }
            Assert.Equal(MaintenanceCommands.SchemaState.Legacy, MaintenanceCommands.Inspect(legacy).State);
            Assert.False(MaintenanceCommands.SchemaReady(legacy));
        }
        finally
        {
            repo.DropThisDatabase();
        }
        Environment.ExitCode = 0;
    }
}
