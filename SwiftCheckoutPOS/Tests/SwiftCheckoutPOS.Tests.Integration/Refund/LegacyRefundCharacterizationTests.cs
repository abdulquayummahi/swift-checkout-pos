using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Repositories;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Refund;

[Collection("SqlServer")]
public sealed class LegacyRefundCharacterizationTests
{
    private readonly SqlServerFixture fixture;
    public LegacyRefundCharacterizationTests(SqlServerFixture fixture)
    {
        this.fixture = fixture;
        fixture.Reset();
    }

    private int Seed(string status = "Completed", int ageDays = 0)
    {
        using var context = fixture.Context();
        var product = FixtureData.Product(context);
        FixtureData.CompletedSale(context, product.ProductId, status, ageDays: ageDays);
        return product.ProductId;
    }

    // Diagnostic caller only: production UI does NOT supply this outer strategy.
    // Retain EnableRetryOnFailure in every context, including this diagnostic profile.
    private void DiagnosticCaller(Action<ApplicationDbContext> action,
        params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors)
    {
        using var strategyContext = fixture.Context();
        strategyContext.Database.CreateExecutionStrategy().Execute(() =>
        {
            using var context = fixture.Context(interceptors);
            action(context);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Difference", "DR-15")]
    [Trait("Difference", "DR-34")]
    [Trait("Audit", "PB-02")]
    public void Current_UnwrappedRefundFailsWithRetries_DiffersFromAtomicRefund(bool directRepository)
    {
        Seed();
        using (var context = fixture.Context())
        {
            var repository = new SalesRepository(context);
            var error = Assert.Throws<InvalidOperationException>(() =>
            {
                if (directRepository) repository.ProcessRefundTransaction(repository.GetSaleByReceiptNumber("BP10-REFUND"));
                else new RefundService(repository).ExecuteRefund("BP10-REFUND");
            });
            Assert.Contains("SqlServerRetryingExecutionStrategy", error.Message);
        }
        using var verify = fixture.Context();
        Assert.Equal("Completed", verify.Sales.Single().OrderStatus);
        Assert.Equal(10, verify.Products.Single().StockQuantity);
        Assert.Single(verify.LedgerEntries);
    }

    [Theory]
    [InlineData("Refunded")]
    [InlineData("Held")]
    [InlineData("Resumed")]
    [InlineData("Cancelled")]
    public void Current_ServiceRejectsInvalidRefundStates(string status)
    {
        Seed(status);
        using var context = fixture.Context();
        Assert.Throws<InvalidOperationException>(() =>
            new RefundService(new SalesRepository(context)).ExecuteRefund("BP10-REFUND"));
        using var verify = fixture.Context();
        Assert.Equal(status, verify.Sales.Single().OrderStatus);
        Assert.Equal(10, verify.Products.Single().StockQuantity);
    }

    [Fact]
    public void Current_ServiceRejectsMissingReceipt()
    {
        using var context = fixture.Context();
        Assert.Throws<InvalidOperationException>(() =>
            new RefundService(new SalesRepository(context)).ExecuteRefund("BP10-MISSING"));
        Assert.Empty(context.Sales);
    }

    [Fact]
    [Trait("Profile", "DiagnosticCallerStrategy_NotCurrentUI")]
    [Trait("Difference", "DR-14")]
    public void Current_RefundBodyAcceptsThirtyDayOldSale_DiffersFromPRQ27()
    {
        Seed(ageDays: 30);
        DiagnosticCaller(context => new RefundService(new SalesRepository(context)).ExecuteRefund("BP10-REFUND"));
        using var verify = fixture.Context();
        Assert.Equal("Refunded", verify.Sales.Single().OrderStatus);
        Assert.Equal(12, verify.Products.Single().StockQuantity);
    }

    [Fact]
    [Trait("Profile", "DiagnosticCallerStrategy_NotCurrentUI")]
    [Trait("Difference", "DR-15")]
    [Trait("Difference", "DR-34")]
    public void Current_RefundBodyRestocksEverythingWithoutCostOrPayoutEvent_DiffersFromPRQ65_PRQ66()
    {
        Seed();
        DateTime originalDate;
        using (var before = fixture.Context()) originalDate = before.Sales.Single().SaleDate;
        DiagnosticCaller(context => new RefundService(new SalesRepository(context)).ExecuteRefund("BP10-REFUND"));
        using var verify = fixture.Context();
        Assert.Equal("Refunded", verify.Sales.Single().OrderStatus);
        Assert.Equal(originalDate, verify.Sales.Single().SaleDate);
        Assert.Equal(12, verify.Products.Single().StockQuantity);
        Assert.Equal(10m, verify.LedgerEntries.Single().Amount); // original COGS remains, no reversal
        Assert.Single(verify.SaleItems);
        Assert.DoesNotContain(verify.Model.GetEntityTypes(),
            entity => (entity.GetTableName() ?? "").Contains("Refund", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Profile", "DiagnosticCallerStrategy_NotCurrentUI")]
    public void Current_ServiceRejectsRepeatedRefundAfterFirstBodyCommit()
    {
        Seed();
        DiagnosticCaller(context => new RefundService(new SalesRepository(context)).ExecuteRefund("BP10-REFUND"));
        Assert.Throws<InvalidOperationException>(() => DiagnosticCaller(context =>
            new RefundService(new SalesRepository(context)).ExecuteRefund("BP10-REFUND")));
        using var verify = fixture.Context();
        Assert.Equal(12, verify.Products.Single().StockQuantity);
        Assert.Single(verify.LedgerEntries);
    }

    [Fact]
    [Trait("Profile", "DiagnosticCallerStrategy_NotCurrentUI")]
    public void Current_RefundBodySaveFailureRollsBackRealSqlEffects()
    {
        Seed();
        var failure = new FailAfterSave();
        Assert.Throws<InvalidOperationException>(() => DiagnosticCaller(context =>
            new RefundService(new SalesRepository(context)).ExecuteRefund("BP10-REFUND"), failure));
        Assert.Equal(1, failure.Calls);
        using var verify = fixture.Context();
        Assert.Equal("Completed", verify.Sales.Single().OrderStatus);
        Assert.Equal(10, verify.Products.Single().StockQuantity);
        Assert.Single(verify.LedgerEntries);
    }
}
