using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;

namespace SwiftCheckoutPOS.DAL.Repositories;

/// <summary>BP-11/T14: bounded SQL aggregates for reports. Periods are half-open [from, to).</summary>
public sealed class MetricsRepository(ApplicationDbContext context)
{
    private static readonly string[] Sold = ["Completed", "PartiallyRefunded", "Refunded"]; // voided sales are excluded

    public sealed record SalesTotals(int Count, decimal Total, decimal Vat, decimal Rounding, decimal Discounts,
        decimal Cost, int Items, int UnknownCostSales, decimal UnknownCostNet);

    public SalesTotals Sales(DateTime from, DateTime to)
    {
        var sales = context.Sales.AsNoTracking().Where(s => Sold.Contains(s.OrderStatus) && s.SaleDate >= from && s.SaleDate < to);
        var items = context.SaleItems.AsNoTracking().Where(i => sales.Any(s => s.SaleId == i.SaleId));
        // PR-Q38: a sale with any zero cost-at-sale line is "cost unknown" and is excluded from profit.
        var unknown = sales.Where(s => s.SaleItems.Any(i => i.CostAtSale == 0));
        return new SalesTotals(
            sales.Count(),
            sales.Sum(s => (decimal?)s.TotalAmount) ?? 0,
            sales.Sum(s => (decimal?)s.TaxAmount) ?? 0,
            sales.Sum(s => (decimal?)s.RoundingAmount) ?? 0,
            (sales.Sum(s => (decimal?)(s.SubTotal - s.TotalAmount)) ?? 0) + (items.Sum(i => (decimal?)(i.UnitPrice * i.Quantity - i.LineTotal)) ?? 0),
            items.Where(i => !unknown.Any(u => u.SaleId == i.SaleId)).Sum(i => (decimal?)(i.CostAtSale * i.Quantity)) ?? 0,
            items.Sum(i => (int?)i.Quantity) ?? 0,
            unknown.Count(),
            unknown.Sum(s => (decimal?)(s.TotalAmount - s.TaxAmount)) ?? 0);
    }

    public sealed record RefundTotals(int Count, decimal Amount, decimal Vat, decimal Cost, decimal PayoutRounding, decimal PaidBack);

    /// <summary>PR-Q31: refunds count on the day they happen. Voids are excluded (their sale is already excluded).</summary>
    public RefundTotals Refunds(DateTime from, DateTime to)
    {
        var refunds = context.Refunds.AsNoTracking().Where(r => r.Kind == "Refund" && r.RefundedAt >= from && r.RefundedAt < to);
        return new RefundTotals(refunds.Count(),
            refunds.Sum(r => (decimal?)r.Amount) ?? 0, refunds.Sum(r => (decimal?)r.VatReversed) ?? 0,
            refunds.Sum(r => (decimal?)r.CostReversed) ?? 0, refunds.Sum(r => (decimal?)r.PayoutRounding) ?? 0,
            context.RefundPayouts.Where(p => refunds.Any(r => r.RefundId == p.RefundId)).Sum(p => (decimal?)p.Amount) ?? 0);
    }

    public decimal Ledger(DateTime from, DateTime to, string type, params string[] categories) =>
        context.LedgerEntries.AsNoTracking()
            .Where(e => e.TransactionDate >= from && e.TransactionDate < to && e.TransactionType == type && categories.Contains(e.Category))
            .Sum(e => (decimal?)e.Amount) ?? 0;

    /// <summary>Money received at the till by tender for sales completed in the period (voided sales excluded).</summary>
    public Dictionary<string, decimal> TenderTotals(DateTime from, DateTime to)
    {
        var result = context.SalePayments.AsNoTracking()
            .Where(p => Sold.Contains(p.Sale.OrderStatus) && p.Sale.SaleDate >= from && p.Sale.SaleDate < to)
            .GroupBy(p => p.Tender).Select(g => new { g.Key, Sum = g.Sum(p => p.Amount) }).ToDictionary(x => x.Key, x => x.Sum);
        // Legacy rows without tender lines.
        foreach (var g in context.Sales.AsNoTracking()
                     .Where(s => Sold.Contains(s.OrderStatus) && s.SaleDate >= from && s.SaleDate < to && !s.Payments.Any())
                     .GroupBy(s => s.PaymentMethod).Select(g => new { g.Key, Sum = g.Sum(s => s.TotalAmount + s.RoundingAmount) }))
            result[g.Key] = result.GetValueOrDefault(g.Key) + g.Sum;
        return result;
    }

    public decimal DueCollections(DateTime from, DateTime to) =>
        context.CustomerDuePayments.AsNoTracking().Where(p => p.ReceivedAt >= from && p.ReceivedAt < to).Sum(p => (decimal?)p.Amount) ?? 0;

    public int HeldCount() => context.Sales.Count(s => s.OrderStatus == "Held");
    public decimal HeldAmount() => context.Sales.Where(s => s.OrderStatus == "Held").Sum(s => (decimal?)s.TotalAmount) ?? 0;
    public int Voids(DateTime from, DateTime to) => context.Refunds.Count(r => r.Kind == "Void" && r.RefundedAt >= from && r.RefundedAt < to);
    public int NewCustomers(DateTime from, DateTime to) => context.Customers.Count(c => c.RegisteredAt >= from && c.RegisteredAt < to);

    public DateTime? FirstSale() => context.Sales.Where(s => Sold.Contains(s.OrderStatus)).Min(s => (DateTime?)s.SaleDate);
    public DateTime? FirstLedger(params string[] categories) =>
        context.LedgerEntries.Where(e => categories.Contains(e.Category)).Min(e => (DateTime?)e.TransactionDate);
    public decimal TotalDueOutstanding() => context.Customers.Sum(c => (decimal?)c.DueBalance) ?? 0;
}
