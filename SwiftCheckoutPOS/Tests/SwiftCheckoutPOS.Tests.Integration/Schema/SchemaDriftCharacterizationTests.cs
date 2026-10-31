using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Entities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Schema;

public sealed class SchemaDriftCharacterizationTests
{
    [Fact]
    [Trait("Audit", "AUD-03")]
    public void Current_MigrationsOmitRequiredModelFields_DiffersFromAUD03()
    {
        using var context = new ApplicationDbContext(SchemaScript.Options(TestConnectionGuard.FromEnvironment()));
        var snapshotType = typeof(ApplicationDbContext).Assembly.GetType("SwiftCheckoutPOS.DAL.Migrations.ApplicationDbContextModelSnapshot")!;
        var snapshot = ((ModelSnapshot)Activator.CreateInstance(snapshotType, nonPublic: true)!).Model;
        Assert.Null(snapshot.FindEntityType(typeof(LedgerEntry)));
        Assert.Null(snapshot.FindEntityType(typeof(SystemSetting)));
        Assert.Null(snapshot.FindEntityType(typeof(Product))!.FindProperty(nameof(Product.RowVersion)));
        Assert.Null(snapshot.FindEntityType(typeof(Sale))!.FindProperty(nameof(Sale.RowVersion)));
        Assert.Null(snapshot.FindEntityType(typeof(Sale))!.FindProperty(nameof(Sale.CashierName)));
        Assert.Null(snapshot.FindEntityType(typeof(SaleItem))!.FindProperty(nameof(SaleItem.CostAtSale)));
        Assert.Null(snapshot.FindEntityType(typeof(Customer))!.FindProperty(nameof(Customer.Address)));
        Assert.NotNull(context.Model.FindEntityType(typeof(LedgerEntry)));
        Assert.True(context.Model.FindEntityType(typeof(Product))!.FindProperty(nameof(Product.RowVersion))!.IsConcurrencyToken);
        Assert.NotNull(context.Model.FindEntityType(typeof(SaleItem))!.FindProperty(nameof(SaleItem.CostAtSale)));
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
