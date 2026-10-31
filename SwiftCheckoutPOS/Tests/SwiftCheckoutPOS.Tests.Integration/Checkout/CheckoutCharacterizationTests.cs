using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Checkout;

/// <summary>Current_...DiffersFrom... tests intentionally record defects; they are not approved rules.</summary>
[Collection("SqlServer")]
public sealed class CheckoutCharacterizationTests
{
    private readonly SqlServerFixture fixture;
    public CheckoutCharacterizationTests(SqlServerFixture fixture)
    {
        this.fixture = fixture;
        fixture.Reset();
    }

    private Product Seed(decimal price = 10m, decimal cost = 5m, decimal tax = 0m, int stock = 10)
    {
        using var context = fixture.Context();
        return FixtureData.Product(context, price, cost, tax, stock);
    }

    [Fact]
    public void Current_CompleteSale_PersistsStockCostAndCogsAtomically()
    {
        var p = Seed(price: 11.25m, cost: 7.25m);
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId, 2);
        var result = service.CompleteSale(cart, "Cash", FixtureData.Actor);
        using var verify = fixture.Context();
        var sale = verify.Sales.Include(s => s.SaleItems).Single();
        Assert.Equal(result.SaleId, sale.SaleId);
        Assert.Equal("Completed", sale.OrderStatus);
        Assert.Equal(22.50m, sale.TotalAmount);
        Assert.Equal(7.25m, sale.SaleItems.Single().CostAtSale);
        Assert.Equal(14.50m, verify.LedgerEntries.Single().Amount);
        Assert.Equal(8, verify.Products.Single().StockQuantity);
    }

    [Fact]
    [Trait("Difference", "DR-04")]
    public void Current_AddsVatOnTop_DiffersFromPRQ08()
    {
        var p = Seed(price: 115m, tax: 15m);
        var service = FixtureData.Checkout(fixture);
        service.CompleteSale(FixtureData.Cart(service, p.ProductId), "Card", FixtureData.Actor);
        using var verify = fixture.Context();
        var sale = verify.Sales.Single();
        Assert.Equal(17.25m, sale.TaxAmount);
        Assert.Equal(132.25m, sale.TotalAmount);
    }

    [Fact]
    [Trait("Difference", "DR-03")]
    public void Current_TaxHalfPaisaUsesToEven_DiffersFromPRQ06()
    {
        var p = Seed(price: 1m, cost: 0m, tax: 0.50m);
        var service = FixtureData.Checkout(fixture);
        service.CompleteSale(FixtureData.Cart(service, p.ProductId), "Cash", FixtureData.Actor);
        using var verify = fixture.Context();
        Assert.Equal(0m, verify.Sales.Single().TaxAmount);
        Assert.Empty(verify.LedgerEntries); // current zero cost path
    }

    [Fact]
    [Trait("Difference", "DR-05")]
    public void Current_CashKeepsPaisa_DiffersFromPRQ07()
    {
        var p = Seed(price: 10.49m);
        var service = FixtureData.Checkout(fixture);
        service.CompleteSale(FixtureData.Cart(service, p.ProductId), "Cash", FixtureData.Actor);
        using var verify = fixture.Context();
        Assert.Equal(10.49m, verify.Sales.Single().TotalAmount);
        Assert.Null(verify.Model.FindEntityType(typeof(Sale))!.FindProperty("RoundingAdjustment"));
    }

    [Fact]
    [Trait("Difference", "DR-06")]
    public void Current_ServiceInputWithBothBillModesUsesFlat_DiffersFromPRQ12()
    {
        var p = Seed(price: 100m);
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId);
        cart.BillDiscountAmount = 5m;
        cart.BillDiscountPercentage = 10m;
        service.CompleteSale(cart, "Cash", FixtureData.Actor);
        using var verify = fixture.Context();
        Assert.Equal(95m, verify.Sales.Single().TotalAmount);
    }

    [Fact]
    [Trait("Difference", "DR-07")]
    [Trait("Difference", "DR-08")]
    public void Current_HundredPercentDiscountNeedsNoApproval_DiffersFromPRQ67_PRQ93()
    {
        var p = Seed();
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId);
        service.ApplyBillDiscount(cart, 100m, "Percent");
        service.CompleteSale(cart, "Cash", FixtureData.Actor);
        using var verify = fixture.Context();
        Assert.Equal(0m, verify.Sales.Single().TotalAmount);
        Assert.Equal(5m, verify.LedgerEntries.Single().Amount);
    }

    [Fact]
    [Trait("Difference", "DR-26")]
    public void Current_ReceiptExistsBeforeCompletionAndIsRetained_DiffersFromPRQ45()
    {
        var p = Seed();
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId);
        Assert.Matches(@"^INV-\d{14}-[A-F0-9]{6}$", cart.InvoiceNumber);
        var result = service.CompleteSale(cart, "Cash", FixtureData.Actor);
        Assert.Equal(cart.InvoiceNumber, result.ReceiptNumber);
    }

    [Theory]
    [InlineData("Cash")]
    [InlineData("Card")]
    [InlineData("Mobile Wallet")]
    public void Current_AcceptsExistingSingleTenderLabels(string tender)
    {
        var p = Seed();
        var service = FixtureData.Checkout(fixture);
        service.CompleteSale(FixtureData.Cart(service, p.ProductId), tender, FixtureData.Actor);
        using var verify = fixture.Context();
        Assert.Equal(tender, verify.Sales.Single().PaymentMethod);
    }

    [Theory]
    [InlineData("bKash")]
    [InlineData("Nagad")]
    [InlineData("Due")]
    [Trait("Difference", "DR-27")]
    [Trait("Difference", "DR-28")]
    public void Current_RejectsApprovedTenderVocabulary_DiffersFromPRQ85_PRQ86(string tender)
    {
        var p = Seed();
        var service = FixtureData.Checkout(fixture);
        Assert.Throws<CheckoutValidationException>(() =>
            service.CompleteSale(FixtureData.Cart(service, p.ProductId), tender, FixtureData.Actor));
        FixtureData.AssertEmptySaleEffects(fixture, p.ProductId, 10);
    }

    [Fact]
    public void Current_RejectsFabricatedCombinedTenderLabel()
    {
        var p = Seed();
        var service = FixtureData.Checkout(fixture);
        Assert.Throws<CheckoutValidationException>(() => service.CompleteSale(
            FixtureData.Cart(service, p.ProductId), "Cash+Card", FixtureData.Actor));
        FixtureData.AssertEmptySaleEffects(fixture, p.ProductId, 10);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("duplicate")]
    [InlineData("precision")]
    [InlineData("negative")]
    [InlineData("cashier")]
    public void Current_InvalidCommandHasNoSqlEffects(string mode)
    {
        var p = Seed();
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId);
        if (mode == "empty") cart.Items.Clear();
        if (mode == "duplicate") cart.Items.Add(cart.Items.Single());
        if (mode == "precision") cart.Items.Single().UnitPrice = 1.001m;
        if (mode == "negative") cart.Items.Single().Quantity = -1;
        Assert.Throws<CheckoutValidationException>(() =>
            service.CompleteSale(cart, "Cash", mode == "cashier" ? "" : FixtureData.Actor));
        FixtureData.AssertEmptySaleEffects(fixture, p.ProductId, 10);
    }

    [Theory]
    [InlineData("stock")]
    [InlineData("price")]
    [InlineData("tax")]
    [InlineData("inactive")]
    [InlineData("missing")]
    public void Current_RevalidatesFreshProductBeforeCompletion(string mode)
    {
        var p = Seed();
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId, 2);
        using (var change = fixture.Context())
        {
            var entity = change.Products.Single();
            if (mode == "stock") entity.StockQuantity = 1;
            if (mode == "price") entity.SellingPrice += 1m;
            if (mode == "tax") entity.TaxRate = 5m;
            if (mode == "inactive") entity.IsActive = false;
            if (mode == "missing") change.Products.Remove(entity);
            change.SaveChanges();
        }
        Assert.Throws<CheckoutValidationException>(() => service.CompleteSale(cart, "Cash", FixtureData.Actor));
        using var verify = fixture.Context();
        Assert.Empty(verify.Sales);
        Assert.Empty(verify.LedgerEntries);
        if (mode != "missing") Assert.Equal(mode == "stock" ? 1 : 10, verify.Products.Single().StockQuantity);
    }

    [Fact]
    public void Current_DuplicateCompletionHasOneStockAndLedgerEffect()
    {
        var p = Seed();
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId, 2);
        var first = service.CompleteSale(cart, "Cash", FixtureData.Actor);
        var replay = service.CompleteSale(cart, "Cash", FixtureData.Actor);
        Assert.Equal(first.SaleId, replay.SaleId);
        Assert.True(replay.AlreadyCompleted);
        using var verify = fixture.Context();
        Assert.Single(verify.Sales);
        Assert.Single(verify.SaleItems);
        Assert.Single(verify.LedgerEntries);
        Assert.Equal(8, verify.Products.Single().StockQuantity);
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("tender")]
    [InlineData("actor")]
    public void Current_ReceiptCannotBeReusedForDifferentPayload(string mode)
    {
        var p = Seed();
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId);
        service.CompleteSale(cart, "Cash", FixtureData.Actor);
        if (mode == "quantity") cart.Items.Single().Quantity = 2;
        Assert.Throws<CheckoutValidationException>(() => service.CompleteSale(cart,
            mode == "tender" ? "Card" : "Cash", mode == "actor" ? "Different fixture actor" : FixtureData.Actor));
        using var verify = fixture.Context();
        Assert.Single(verify.Sales);
        Assert.Single(verify.LedgerEntries);
        Assert.Equal(9, verify.Products.Single().StockQuantity);
    }

    [Fact]
    public void Current_SaveFailureRollsBackRealSqlWritesBeforeCommit()
    {
        var p = Seed();
        var failure = new FailAfterSave();
        var service = FixtureData.Checkout(fixture, failure);
        var cart = FixtureData.Cart(service, p.ProductId, 2);
        Assert.Throws<InjectedTestFailure>(() => service.CompleteSale(cart, "Cash", FixtureData.Actor));
        Assert.Equal(1, failure.Calls);
        FixtureData.AssertEmptySaleEffects(fixture, p.ProductId, 10);
    }

    [Fact]
    public void Current_ProductCostChangesDoNotRewriteCapturedCompletionCost()
    {
        var p = Seed(cost: 5.25m);
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId);
        service.CompleteSale(cart, "Cash", FixtureData.Actor);
        using (var change = fixture.Context())
        {
            change.Products.Single().CostPrice = 9m;
            change.SaveChanges();
        }
        using var verify = fixture.Context();
        Assert.Equal(5.25m, verify.SaleItems.Single().CostAtSale);
        Assert.Equal(5.25m, verify.LedgerEntries.Single().Amount);
    }

    [Fact]
    [Trait("Difference", "DR-33")]
    public void Current_ResumeReleasesStockThenCompletesOriginalSale_DiffersFromCQ04()
    {
        var p = Seed(stock: 20);
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId, 8);
        service.HoldOrder(cart, FixtureData.Actor);
        var resumed = service.ResumeOrder(cart.PersistedSaleId!.Value);
        using (var verify = fixture.Context()) Assert.Equal(20, verify.Products.Single().StockQuantity);
        var completed = service.CompleteSale(resumed, "Cash", FixtureData.Actor);
        Assert.Equal(cart.PersistedSaleId, completed.SaleId);
        using var final = fixture.Context();
        Assert.Single(final.Sales);
        Assert.Single(final.LedgerEntries);
        Assert.Equal(12, final.Products.Single().StockQuantity);
    }

    [Fact]
    public void Current_LoyaltyCompletionDoesNotInventPoints()
    {
        var p = Seed();
        int id;
        using (var seed = fixture.Context())
        {
            var customer = new Customer { FullName = "BP10 synthetic customer", PhoneNumber = "BP10-PHONE", LoyaltyPoints = 17 };
            seed.Customers.Add(customer);
            seed.SaveChanges();
            id = customer.CustomerId;
        }
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId);
        cart.CustomerType = "Loyalty";
        cart.CustomerId = id;
        service.CompleteSale(cart, "Cash", FixtureData.Actor);
        using var verify = fixture.Context();
        Assert.Equal(17, verify.Customers.Single().LoyaltyPoints);
        Assert.Equal(id, verify.Sales.Single().CustomerId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(999999)]
    public void Current_UnlinkedOrMissingCustomerCannotComplete(int? customerId)
    {
        var p = Seed();
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId);
        cart.CustomerType = "Loyalty";
        cart.CustomerId = customerId;
        Assert.Throws<CheckoutValidationException>(() => service.CompleteSale(cart, "Cash", FixtureData.Actor));
        FixtureData.AssertEmptySaleEffects(fixture, p.ProductId, 10);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Category", "ControlledConcurrency")]
    public async Task Current_ControlledConcurrentCompletionsHaveOneFinancialEffect(bool sameReceipt)
    {
        var p = Seed(stock: 1);
        using var gate = new DualSaveGate();
        var service = FixtureData.Checkout(fixture, gate);
        var first = FixtureData.Cart(service, p.ProductId);
        var second = FixtureData.Cart(service, p.ProductId);
        if (sameReceipt) second.InvoiceNumber = first.InvoiceNumber;
        var errors = await Task.WhenAll(
            Task.Run(() => Record.Exception(() => service.CompleteSale(first, "Cash", FixtureData.Actor))),
            Task.Run(() => Record.Exception(() => service.CompleteSale(second, "Cash", FixtureData.Actor))));
        Assert.Contains(errors, e => e is null);
        Assert.All(errors.Where(e => e is not null), e =>
            Assert.True(e is DbUpdateConcurrencyException || e is CheckoutValidationException,
                "Controlled race must return a known conflict, not a timeout/unhandled SQL failure."));
        using var verify = fixture.Context();
        Assert.Single(verify.Sales);
        Assert.Single(verify.SaleItems);
        Assert.Single(verify.LedgerEntries);
        Assert.Equal(0, verify.Products.Single().StockQuantity);
    }
}
