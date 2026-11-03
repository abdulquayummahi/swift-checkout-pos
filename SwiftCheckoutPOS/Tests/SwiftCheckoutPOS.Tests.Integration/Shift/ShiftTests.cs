using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Repositories;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Shifts;

[Collection("SqlServer")]
public sealed class ShiftTests
{
    private readonly SqlServerFixture fixture;
    public ShiftTests(SqlServerFixture fixture) { this.fixture = fixture; fixture.Reset(); }

    private ShiftService Shifts(SwiftCheckoutPOS.DAL.Context.ApplicationDbContext c, IApprover? approver = null) =>
        new(new ShiftRepository(c), new AtomicUnit(c), approver);

    [Fact]
    [Trait("Fixes", "DR-21")]
    public void Approved_PaymentsNeedAnOpenShift_CQ12()
    {
        int productId;
        using (var c = fixture.Context()) productId = FixtureData.Product(c).ProductId;
        using (var c = fixture.Context()) Shifts(c).Close(0m, null, FixtureData.Actor, "Admin"); // close the fixture shift
        var checkout = FixtureData.Checkout(fixture);
        Assert.Contains("Open a shift", Assert.Throws<CheckoutValidationException>(() =>
            checkout.CompleteSale(FixtureData.Cart(checkout, productId), "Cash", FixtureData.Actor)).Message);
    }

    [Fact]
    [Trait("Fixes", "DR-21")]
    public void Approved_OnlyOneShiftAndExpectedCashFormula_PRQ42_PRQ43()
    {
        int productId;
        using (var c = fixture.Context()) productId = FixtureData.Product(c, price: 100.40m).ProductId;
        using (var c = fixture.Context()) Shifts(c).Close(0m, null, FixtureData.Actor, "Admin");
        using (var c = fixture.Context()) Shifts(c).Open(500m, FixtureData.Actor);
        using (var c = fixture.Context()) Assert.Throws<InvalidOperationException>(() => Shifts(c).Open(100m, FixtureData.Actor));
        var checkout = FixtureData.Checkout(fixture);
        checkout.CompleteSale(FixtureData.Cart(checkout, productId), "Cash", FixtureData.Actor);  // +100 (rounded)
        checkout.CompleteSale(FixtureData.Cart(checkout, productId), "Card", FixtureData.Actor);  // no cash
        using (var c = fixture.Context()) Shifts(c).RecordCash("PaidOut", 30m, "Tea", FixtureData.Actor);
        using (var c = fixture.Context()) Shifts(c).RecordCash("PaidIn", 10m, "Change top-up", FixtureData.Actor);
        using var v = fixture.Context();
        var current = Shifts(v).Current()!;
        Assert.Equal(100m, current.CashSales);
        Assert.Equal(580m, current.ExpectedCash); // 500 + 100 - 30 + 10
    }

    [Theory]
    [Trait("Fixes", "DR-21")]
    [InlineData("Cashier", 1000, true)]   // exactly 1000 short: no approval
    [InlineData("Cashier", 1000.01, false)]
    [InlineData("Manager", 5000, true)]   // signed-in manager approves
    public void Approved_BlindCloseNeedsApprovalAboveThousand_PRQ88_CQ08(string role, double shortBy, bool closes)
    {
        using (var c = fixture.Context()) FixtureData.EnsureShift(c);
        using var context = fixture.Context();
        var counted = 2000m; // expected = 0 float + 0 sales... make expected 2000 + shortBy by paying in
        Shifts(context).RecordCash("PaidIn", counted + (decimal)shortBy, "Float top-up", FixtureData.Actor);
        using var c2 = fixture.Context();
        if (!closes)
        {
            Assert.StartsWith("APPROVAL REQUIRED", Assert.Throws<InvalidOperationException>(() => Shifts(c2).Close(counted, null, FixtureData.Actor, role)).Message);
            return;
        }
        var closed = Shifts(c2).Close(counted, "count", FixtureData.Actor, role);
        Assert.Equal(-(decimal)shortBy, closed.Difference);
        Assert.Equal("Closed", closed.Status);
    }

    [Fact]
    public void Approved_NoSaleOnlyForManagerAdminAndLogged_PRQ47()
    {
        using (var c = fixture.Context()) FixtureData.EnsureShift(c);
        using (var c = fixture.Context()) Assert.Throws<InvalidOperationException>(() => Shifts(c).NoSale("check", FixtureData.Actor, "Cashier"));
        using (var c = fixture.Context()) Shifts(c).NoSale("Give change", FixtureData.Actor, "Manager");
        using var v = fixture.Context();
        Assert.Equal("NoSale", v.CashMovements.Single().Type);
        Assert.Equal("DrawerOpened", v.AuditEvents.Single().Action);
    }
}
