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

        // Insights and Stock health read the same data: their totals must agree with the sales table.
        var insights = new InsightsService(new InsightsRepository(c), metrics);
        var month = insights.Get("30days");
        var counted = sold.Where(s => s.OrderStatus is "Completed" or "PartiallyRefunded" or "Refunded").ToList(); // voided sales never count
        var monthSales = counted.Where(s => s.SaleDate >= month.From && s.SaleDate < month.ToExclusive).ToList();
        Assert.Equal(monthSales.Sum(s => s.TotalAmount), month.Trend.Total);
        Assert.Equal(monthSales.Sum(s => s.TotalAmount), month.Heat.Sum(h => h.Total));
        Assert.Equal(monthSales.Count, month.Cashiers.Sum(x => x.Sales));
        Assert.Equal(monthSales.Sum(s => s.TotalAmount), month.WalkInTotal + month.RegisteredTotal);
        Assert.Equal(month.Now.ItemsSold, month.AllProducts.Sum(p => p.Units));
        Assert.True(month.TopByRevenue.Count > 5 && month.TopByRevenue[0].Revenue >= month.TopByRevenue[1].Revenue);
        Assert.Equal(month.AllProducts.Sum(p => p.Revenue), month.Categories.Sum(x => x.Revenue));
        Assert.True(month.Tenders.Count > 0 && month.Customers.Count > 0);
        var year = insights.Get("1year");
        Assert.Equal("month", year.Trend.Unit);
        Assert.Equal(counted.Sum(s => s.TotalAmount), year.Trend.Total);

        var stock = insights.StockHealth();
        Assert.Equal(c.Products.Count(p => p.IsActive), stock.Lines.Count);
        Assert.Equal(c.Products.Where(p => p.IsActive).Sum(p => p.StockQuantity * p.CostPrice), stock.ValueAtCost);
        Assert.All(stock.Lines.Where(l => l.Status == "Out"), l => Assert.True(l.Available <= 0));
        Assert.Contains(stock.Lines, l => l.Sold30 > 0 && l.DaysOfCover is not null);

        var anyProduct = c.Products.First();
        Assert.Contains(insights.Search(anyProduct.ProductName[..4], manager: false, canViewFinancials: false), h => h.Group == "Products");
        Assert.DoesNotContain(insights.Search(sold[0].ReceiptNumber, manager: false, canViewFinancials: false), h => h.Group == "Receipts");
        Assert.Contains(insights.Search(sold[0].ReceiptNumber, manager: true, canViewFinancials: true), h => h.Group == "Receipts" && h.Detail.Contains('৳'));
        Assert.DoesNotContain(insights.Search(sold[0].ReceiptNumber, manager: true, canViewFinancials: false), h => h.Detail.Contains('৳'));
        var alerts = insights.Alerts(manager: true, admin: true, canViewFinancials: true, shiftOpen: true, heldRetentionHours: 72, backupWarnings: ["No backup yet."]);
        Assert.Contains(alerts, a => a.Controller == "Checkout" && a.Text.StartsWith("2 orders are on hold"));
        Assert.Contains(alerts, a => a.Controller == "BackupAdmin");
        var csv = System.Text.Encoding.UTF8.GetString(insights.SalesCsv(from, to));
        Assert.StartsWith("\uFEFFReceipt,Date,Status", csv);
        Assert.Equal(sold.Count(s => s.SaleDate >= from && s.SaleDate < to) + 1, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.DoesNotContain("Cost", System.Text.Encoding.UTF8.GetString(insights.ProductsCsv(showCost: false)).Split('\n')[0]);
        Assert.Contains("Cost", System.Text.Encoding.UTF8.GetString(insights.ProductsCsv(showCost: true)).Split('\n')[0]);

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
