using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Repositories;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Refund;

/// <summary>
/// Full-sale refund on real SQL with EnableRetryOnFailure (production setting), no outer helper strategy.
/// IMM-04 replaced Current_UnwrappedRefundFailsWithRetries_DiffersFromAtomicRefund with the
/// Refund_* tests below. Current_* tests still document refund BUSINESS rules not yet implemented (BP-04/IMM-07).
/// </summary>
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

    private void Refund(params IInterceptor[] interceptors)
    {
        using var context = fixture.Context(interceptors);
        new RefundService(new SalesRepository(context)).ExecuteRefund("BP10-REFUND");
    }

    private void AssertUnchanged()
    {
        using var verify = fixture.Context();
        Assert.Equal("Completed", verify.Sales.Single().OrderStatus);
        Assert.Equal(10, verify.Products.Single().StockQuantity);
        Assert.Single(verify.LedgerEntries);
    }

    [Fact]
    [Trait("Fixes", "DR-15")]
    [Trait("Audit", "PB-02")]
    public void Refund_SucceedsWithRetriesEnabled()
    {
        Seed();
        Refund();
        using var verify = fixture.Context();
        Assert.Equal("Refunded", verify.Sales.Single().OrderStatus);
        Assert.Equal(12, verify.Products.Single().StockQuantity);
    }

    [Fact]
    public void Refund_TransientFailureBeforeCommitIsRetriedWithSingleEffect()
    {
        Seed();
        var transient = new TransientOnceAfterSave();
        Refund(transient);
        Assert.Equal(2, transient.Calls); // first attempt rolled back, second committed
        using var verify = fixture.Context();
        Assert.Equal("Refunded", verify.Sales.Single().OrderStatus);
        Assert.Equal(12, verify.Products.Single().StockQuantity); // restocked once, not twice
    }

    [Fact]
    public void Refund_NonTransientFailureAfterSaveRollsBackEverything()
    {
        Seed();
        var failure = new FailAfterSave();
        Assert.ThrowsAny<Exception>(() => Refund(failure));
        Assert.Equal(1, failure.Calls);
        AssertUnchanged();
    }

    [Fact]
    public void Refund_RepeatIsRejectedWithoutSecondRestock()
    {
        Seed();
        Refund();
        var error = Assert.Throws<InvalidOperationException>(() => Refund());
        Assert.Contains("already been refunded", error.Message);
        using var verify = fixture.Context();
        Assert.Equal(12, verify.Products.Single().StockQuantity);
    }

    [Fact]
    public void Refund_ConcurrentSecondRefundLosesOnRowVersion()
    {
        Seed();
        // While request A is about to save, request B refunds the same sale and commits first.
        var competitor = new RunBeforeFirstSave(() => Refund());
        var error = Assert.Throws<InvalidOperationException>(() => Refund(competitor));
        Assert.Contains("already been refunded", error.Message);
        using var verify = fixture.Context();
        Assert.Equal("Refunded", verify.Sales.Single().OrderStatus);
        Assert.Equal(12, verify.Products.Single().StockQuantity); // B restocked; A did not add again
    }

    [Theory]
    [InlineData("Refunded")]
    [InlineData("Held")]
    [InlineData("Resumed")]
    [InlineData("Cancelled")]
    public void Refund_RejectsInvalidStates(string status)
    {
        Seed(status);
        Assert.Throws<InvalidOperationException>(() => Refund());
        using var verify = fixture.Context();
        Assert.Equal(status, verify.Sales.Single().OrderStatus);
        Assert.Equal(10, verify.Products.Single().StockQuantity);
    }

    [Fact]
    public void Refund_RejectsMissingReceipt()
    {
        using var context = fixture.Context();
        Assert.Throws<InvalidOperationException>(() =>
            new RefundService(new SalesRepository(context)).ExecuteRefund("BP10-MISSING"));
        Assert.Empty(context.Sales);
    }

    [Fact]
    [Trait("Difference", "DR-14")]
    public void Current_RefundAcceptsThirtyDayOldSale_DiffersFromPRQ27()
    {
        Seed(ageDays: 30);
        Refund();
        using var verify = fixture.Context();
        Assert.Equal("Refunded", verify.Sales.Single().OrderStatus);
        Assert.Equal(12, verify.Products.Single().StockQuantity);
    }

    [Fact]
    [Trait("Difference", "DR-15")]
    [Trait("Difference", "DR-34")]
    public void Current_RefundRestocksEverythingWithoutCostOrPayoutEvent_DiffersFromPRQ65_PRQ66()
    {
        Seed();
        DateTime originalDate;
        using (var before = fixture.Context()) originalDate = before.Sales.Single().SaleDate;
        Refund();
        using var verify = fixture.Context();
        Assert.Equal("Refunded", verify.Sales.Single().OrderStatus);
        Assert.Equal(originalDate, verify.Sales.Single().SaleDate);
        Assert.Equal(12, verify.Products.Single().StockQuantity);
        Assert.Equal(10m, verify.LedgerEntries.Single().Amount); // original COGS remains, no reversal
        Assert.Single(verify.SaleItems);
        Assert.DoesNotContain(verify.Model.GetEntityTypes(),
            entity => (entity.GetTableName() ?? "").Contains("Refund", StringComparison.OrdinalIgnoreCase));
    }
}
