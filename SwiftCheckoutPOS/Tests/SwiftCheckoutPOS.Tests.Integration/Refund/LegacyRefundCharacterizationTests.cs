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
        new RefundService(new SalesRepository(context), new AtomicUnit(context)).ExecuteRefund("BP10-REFUND");
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
            new RefundService(new SalesRepository(context), new AtomicUnit(context)).ExecuteRefund("BP10-MISSING"));
        Assert.Empty(context.Sales);
    }

    [Theory]
    [Trait("Fixes", "DR-14")]
    [InlineData(6, true)]
    [InlineData(8, false)]
    public void Approved_RefundOnlyWithinSevenDays_PRQ27(int ageDays, bool allowed)
    {
        Seed(ageDays: ageDays);
        if (allowed) Refund();
        else Assert.Contains("7-day", Assert.Throws<InvalidOperationException>(() => Refund()).Message);
        using var verify = fixture.Context();
        Assert.Equal(allowed ? "Refunded" : "Completed", verify.Sales.Single().OrderStatus);
    }

    [Fact]
    [Trait("Fixes", "DR-15")]
    [Trait("Fixes", "DR-34")]
    public void Approved_RefundIsADatedEventWithCogsReversalAndPayout_PRQ31_PRQ65_PRQ66()
    {
        Seed();
        DateTime originalDate;
        using (var before = fixture.Context()) originalDate = before.Sales.Single().SaleDate;
        Refund();
        using var verify = fixture.Context();
        var sale = verify.Sales.Single();
        Assert.Equal(originalDate, sale.SaleDate);          // original sale unchanged
        Assert.Equal(20m, sale.RefundedAmount);
        var refund = verify.Refunds.Single();
        Assert.Equal(20m, refund.Amount);
        Assert.True(refund.RefundedAt >= originalDate);
        Assert.Equal(20m, verify.RefundPayouts.Single().Amount); // legacy cash sale: cash back
        Assert.Equal(12, verify.Products.Single().StockQuantity);
        Assert.Equal(10m, verify.LedgerEntries.Single(e => e.Category == "COGS" && e.TransactionType == "Inflow").Amount);
        Assert.Equal(20m, verify.LedgerEntries.Single(e => e.Category == "Refund").Amount);
        Assert.Equal("Refund", verify.AuditEvents.Single().Action);
    }

    [Fact]
    [Trait("Fixes", "DR-15")]
    public void Approved_PartialRefundByQuantityWithDamagedGoods_PRQ26_PRQ29_CQ06()
    {
        Seed(); // 2 units sold at 10, cost 5
        int itemId;
        using (var c = fixture.Context()) itemId = c.SaleItems.Single().SaleItemId;
        using (var context = fixture.Context())
            new RefundService(new SalesRepository(context), new AtomicUnit(context)).ExecuteRefund("BP10-REFUND",
                [new() { SaleItemId = itemId, Quantity = 1, Disposition = "Damaged" }], FixtureData.Actor);
        using (var verify = fixture.Context())
        {
            Assert.Equal("PartiallyRefunded", verify.Sales.Single().OrderStatus);
            Assert.Equal(10, verify.Products.Single().StockQuantity); // damaged: not back on the shelf
            Assert.Equal(5m, verify.LedgerEntries.Single(e => e.Category == "Damaged").Amount);
            Assert.Equal(5m, verify.LedgerEntries.Single(e => e.Category == "COGS" && e.TransactionType == "Inflow").Amount);
        }
        // Cannot refund more than was sold: 1 left.
        using (var context = fixture.Context())
            Assert.Throws<InvalidOperationException>(() => new RefundService(new SalesRepository(context), new AtomicUnit(context))
                .ExecuteRefund("BP10-REFUND", [new() { SaleItemId = itemId, Quantity = 2 }], FixtureData.Actor));
        Refund(); // the remaining unit back to stock
        using var final = fixture.Context();
        Assert.Equal("Refunded", final.Sales.Single().OrderStatus);
        Assert.Equal(20m, final.Refunds.Sum(r => r.Amount));
        Assert.Equal(11, final.Products.Single().StockQuantity);
    }

    [Fact]
    [Trait("Fixes", "DR-34")]
    public void Approved_RefundCancelsUnpaidDueFirstThenPaysBackReceivedMoney_CQ05_CQ18()
    {
        // Sale 100: Cash 20 + Due 80; customer later paid 40 of the due (owes 40).
        var p = SeedSplitDueSale();
        Refund();
        using var verify = fixture.Context();
        var refund = verify.Refunds.Single();
        Assert.Equal(100m, refund.Amount);
        Assert.Equal(40m, refund.DueCancelled);                    // unpaid due removed
        Assert.Equal(0m, verify.Customers.Single().DueBalance);
        Assert.Equal(60m, verify.RefundPayouts.Sum(x => x.Amount)); // 20 cash + 40 collected, never more than received
    }

    private int SeedSplitDueSale()
    {
        using var context = fixture.Context();
        var product = FixtureData.Product(context, price: 100m, cost: 50m);
        var customer = new SwiftCheckoutPOS.DAL.Entities.Customer { FullName = "Due C", PhoneNumber = "BP10-DUE", DueBalance = 40m };
        context.Customers.Add(customer); context.SaveChanges();
        var sale = new SwiftCheckoutPOS.DAL.Entities.Sale
        {
            ReceiptNumber = "BP10-REFUND", SaleDate = DateTime.Now, SubTotal = 100m, TotalAmount = 100m, PaymentMethod = "Cash+Due",
            CashierName = FixtureData.Actor, OrderStatus = "Completed", CustomerId = customer.CustomerId,
            SaleItems = [new() { ProductId = product.ProductId, Quantity = 1, UnitPrice = 100m, LineTotal = 100m, CostAtSale = 50m }],
            Payments = [new() { Tender = "Cash", Amount = 20m }, new() { Tender = "Due", Amount = 80m }]
        };
        context.Sales.Add(sale); context.SaveChanges();
        return product.ProductId;
    }

    [Theory]
    [Trait("Fixes", "DR-29")]
    [InlineData(4, true)]
    [InlineData(6, false)]
    public void Approved_VoidWithinFiveMinutesReversesEverything_PRQ87_CQ11(int minutesAgo, bool allowed)
    {
        Seed();
        using (var c = fixture.Context())
        {
            c.Sales.Single().SaleDate = DateTime.Now.AddMinutes(-minutesAgo);
            c.SaveChanges();
        }
        using var context = fixture.Context();
        var service = new RefundService(new SalesRepository(context), new AtomicUnit(context));
        if (!allowed)
        {
            Assert.Throws<InvalidOperationException>(() => service.VoidSale("BP10-REFUND", "Wrong item", FixtureData.Actor));
            return;
        }
        Assert.Throws<ArgumentException>(() => service.VoidSale("BP10-REFUND", "", FixtureData.Actor)); // reason required
        service.VoidSale("BP10-REFUND", "Wrong item", FixtureData.Actor);
        using var verify = fixture.Context();
        Assert.Equal("Voided", verify.Sales.Single().OrderStatus);
        Assert.Equal(12, verify.Products.Single().StockQuantity);
        Assert.Equal("Void", verify.Refunds.Single().Kind);
        Assert.Equal("Wrong item", verify.AuditEvents.Single().Reason);
    }
}
