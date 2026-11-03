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
    [Trait("Fixes", "DR-04")]
    public void Approved_PricesAreVatInclusive_PRQ08()
    {
        var p = Seed(price: 115m, tax: 15m);
        var service = FixtureData.Checkout(fixture);
        service.CompleteSale(FixtureData.Cart(service, p.ProductId), "Card", FixtureData.Actor);
        using var verify = fixture.Context();
        var sale = verify.Sales.Single();
        Assert.Equal(15.00m, sale.TaxAmount);    // 115 x 15/115 contained in the shelf price
        Assert.Equal(115.00m, sale.TotalAmount); // customer pays the shelf price
        Assert.Equal(0m, sale.RoundingAmount);   // card is exact (PR-Q91)
    }

    [Fact]
    [Trait("Fixes", "DR-03")]
    public void Approved_HalfPaisaRoundsAwayFromZero_PRQ06()
    {
        var p = Seed(price: 0.01m, cost: 0m, tax: 100m);  // VAT contained = 0.01 x 100/200 = 0.005 exactly
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId);
        Assert.Equal(0.01m, cart.TaxAmount);              // ToEven would give 0.00
        Assert.Equal(0.01m, SwiftCheckoutPOS.BLL.Pricing.Money.R2(0.005m));
        Assert.Equal(-0.01m, SwiftCheckoutPOS.BLL.Pricing.Money.R2(-0.005m));
        Assert.Equal(11m, SwiftCheckoutPOS.BLL.Pricing.Money.WholeTaka(10.50m));
    }

    [Theory]
    [Trait("Fixes", "DR-05")]
    [InlineData("Cash", "10.49", "-0.49")]
    [InlineData("Cash", "10.50", "0.50")]
    [InlineData("Card", "10.49", "0")]
    [InlineData("bKash", "10.50", "0")]
    public void Approved_CashOnlyWholeTakaRounding_PRQ07_PRQ91(string tender, string price, string rounding)
    {
        var p = Seed(price: decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture));
        var service = FixtureData.Checkout(fixture);
        service.CompleteSale(FixtureData.Cart(service, p.ProductId), tender, FixtureData.Actor);
        using var verify = fixture.Context();
        var sale = verify.Sales.Single();
        Assert.Equal(decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture), sale.TotalAmount);
        Assert.Equal(decimal.Parse(rounding, System.Globalization.CultureInfo.InvariantCulture), sale.RoundingAmount);
    }

    [Fact]
    [Trait("Fixes", "DR-06")]
    public void Approved_BothBillDiscountModesRejected_PRQ12()
    {
        var p = Seed(price: 100m);
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId);
        cart.BillDiscountAmount = 5m;
        cart.BillDiscountPercentage = 10m;
        Assert.Throws<CheckoutValidationException>(() => service.CompleteSale(cart, "Cash", FixtureData.Actor));
        FixtureData.AssertEmptySaleEffects(fixture, p.ProductId, 10);
    }

    [Theory]
    [Trait("Fixes", "DR-07")]
    [InlineData("Cashier", 25, false)]
    [InlineData("Cashier", 26, true)]
    [InlineData("Manager", 50, false)]
    [InlineData("Manager", 51, true)]
    [InlineData("Admin", 49, false)]
    public void Approved_DiscountAboveRoleLimitNeedsApproval_PRQ67(string role, int percent, bool needsApproval)
    {
        var p = Seed(price: 100m, cost: 10m);
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId);
        service.ApplyBillDiscount(cart, percent, "Percent");
        var plan = SwiftCheckoutPOS.BLL.DTOs.PaymentPlanDTO.Single("Card");
        if (needsApproval)
        {
            var error = Assert.Throws<CheckoutValidationException>(() => service.CompleteSale(cart, plan, FixtureData.Actor, role, null));
            Assert.StartsWith("APPROVAL REQUIRED", error.Message);
            FixtureData.AssertEmptySaleEffects(fixture, p.ProductId, 10);
        }
        else
        {
            service.CompleteSale(cart, plan, FixtureData.Actor, role, null);
            using var verify = fixture.Context();
            Assert.Equal(100m - percent, verify.Sales.Single().TotalAmount);
            Assert.Equal((decimal)percent, verify.SaleItems.Single().DiscountAmount); // DR-36 persisted
        }
    }

    [Theory]
    [Trait("Fixes", "DR-07")]
    [Trait("Fixes", "DR-08")]
    [InlineData("Manager", 40, true)]   // manager may approve 40%
    [InlineData("Manager", 60, false)]  // above 50% needs Admin
    [InlineData("Admin", 95, true)]     // below cost needs Admin while financial view is OFF (CQ-03)
    [InlineData("Manager", 95, false)]
    public void Approved_ApproverRoleDecides_PRQ67_PRQ93(string approverRole, int percent, bool succeeds)
    {
        var p = Seed(price: 100m, cost: 10m);
        var service = new CheckoutService(new SwiftCheckoutPOS.DAL.Repositories.CheckoutRepository(fixture.Options()),
            new FakeApprover(approverRole));
        var cart = FixtureData.Cart(service, p.ProductId);
        service.ApplyBillDiscount(cart, percent, "Percent");
        var approval = new SwiftCheckoutPOS.BLL.DTOs.ApprovalRequestDTO { Username = "boss", Password = "pw" };
        var plan = SwiftCheckoutPOS.BLL.DTOs.PaymentPlanDTO.Single("Card");
        if (!succeeds)
        {
            Assert.Throws<CheckoutValidationException>(() => service.CompleteSale(cart, plan, FixtureData.Actor, "Cashier", approval));
            return;
        }
        service.CompleteSale(cart, plan, FixtureData.Actor, "Cashier", approval);
        using var verify = fixture.Context();
        Assert.Equal("User 9: Boss", verify.Sales.Single().ApprovedBy);
        Assert.Equal("SaleApproval", verify.AuditEvents.Single().Action);
    }

    private sealed class FakeApprover(string role) : IApprover
    {
        public (string Actor, string Role)? Verify(string username, string password) =>
            username == "boss" && password == "pw" ? ("User 9: Boss", role) : null;
    }

    [Fact]
    [Trait("Fixes", "DR-26")]
    public void Approved_ReceiptNumbersAssignedAtCompletionAndSequentialPerDay_PRQ45()
    {
        var p = Seed(stock: 10);
        var service = FixtureData.Checkout(fixture);
        var held = FixtureData.Cart(service, p.ProductId);
        service.HoldOrder(held, FixtureData.Actor);
        var day = DateTime.Now.ToString("yyyyMMdd");
        var first = service.CompleteSale(FixtureData.Cart(service, p.ProductId), "Cash", FixtureData.Actor);
        var second = service.CompleteSale(FixtureData.Cart(service, p.ProductId), "Card", FixtureData.Actor);
        Assert.Equal($"SCP-{day}-0001", first.ReceiptNumber);
        Assert.Equal($"SCP-{day}-0002", second.ReceiptNumber);
        using var verify = fixture.Context();
        Assert.DoesNotContain(verify.Sales.Where(s => s.OrderStatus == "Held"), s => s.ReceiptNumber.StartsWith("SCP-")); // held: no number yet
        // Replaying the same cart returns the same receipt, no second number.
        var cart = FixtureData.Cart(service, p.ProductId);
        var a = service.CompleteSale(cart, "Cash", FixtureData.Actor);
        var b = service.CompleteSale(cart, "Cash", FixtureData.Actor);
        Assert.Equal(a.ReceiptNumber, b.ReceiptNumber);
        Assert.True(b.AlreadyCompleted);
    }

    [Theory]
    [Trait("Fixes", "DR-27")]
    [InlineData("Cash")]
    [InlineData("Card")]
    [InlineData("bKash")]
    [InlineData("Nagad")]
    public void Approved_AcceptsApprovedSingleTenders_PRQ85(string tender)
    {
        var p = Seed();
        var service = FixtureData.Checkout(fixture);
        service.CompleteSale(FixtureData.Cart(service, p.ProductId), tender, FixtureData.Actor);
        using var verify = fixture.Context();
        Assert.Equal(tender, verify.Sales.Single().PaymentMethod);
    }

    [Theory]
    [InlineData("Mobile Wallet")]
    [InlineData("Cheque")]
    public void Approved_RejectsUnknownTender_PRQ85(string tender)
    {
        var p = Seed();
        var service = FixtureData.Checkout(fixture);
        Assert.Throws<CheckoutValidationException>(() =>
            service.CompleteSale(FixtureData.Cart(service, p.ProductId), tender, FixtureData.Actor));
        FixtureData.AssertEmptySaleEffects(fixture, p.ProductId, 10);
    }

    [Fact]
    [Trait("Fixes", "DR-27")]
    public void Approved_SplitCashAndBkashRoundsOnlyTheCashPart_CQ01()
    {
        var p = Seed(price: 100.49m);
        var service = FixtureData.Checkout(fixture);
        var plan = new SwiftCheckoutPOS.BLL.DTOs.PaymentPlanDTO { Lines = [
            new() { Tender = "Cash", Tendered = 50m },
            new() { Tender = "bKash", Amount = 60m, Reference = "TRX123" } ] };
        var result = service.CompleteSale(FixtureData.Cart(service, p.ProductId), plan, FixtureData.Actor, "Cashier", null);
        using var verify = fixture.Context();
        var sale = verify.Sales.Single();
        Assert.Equal("Cash+bKash", sale.PaymentMethod);
        Assert.Equal(100.49m, sale.TotalAmount);
        Assert.Equal(-0.49m, sale.RoundingAmount);                 // cash part 40.49 -> 40
        var payments = verify.SalePayments.OrderBy(x => x.Tender).ToList();
        Assert.Equal(40m, payments.Single(x => x.Tender == "Cash").Amount);
        Assert.Equal(60m, payments.Single(x => x.Tender == "bKash").Amount);
        Assert.Equal("TRX123", payments.Single(x => x.Tender == "bKash").Reference);
        Assert.Equal(10m, result.Receipt.ReceiptPayments!.Single(x => x.Tender == "Cash").Change); // 50 given
    }

    [Fact]
    [Trait("Fixes", "DR-28")]
    public void Approved_DueSaleNeedsCustomerApprovalAndLimit_PRQ86()
    {
        var p = Seed(price: 1000m, cost: 100m, stock: 10);
        int customerId;
        using (var seed = fixture.Context())
        {
            var c = new Customer { FullName = "Due customer", PhoneNumber = "01700000000", DueBalance = 1000m };
            seed.Customers.Add(c); seed.SaveChanges(); customerId = c.CustomerId;
        }
        var service = FixtureData.Checkout(fixture);
        CheckoutSessionDTO Cart(int qty)
        {
            var cart = FixtureData.Cart(service, p.ProductId, qty);
            cart.CustomerType = "Loyalty"; cart.CustomerId = customerId; cart.CustomerName = "Due customer";
            return cart;
        }
        var duePlan = SwiftCheckoutPOS.BLL.DTOs.PaymentPlanDTO.Single("Due");
        // Cashier alone cannot sell on Due.
        Assert.StartsWith("APPROVAL REQUIRED", Assert.Throws<CheckoutValidationException>(() =>
            service.CompleteSale(Cart(1), duePlan, FixtureData.Actor, "Cashier", null)).Message);
        // Manager may; balance 1000 + 1000 = 2000 <= 2500.
        service.CompleteSale(Cart(1), duePlan, FixtureData.Actor, "Manager", null);
        // Next 1000 would make 3000 > 2500.
        Assert.Contains("Due limit", Assert.Throws<CheckoutValidationException>(() =>
            service.CompleteSale(Cart(1), duePlan, FixtureData.Actor, "Manager", null)).Message);
        // Collect 500, then the balance is 1500.
        Assert.Equal(1500m, service.CollectDue(customerId, 500m, "Cash", null, FixtureData.Actor));
        using var verify = fixture.Context();
        Assert.Equal(1500m, verify.Customers.Single().DueBalance);
        Assert.Single(verify.CustomerDuePayments);
        Assert.Single(verify.Sales, s => s.OrderStatus == "Completed");
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
    [Trait("Fixes", "DR-33")]
    public void Approved_ResumeKeepsReservationUntilCompletion_CQ04()
    {
        var p = Seed(stock: 20);
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId, 8);
        service.HoldOrder(cart, FixtureData.Actor);
        var resumed = service.ResumeOrder(cart.PersistedSaleId!.Value);
        using (var verify = fixture.Context())
        {
            Assert.Equal(20, verify.Products.Single().StockQuantity);  // still owned
            Assert.Equal(8, verify.Products.Single().ReservedQuantity); // still reserved while resumed
        }
        // Another cashier cannot take the reserved units.
        var other = new SwiftCheckoutPOS.BLL.DTOs.CheckoutSessionDTO();
        Assert.Throws<SwiftCheckoutPOS.BLL.Services.CheckoutValidationException>(() => service.AddToCart(other, p.ProductId, 13));
        var completed = service.CompleteSale(resumed, "Cash", FixtureData.Actor);
        Assert.Equal(cart.PersistedSaleId, completed.SaleId);
        using var final = fixture.Context();
        Assert.Single(final.Sales);
        Assert.Equal(12, final.Products.Single().StockQuantity);
        Assert.Equal(0, final.Products.Single().ReservedQuantity);
    }

    [Fact]
    [Trait("Fixes", "DR-10")]
    public void Approved_CancelReleasesReservationAndKeepsHistory_PRQ17()
    {
        var p = Seed(stock: 10);
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId, 4);
        service.HoldOrder(cart, FixtureData.Actor);
        var resumed = service.ResumeOrder(cart.PersistedSaleId!.Value);
        service.CancelCart(resumed);
        using var verify = fixture.Context();
        Assert.Equal("Cancelled", verify.Sales.Single().OrderStatus);
        Assert.Equal(10, verify.Products.Single().StockQuantity);
        Assert.Equal(0, verify.Products.Single().ReservedQuantity);
    }

    [Theory]
    [Trait("Fixes", "DR-10")]
    [InlineData(72, 73, true)]
    [InlineData(72, 71, false)]
    [InlineData(0, 10000, false)] // 0 = never expire
    public void Approved_HeldOrdersExpireAfterConfiguredHours_PRQ18(int hours, int ageHours, bool expires)
    {
        var p = Seed(stock: 10);
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId, 3);
        service.HoldOrder(cart, FixtureData.Actor);
        var expired = service.ExpireHeldOrders(hours, DateTime.Now.AddHours(ageHours));
        Assert.Equal(expires ? 1 : 0, expired);
        using var verify = fixture.Context();
        Assert.Equal(expires ? "Cancelled" : "Held", verify.Sales.Single().OrderStatus);
        Assert.Equal(expires ? 0 : 3, verify.Products.Single().ReservedQuantity);
        Assert.Equal(10, verify.Products.Single().StockQuantity);
    }

    [Fact]
    [Trait("Fixes", "DR-10")]
    public void Approved_ResumedOrderDoesNotExpireAndReholdRestartsClock_CQ20()
    {
        var p = Seed(stock: 10);
        var service = FixtureData.Checkout(fixture);
        var cart = FixtureData.Cart(service, p.ProductId, 3);
        service.HoldOrder(cart, FixtureData.Actor);
        var resumed = service.ResumeOrder(cart.PersistedSaleId!.Value);
        Assert.Equal(0, service.ExpireHeldOrders(72, DateTime.Now.AddHours(100))); // resumed: no expiry
        service.HoldOrder(resumed, FixtureData.Actor);                              // re-hold: new clock
        Assert.Equal(0, service.ExpireHeldOrders(72, DateTime.Now.AddHours(71)));
        using var verify = fixture.Context();
        Assert.Equal(3, verify.Products.Single().ReservedQuantity);
        Assert.Equal("Held", verify.Sales.Single().OrderStatus);
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
    public async Task Approved_ConcurrentCompletionsAreSerializedWithOneFinancialEffect(bool sameReceipt)
    {
        // The per-day receipt counter row is locked inside each completion, so concurrent completions run one
        // after the other. The second then sees the first's result (replay, or "not enough stock").
        var p = Seed(stock: 1);
        var service = FixtureData.Checkout(fixture);
        var first = FixtureData.Cart(service, p.ProductId);
        var second = FixtureData.Cart(service, p.ProductId);
        if (sameReceipt) second.InvoiceNumber = first.InvoiceNumber;
        using var start = new ManualResetEventSlim();
        var errors = await Task.WhenAll(
            Task.Run(() => { start.Wait(); return Record.Exception(() => service.CompleteSale(first, "Cash", FixtureData.Actor)); }),
            Task.Run(() => { start.Wait(); return Record.Exception(() => service.CompleteSale(second, "Cash", FixtureData.Actor)); }),
            Task.Run(() => { start.Set(); return (Exception?)null; }));
        Assert.All(errors.Where(e => e is not null), e =>
            Assert.True(e is DbUpdateConcurrencyException || e is CheckoutValidationException,
                $"Race must return a known conflict, got {e!.GetType().Name}."));
        using var verify = fixture.Context();
        Assert.Single(verify.Sales);
        Assert.Single(verify.SaleItems);
        Assert.Single(verify.LedgerEntries);
        Assert.Equal(0, verify.Products.Single().StockQuantity);
    }
}
