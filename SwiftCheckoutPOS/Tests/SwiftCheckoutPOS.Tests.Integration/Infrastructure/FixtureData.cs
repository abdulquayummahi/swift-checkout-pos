using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.Tests.Integration.Infrastructure;

public static class FixtureData
{
    public const string Actor = "User 1: Fixture Cashier";

    public static Product Product(ApplicationDbContext context, decimal price = 10m,
        decimal cost = 5m, decimal tax = 0m, int stock = 10, string barcode = "BP10-ITEM")
    {
        var category = new Category { CategoryName = "BP10 Category" };
        var vendor = new Vendor { VendorName = "BP10 Vendor" };
        context.AddRange(category, vendor);
        context.SaveChanges();
        var product = new Product
        {
            ProductName = "BP10 synthetic item", Barcode = barcode,
            CategoryId = category.CategoryId, VendorId = vendor.VendorId,
            SellingPrice = price, CostPrice = cost, TaxRate = tax, StockQuantity = stock, IsActive = true
        };
        context.Products.Add(product);
        context.SaveChanges(); // raw entity values: do not pre-round through InventoryService
        return product;
    }

    public static CheckoutService Checkout(SqlServerFixture fixture, params Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor[] interceptors) =>
        new(new CheckoutRepository(fixture.Options(interceptors)));

    public static CheckoutSessionDTO Cart(CheckoutService service, int productId, int quantity = 1)
    {
        var cart = new CheckoutSessionDTO();
        service.AddToCart(cart, productId, quantity);
        return cart;
    }

    public static InventoryService Inventory(ApplicationDbContext context) =>
        new(new InventoryRepository(context),
            new FinanceService(new FinanceRepository(context), new SalesRepository(context)));

    public static Sale CompletedSale(ApplicationDbContext context, int productId,
        string status = "Completed", int quantity = 2, int ageDays = 0)
    {
        var sale = new Sale
        {
            ReceiptNumber = "BP10-REFUND", SaleDate = DateTime.Now.AddDays(-ageDays),
            SubTotal = 20m, TaxAmount = 0m, TotalAmount = 20m, PaymentMethod = "Cash",
            CashierName = Actor, OrderStatus = status,
            SaleItems = [new SaleItem { ProductId = productId, Quantity = quantity,
                UnitPrice = 10m, LineTotal = 10m * quantity, CostAtSale = 5m }]
        };
        context.Sales.Add(sale);
        context.LedgerEntries.Add(new LedgerEntry { Category = "COGS", TransactionType = "Outflow",
            Amount = 5m * quantity, TransactionDate = sale.SaleDate, RecordedBy = Actor });
        context.SaveChanges();
        return sale;
    }

    public static void AssertEmptySaleEffects(SqlServerFixture fixture, int productId, int stock)
    {
        using var verify = fixture.Context();
        Assert.Empty(verify.Sales);
        Assert.Empty(verify.SaleItems);
        Assert.Empty(verify.LedgerEntries);
        Assert.Equal(stock, verify.Products.Single(p => p.ProductId == productId).StockQuantity);
    }
}
