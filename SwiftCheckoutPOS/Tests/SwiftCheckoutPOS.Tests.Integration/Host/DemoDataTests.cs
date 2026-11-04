using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Repositories;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;
using SwiftCheckoutPOS.UI.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Host;

/// <summary>The --seed-demo data is made by the real services and stays consistent after its dates are moved back.</summary>
[Collection("SqlServer")]
public sealed class DemoDataTests
{
    private readonly SqlServerFixture fixture;
    public DemoDataTests(SqlServerFixture fixture) { this.fixture = fixture; fixture.Reset(); }

    [Fact]
    [Trait("Fixes", "DEMO")]
    public void DemoShopIsConsistentAndTheTillKeepsWorkingAfterIt()
    {
        var cs = fixture.Guard.ForTestDatabase();
        Assert.True(DemoDataSeeder.IsEmpty(cs));
        var seeder = new DemoDataSeeder(cs);
        seeder.Run(days: 8);
        Assert.False(DemoDataSeeder.IsEmpty(cs));
        Assert.True(seeder.Sales > 60, $"only {seeder.Sales} sales");

        using var c = fixture.Context();
        Assert.Equal(["nasrin", "owner", "rahim", "sumaiya"], c.Users.Select(u => u.Username).OrderBy(u => u).ToArray());
        var sold = c.Sales.AsNoTracking().Where(s => s.OrderStatus != "Held" && s.OrderStatus != "Resumed").ToList();
        // Every receipt carries the date of its own (moved-back) sale and numbers start at 0001 each day.
        Assert.All(sold, s => Assert.Equal($"SCP-{s.SaleDate:yyyyMMdd}-", s.ReceiptNumber[..13]));
        foreach (var day in sold.GroupBy(s => s.SaleDate.Date))
            Assert.Equal(Enumerable.Range(1, day.Count()).Select(n => n.ToString("D4")), day.OrderBy(s => s.SaleId).Select(s => s.ReceiptNumber[13..]));
        Assert.Equal(sold.Select(s => s.SaleDate.Date).Distinct().Count(), c.ReceiptSequences.Count());
        Assert.True(sold.Select(s => s.SaleDate.Date).Distinct().Count() >= 8);
        Assert.All(c.LedgerEntries.AsNoTracking().ToList(), e => Assert.True(e.TransactionDate <= DateTime.Now.AddMinutes(1)));
        Assert.Single(c.Shifts.Where(s => s.Status == "Open"));
        Assert.True(c.Shifts.Count(s => s.Status == "Closed") >= 8);
        Assert.Equal(2, c.Sales.Count(s => s.OrderStatus == "Held"));
        Assert.True(c.Products.All(p => p.StockQuantity >= 0 && p.ReservedQuantity <= p.StockQuantity));
        Assert.True(c.Customers.Count() == 20);

        var metrics = new MetricsService(new MetricsRepository(c));
        var (from, to) = MetricsService.Days(DateTime.Today.AddDays(-8), DateTime.Today);
        Assert.True(metrics.Period(from, to).NetSales > 0);

        // A real sale made now after seeding gets the next number for today without a clash.
        var checkout = FixtureData.Checkout(fixture);
        var id = c.Products.First(p => p.StockQuantity - p.ReservedQuantity > 5).ProductId;
        var receipt = checkout.CompleteSale(FixtureData.Cart(checkout, id, 1), "Cash", "User 3: Rahim Uddin").ReceiptNumber;
        Assert.StartsWith($"SCP-{DateTime.Today:yyyyMMdd}-", receipt);
        // And a seeded sale from earlier this week can be looked up for a refund.
        var recent = sold.Where(s => s.OrderStatus == "Completed" && s.SaleDate > DateTime.Now.AddDays(-5)).OrderByDescending(s => s.SaleId).First();
        using var rc = fixture.Context();
        Assert.True(new RefundService(new SalesRepository(rc), new AtomicUnit(rc)).LookupTransaction(recent.ReceiptNumber).CanRefund);
    }
}
