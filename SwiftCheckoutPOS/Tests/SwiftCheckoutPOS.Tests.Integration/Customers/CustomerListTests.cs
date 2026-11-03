using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.DAL.Repositories;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Customers;

[Collection("SqlServer")]
public sealed class CustomerListTests
{
    private readonly SqlServerFixture fixture;
    public CustomerListTests(SqlServerFixture fixture) { this.fixture = fixture; fixture.Reset(); }

    [Fact]
    [Trait("Fixes", "BP-11")]
    public void Approved_CustomerTotalsComeFromSqlAggregatesWithSameDefinitions_PRQ58()
    {
        using (var c = fixture.Context())
        {
            var productId = FixtureData.Product(c).ProductId;
            var rahim = new Customer { FullName = "Rahim", PhoneNumber = "01700000001", RegisteredAt = DateTime.Now };
            var karim = new Customer { FullName = "Karim", PhoneNumber = "01700000002", Address = "Mirpur", RegisteredAt = DateTime.Now };
            c.Customers.AddRange(rahim, karim);
            c.SaveChanges();
            Sale Sale(string status, decimal sub, decimal total, int qty, int n) => new()
            {
                ReceiptNumber = $"CUST-{n}", SaleDate = DateTime.Now, SubTotal = sub, TaxAmount = 0m, TotalAmount = total,
                PaymentMethod = "Cash", CashierName = FixtureData.Actor, OrderStatus = status, CustomerId = rahim.CustomerId,
                SaleItems = [new SaleItem { ProductId = productId, Quantity = qty, UnitPrice = 10m, LineTotal = 10m * qty, CostAtSale = 5m }]
            };
            c.Sales.AddRange(Sale("Completed", 20m, 18m, 2, 1), Sale("PartiallyRefunded", 30m, 30m, 3, 2),
                Sale("Refunded", 10m, 10m, 1, 3), Sale("Held", 50m, 50m, 5, 4));
            c.SaveChanges();
        }
        using var context = fixture.Context();
        var service = new CustomerService(new CustomerRepository(context), new SalesRepository(context));
        var list = service.GetAllCustomers().ToList();
        Assert.Equal(["Karim", "Rahim"], list.Select(p => p.FullName));
        var r = list.Single(p => p.FullName == "Rahim");
        Assert.Equal(48m, r.TotalRevenue);       // 18 + 30 (held and refunded excluded)
        Assert.Equal(2m, r.TotalDiscounts);      // 20 - 18
        Assert.Equal(10m, r.TotalRefunded);
        Assert.Equal(2, r.TotalPurchases);
        Assert.Equal(5, r.TotalItemsSold);
        Assert.Equal(48m - 25m, r.EstimatedProfit); // cost 5 x 5 items
        Assert.Equal(0m, list.Single(p => p.FullName == "Karim").TotalRevenue);
        Assert.Equal("Karim", Assert.Single(service.GetAllCustomers("mirpur")).FullName);
        Assert.Equal("Rahim", service.GetAllCustomers(null, "spent_desc").First().FullName);
    }
}
