using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;

namespace SwiftCheckoutPOS.DAL.Repositories;

/// <summary>
/// Read-only aggregates for the Insights and Stock health pages, the exports, the search box and the alerts bell.
/// Periods are half-open [from, to). "Sold" follows MetricsRepository: voided and held sales never count.
/// </summary>
public sealed class InsightsRepository(ApplicationDbContext context)
{
    private static readonly string[] Sold = ["Completed", "PartiallyRefunded", "Refunded"];

    public sealed record SaleRow(DateTime SaleDate, decimal Total, decimal Vat, string? Cashier, int? CustomerId);
    public sealed record ProductSalesRow(int ProductId, int Quantity, decimal Revenue, decimal Cost, int UnknownCostQuantity, decimal UnknownCostRevenue);
    public sealed record ProductRow(int ProductId, string Barcode, string Name, int CategoryId, string Category, string Vendor, decimal CostPrice,
        decimal SellingPrice, int Stock, int Reserved, int ReorderLevel, string Unit, bool IsActive);
    public sealed record SaleExportRow(string Receipt, DateTime SaleDate, string Status, string? Customer, string? Cashier, string Payment,
        decimal SubTotal, decimal Vat, decimal Total, decimal Rounding, decimal Refunded);
    public sealed record CustomerRow(int CustomerId, string Name, string Phone, decimal Due);

    /// <summary>One light row per sold sale: enough for the hour/weekday map, the daily trend and cashier and customer totals.</summary>
    public List<SaleRow> Sales(DateTime from, DateTime to) =>
        context.Sales.AsNoTracking().Where(s => Sold.Contains(s.OrderStatus) && s.SaleDate >= from && s.SaleDate < to)
            .Select(s => new SaleRow(s.SaleDate, s.TotalAmount, s.TaxAmount, s.CashierName, s.CustomerId)).ToList();

    /// <summary>
    /// Units, revenue (price x quantity minus every discount on the line, VAT included) and cost at the time of sale,
    /// per product. Lines with no recorded cost are reported separately so profit never treats them as free (PR-Q38).
    /// </summary>
    public List<ProductSalesRow> ProductSales(DateTime from, DateTime to) =>
        context.SaleItems.AsNoTracking()
            .Where(i => Sold.Contains(i.Sale.OrderStatus) && i.Sale.SaleDate >= from && i.Sale.SaleDate < to)
            .GroupBy(i => i.ProductId)
            .Select(g => new ProductSalesRow(g.Key,
                g.Sum(i => i.Quantity),
                g.Sum(i => i.UnitPrice * i.Quantity - i.DiscountAmount),
                g.Sum(i => i.CostAtSale * i.Quantity),
                g.Sum(i => i.CostAtSale == 0 ? i.Quantity : 0),
                g.Sum(i => i.CostAtSale == 0 ? i.UnitPrice * i.Quantity - i.DiscountAmount : 0)))
            .ToList();

    /// <summary>Units given back per product by refunds dated in the period (voids excluded: their sale is not counted).</summary>
    public Dictionary<int, int> RefundedUnits(DateTime from, DateTime to) =>
        context.RefundItems.AsNoTracking()
            .Join(context.Refunds.AsNoTracking().Where(r => r.Kind == "Refund" && r.RefundedAt >= from && r.RefundedAt < to),
                i => i.RefundId, r => r.RefundId, (i, r) => i)
            .GroupBy(i => i.ProductId).Select(g => new { g.Key, Units = g.Sum(i => i.Quantity) })
            .ToDictionary(x => x.Key, x => x.Units);

    public Dictionary<int, DateTime> LastSold() =>
        context.SaleItems.AsNoTracking().Where(i => Sold.Contains(i.Sale.OrderStatus))
            .GroupBy(i => i.ProductId).Select(g => new { g.Key, Last = g.Max(i => i.Sale.SaleDate) })
            .ToDictionary(x => x.Key, x => x.Last);

    public List<ProductRow> Products() =>
        context.Products.AsNoTracking().OrderBy(p => p.ProductName)
            .Select(p => new ProductRow(p.ProductId, p.Barcode, p.ProductName, p.CategoryId, p.Category.CategoryName, p.Vendor.VendorName,
                p.CostPrice, p.SellingPrice, p.StockQuantity, p.ReservedQuantity, p.ReorderLevel, p.UnitOfMeasure, p.IsActive)).ToList();

