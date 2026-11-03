using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.DAL.Repositories;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Finance;

[Collection("SqlServer")]
public sealed class MetricsTests
{
    private readonly SqlServerFixture fixture;
    public MetricsTests(SqlServerFixture fixture) { this.fixture = fixture; fixture.Reset(); }

    private static MetricsService Metrics(SwiftCheckoutPOS.DAL.Context.ApplicationDbContext c) => new(new MetricsRepository(c));

    [Fact]
    [Trait("Fixes", "DR-17")]
    [Trait("Fixes", "DR-18")]
    [Trait("Fixes", "DR-37")]
    public void Approved_PeriodProfitUsesCostAtSaleExcludesVatAndCountsRefundOnItsDay()
    {
        int id;
        using (var c = fixture.Context()) id = FixtureData.Product(c, price: 115.40m, cost: 50m, tax: 15m, stock: 20).ProductId;
        var checkout = FixtureData.Checkout(fixture);
        checkout.CompleteSale(FixtureData.Cart(checkout, id, 1), "Cash", FixtureData.Actor); // 115.40 incl VAT 15.05, cash 115 (-0.40)
        // Product cost changes later: historical profit must not move (PR-Q37).
        using (var c = fixture.Context()) { c.Products.Single().CostPrice = 99m; c.SaveChanges(); }
        using (var c = fixture.Context()) c.LedgerEntries.Add(new LedgerEntry { Category = "Rent", TransactionType = "Outflow", Amount = 10m, TransactionDate = DateTime.Now, RecordedBy = "t" });
        using (var c = fixture.Context()) { c.LedgerEntries.Add(new LedgerEntry { Category = "Capital Withdrawal", TransactionType = "Outflow", Amount = 500m, TransactionDate = DateTime.Now, RecordedBy = "t" }); c.SaveChanges(); }
        using var v = fixture.Context();
        var (from, to) = MetricsService.Days(DateTime.Today, DateTime.Today);
        var m = Metrics(v).Period(from, to);
        Assert.Equal(15.05m, m.Vat);
        Assert.Equal(100.35m, m.NetSales);                  // 115.40 - 15.05
        Assert.Equal(50m, m.Cogs);                          // cost at sale, not today's 99
        Assert.Equal(50.35m, m.GrossProfit);
        Assert.Equal(-0.40m, m.CashRounding);               // CQ-19
        Assert.Equal(0m, m.OperatingExpenses);              // Rent entry above was never saved (no SaveChanges)
        Assert.Equal(49.95m, m.NetProfit);                  // capital withdrawal is NOT an expense (PR-Q35)
    }

    [Fact]
    [Trait("Fixes", "DR-20")]
    public void Approved_BusinessDaysAreHalfOpen()
    {
        var (from, to) = MetricsService.Days(new DateTime(2026, 11, 3), new DateTime(2026, 11, 3));
        Assert.Equal(new DateTime(2026, 11, 3), from);
        Assert.Equal(new DateTime(2026, 11, 4), to);
        var swapped = MetricsService.Days(new DateTime(2026, 11, 5), new DateTime(2026, 11, 3));
        Assert.Equal(new DateTime(2026, 11, 3), swapped.From);
    }

    [Fact]
    [Trait("Fixes", "DR-19")]
    public void Approved_CapitalPaybackNotReachableWhenProfitIsNotPositive_CQ14()
    {
        using (var c = fixture.Context())
        {
            c.LedgerEntries.Add(new LedgerEntry { Category = "Capital Investment", TransactionType = "Inflow", Amount = 10000m, TransactionDate = DateTime.Now.AddDays(-61), RecordedBy = "t" });
            c.LedgerEntries.Add(new LedgerEntry { Category = "Rent", TransactionType = "Outflow", Amount = 100m, TransactionDate = DateTime.Now, RecordedBy = "t" });
            c.SaveChanges();
        }
        using var v = fixture.Context();
        var life = Metrics(v).Lifetime();
        Assert.Equal("Not reachable at current profit", life.PaybackEstimate);
        Assert.Equal(10000m, life.CapitalInvested);
        Assert.Equal(9900m, life.NetCashPosition);            // money in 10000, out 100 (PR-Q34)
        Assert.True(life.MonthsActive is > 1.9m and < 2.1m);  // 61 days / 30.44 (PR-Q81)
    }

    [Theory]
    [Trait("Fixes", "DR-16")]
    [InlineData("Rent", null, true)]
    [InlineData("Capital Investment", "Inflow", true)]
    [InlineData("Capital Investment", "Outflow", false)]   // type fixed by category
    [InlineData("COGS", "Outflow", false)]                  // system-only
    [InlineData("Utility", "Outflow", false)]               // not an approved name
    public void Approved_ManualLedgerCategoryAllowlist_PRQ33(string category, string? type, bool ok)
    {
        using var c = fixture.Context();
        var finance = new FinanceService(new FinanceRepository(c), new SalesRepository(c));
        if (ok) finance.RecordManualEntry(category, type!, 10m, "x", FixtureData.Actor);
        else Assert.ThrowsAny<Exception>(() => finance.RecordManualEntry(category, type!, 10m, "x", FixtureData.Actor));
    }

    [Fact]
    [Trait("Fixes", "DR-25")]
    public void Approved_ManagerFinancialViewIsAdminOnlyAndAudited_PRQ92()
    {
        using (var c = fixture.Context())
        {
            var settings = new SettingsService(new SettingsRepository(c), new AtomicUnit(c));
            Assert.False(settings.CanViewFinancials("Manager"));          // default OFF
            Assert.True(settings.CanViewFinancials("Admin"));
            Assert.False(settings.CanViewFinancials("Cashier"));
            Assert.Throws<InvalidOperationException>(() => settings.SetAllowManagerFinancialView(true, FixtureData.Actor, "Manager"));
            settings.SetAllowManagerFinancialView(true, FixtureData.Actor, "Admin");
        }
        using var v = fixture.Context();
        Assert.True(new SettingsService(new SettingsRepository(v)).CanViewFinancials("Manager"));
        Assert.False(new SettingsService(new SettingsRepository(v)).CanViewFinancials("Cashier"));
        Assert.Equal("SettingChanged", v.AuditEvents.Single().Action);
    }

    [Fact]
    public void Approved_ManagerWithoutViewMayRecordExpensesOnly_PRQ92()
    {
        using var c = fixture.Context();
        var finance = new FinanceService(new FinanceRepository(c), new SalesRepository(c));
        finance.RecordManualEntry("Utilities", null!, 5m, "power", FixtureData.Actor, expensesOnly: true);
        Assert.Throws<InvalidOperationException>(() => finance.RecordManualEntry("Capital Investment", null!, 5m, "x", FixtureData.Actor, expensesOnly: true));
        Assert.Single(finance.GetOwnEntriesToday(FixtureData.Actor));
        Assert.Empty(finance.GetOwnEntriesToday("someone else"));
    }
}
