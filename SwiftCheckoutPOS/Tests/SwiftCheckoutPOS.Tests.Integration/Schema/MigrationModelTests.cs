using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Entities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Schema;

public sealed class MigrationModelTests
{
    [Fact]
    [Trait("Fixes", "AUD-03")]
    public void Migrations_MatchCurrentModel()
    {
        using var context = new ApplicationDbContext(SchemaScript.Options(TestConnectionGuard.FromEnvironment()));
        Assert.False(context.Database.HasPendingModelChanges(),
            "The EF model has changes without a migration. Add a migration (see TESTING.md).");
        Assert.NotEmpty(context.Database.GetMigrations());
    }
}

[Collection("SqlServer")]
public sealed class SchemaDatabaseTests(SqlServerFixture fixture)
{
    [Fact]
    [Trait("Category", "DatabaseSafety")]
    public void WrongMarkerStopsResetWithoutDeletingSyntheticRows()
    {
        fixture.Reset();
        using var context = fixture.Context();
        FixtureData.Product(context);
        context.Database.ExecuteSqlRaw("UPDATE dbo.BP10_TestFixture SET Owner=N'wrong-owner' WHERE Id=1;");
        try
        {
            Assert.Throws<InvalidOperationException>(() => fixture.Reset());
            using var verify = fixture.Context();
            Assert.Single(verify.Products);
        }
        finally
        {
            context.Database.ExecuteSqlRaw("UPDATE dbo.BP10_TestFixture SET Owner=@owner WHERE Id=1;",
                new Microsoft.Data.SqlClient.SqlParameter("@owner", SqlServerFixture.Owner));
        }
    }

    [Fact]
    [Trait("Category", "DatabaseSafety")]
    public void TestDatabaseHasAllMigrationsApplied()
    {
        using var context = fixture.Context();
        Assert.Empty(context.Database.GetPendingMigrations());
    }

    [Fact]
    [Trait("Category", "DatabaseSafety")]
    public void MarkedTestDatabaseHasRealPrecisionRowversionsAndUniqueReceipts()
    {
        fixture.Reset();
        using var context = fixture.Context();
        var p = FixtureData.Product(context, price: 11.25m, cost: 7.25m, tax: 5.50m);
        Assert.Equal(8, p.RowVersion.Length);
        Assert.Equal(11.25m, context.Products.AsNoTracking().Single().SellingPrice);
        var sale = FixtureData.CompletedSale(context, p.ProductId);
        Assert.Equal(8, sale.RowVersion.Length);
        context.Sales.Add(new Sale { ReceiptNumber = sale.ReceiptNumber, PaymentMethod = "Cash" });
        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
    }
}