    public Dictionary<int, string> CustomerNames(IEnumerable<int> ids)
    {
        var wanted = ids.Distinct().ToList();
        return context.Customers.AsNoTracking().Where(c => wanted.Contains(c.CustomerId)).ToDictionary(c => c.CustomerId, c => c.FullName);
    }

    public List<CustomerRow> CustomersWithDue(int take) =>
        context.Customers.AsNoTracking().Where(c => c.DueBalance > 0).OrderByDescending(c => c.DueBalance).Take(take)
            .Select(c => new CustomerRow(c.CustomerId, c.FullName, c.PhoneNumber, c.DueBalance)).ToList();

    /// <summary>Every sale in the period, newest first, for the spreadsheet export (held orders are left out).</summary>
    public List<SaleExportRow> SalesForExport(DateTime from, DateTime to, int take) =>
        context.Sales.AsNoTracking().Where(s => s.SaleDate >= from && s.SaleDate < to && s.OrderStatus != "Held" && s.OrderStatus != "Resumed")
            .OrderByDescending(s => s.SaleDate).Take(take)
            .Select(s => new SaleExportRow(s.ReceiptNumber, s.SaleDate, s.OrderStatus, s.Customer != null ? s.Customer.FullName : null, s.CashierName,
                s.PaymentMethod, s.SubTotal, s.TaxAmount, s.TotalAmount, s.RoundingAmount, s.RefundedAmount)).ToList();

    // ---- search box --------------------------------------------------------------------------------------------
    public List<ProductRow> FindProducts(string term, int take) =>
        context.Products.AsNoTracking().Where(p => p.ProductName.Contains(term) || p.Barcode.Contains(term)).OrderBy(p => p.ProductName).Take(take)
            .Select(p => new ProductRow(p.ProductId, p.Barcode, p.ProductName, p.CategoryId, p.Category.CategoryName, p.Vendor.VendorName,
                p.CostPrice, p.SellingPrice, p.StockQuantity, p.ReservedQuantity, p.ReorderLevel, p.UnitOfMeasure, p.IsActive)).ToList();

    public List<CustomerRow> FindCustomers(string term, int take) =>
        context.Customers.AsNoTracking().Where(c => c.FullName.Contains(term) || c.PhoneNumber.Contains(term)).OrderBy(c => c.FullName).Take(take)
            .Select(c => new CustomerRow(c.CustomerId, c.FullName, c.PhoneNumber, c.DueBalance)).ToList();

    public List<(string Receipt, DateTime SaleDate, decimal Total, string Status)> FindReceipts(string term, int take) =>
        context.Sales.AsNoTracking().Where(s => s.ReceiptNumber.Contains(term) && s.OrderStatus != "Held" && s.OrderStatus != "Resumed")
            .OrderByDescending(s => s.SaleDate).Take(take)
            .Select(s => new { s.ReceiptNumber, s.SaleDate, s.TotalAmount, s.OrderStatus }).AsEnumerable()
            .Select(s => (s.ReceiptNumber, s.SaleDate, s.TotalAmount, s.OrderStatus)).ToList();

    // ---- alerts bell -------------------------------------------------------------------------------------------
    public int OutOfStockCount() => context.Products.Count(p => p.IsActive && p.StockQuantity - p.ReservedQuantity <= 0);
    public int LowStockCount() => context.Products.Count(p => p.IsActive && p.StockQuantity - p.ReservedQuantity > 0
        && p.StockQuantity - p.ReservedQuantity <= p.ReorderLevel);
    public int HeldOrders() => context.Sales.Count(s => s.OrderStatus == "Held");
    public DateTime? OldestHeldAt() => context.Sales.Where(s => s.OrderStatus == "Held").Min(s => (DateTime?)(s.HeldAt ?? s.SaleDate));
    public (int Customers, decimal Total) DueOutstanding() =>
        (context.Customers.Count(c => c.DueBalance > 0), context.Customers.Sum(c => (decimal?)c.DueBalance) ?? 0);
}
