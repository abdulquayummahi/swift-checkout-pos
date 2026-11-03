using SwiftCheckoutPOS.BLL.Pricing;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services;

/// <summary>Approved financial indicators for a period (OWNER_DECISIONS G, CQ-07/14/16/19). All amounts exclude VAT.</summary>
public sealed class PeriodMetricsDTO
{
    public DateTime From { get; set; }
    public DateTime ToExclusive { get; set; }
    public int SalesCount { get; set; }
    public int ItemsSold { get; set; }
    public decimal GrossSalesInclVat { get; set; }
    public decimal Vat { get; set; }
    public decimal Discounts { get; set; }
    public int RefundCount { get; set; }
    public decimal RefundsExVat { get; set; }
    public int VoidCount { get; set; }
    /// <summary>PR-Q36: net sales excluding VAT, after refunds dated in the period.</summary>
    public decimal NetSales { get; set; }
    /// <summary>PR-Q37: cost at the time of sale, minus cost reversed by refunds.</summary>
    public decimal Cogs { get; set; }
    public decimal GrossProfit { get; set; }
    /// <summary>CQ-16/19: cash rounding, other income/expense.</summary>
    public decimal CashRounding { get; set; }
    /// <summary>PR-Q35: Rent, Utilities, Salaries, Transport, Maintenance, Other Expense only.</summary>
    public decimal OperatingExpenses { get; set; }
    /// <summary>Damaged + Inventory Adjustment losses minus adjustment gains.</summary>
    public decimal StockLosses { get; set; }
    public decimal OtherIncome { get; set; }
    public decimal NetProfit { get; set; }
    /// <summary>PR-Q38: sales with unknown cost are excluded from profit (shown as a note).</summary>
    public int UnknownCostSales { get; set; }
    public Dictionary<string, decimal> Tenders { get; set; } = new();
    public decimal DueCollected { get; set; }
}

public sealed class LifetimeMetricsDTO
{
    /// <summary>PR-Q34/CQ-07: all money in − all money out, real money only.</summary>
    public decimal NetCashPosition { get; set; }
    public decimal CapitalInvested { get; set; }
    public decimal NetProfit { get; set; }
    public DateTime? BusinessStart { get; set; }
    public decimal MonthsActive { get; set; }
    public decimal AverageMonthlyProfit { get; set; }
    public decimal PaybackPercent { get; set; }
    /// <summary>PR-Q39/CQ-14: "Capital payback estimate" text.</summary>
    public string PaybackEstimate { get; set; } = "";
    public decimal DueOutstanding { get; set; }
}

public sealed class MetricsService(MetricsRepository metrics)
{
    public static readonly string[] OpexCategories = ["Rent", "Utilities", "Salaries", "Transport", "Maintenance", "Other Expense"];
    public static readonly string[] ManualInflow = ["Capital Investment", "Loan Received", "Other Income"];
    public static readonly string[] ManualOutflow = ["Rent", "Utilities", "Salaries", "Transport", "Maintenance", "Other Expense", "Capital Withdrawal", "Loan Repayment"];
    private static readonly string[] CapitalIn = ["Capital Investment", "Capital"]; // "Capital" = legacy name

    /// <summary>PR-Q58/79, DR-20: business days are local (Asia/Dhaka) midnight to midnight; end date inclusive → half-open.</summary>
    public static (DateTime From, DateTime To) Days(DateTime? start, DateTime? end)
    {
        var s = (start ?? DateTime.Today).Date; var e = (end ?? DateTime.Today).Date;
        if (s > e) (s, e) = (e, s);
        return (s, e.AddDays(1));
    }

    public int HeldCount() => metrics.HeldCount();
    public decimal HeldAmount() => metrics.HeldAmount();

    public PeriodMetricsDTO Period(DateTime from, DateTime to)
    {
        var sales = metrics.Sales(from, to);
        var refunds = metrics.Refunds(from, to);
        var dto = new PeriodMetricsDTO
        {
            From = from, ToExclusive = to, SalesCount = sales.Count, ItemsSold = sales.Items,
            GrossSalesInclVat = sales.Total, Vat = sales.Vat - refunds.Vat, Discounts = sales.Discounts,
            RefundCount = refunds.Count, RefundsExVat = refunds.Amount - refunds.Vat, VoidCount = metrics.Voids(from, to),
            UnknownCostSales = sales.UnknownCostSales, Tenders = metrics.TenderTotals(from, to), DueCollected = metrics.DueCollections(from, to)
        };
        dto.NetSales = sales.Total - sales.Vat - dto.RefundsExVat;
        // Profit excludes unknown-cost sales entirely (their net sales are taken out of the profit base).
        var profitSales = sales.Total - sales.Vat - sales.UnknownCostNet - dto.RefundsExVat;
        dto.Cogs = sales.Cost - refunds.Cost;
        dto.GrossProfit = profitSales - dto.Cogs;
        dto.CashRounding = sales.Rounding - refunds.PayoutRounding;
        dto.OperatingExpenses = metrics.Ledger(from, to, "Outflow", OpexCategories);
        dto.StockLosses = metrics.Ledger(from, to, "Outflow", "Damaged", "Inventory Adjustment") - metrics.Ledger(from, to, "Inflow", "Inventory Adjustment");
        dto.OtherIncome = metrics.Ledger(from, to, "Inflow", "Other Income");
        dto.NetProfit = dto.GrossProfit + dto.CashRounding + dto.OtherIncome - dto.OperatingExpenses - dto.StockLosses;
        return dto;
    }

    public LifetimeMetricsDTO Lifetime(DateTime? now = null)
    {
        var today = (now ?? DateTime.Now);
        var from = new DateTime(1753, 1, 1); var to = today.Date.AddDays(1);
        var p = Period(from, to);
        var tenders = p.Tenders.Where(t => t.Key != "Due").Sum(t => t.Value);
        var moneyIn = tenders + p.DueCollected
            + metrics.Ledger(from, to, "Inflow", ManualInflow.Concat(["Capital"]).ToArray())
            + metrics.Ledger(from, to, "Inflow", "Inventory"); // vendor refunds received
        var moneyOut = metrics.Ledger(from, to, "Outflow", ManualOutflow)
            + metrics.Ledger(from, to, "Outflow", "Inventory", "Refund"); // stock bought, refunds paid (once)
        var capital = metrics.Ledger(from, to, "Inflow", CapitalIn) - metrics.Ledger(from, to, "Outflow", "Capital Withdrawal");
        var firstCapital = metrics.FirstLedger(CapitalIn);
        var firstSale = metrics.FirstSale();
        DateTime? start = firstCapital is null ? firstSale : firstSale is null ? firstCapital : (firstCapital < firstSale ? firstCapital : firstSale);
        var months = start is null ? 1m : Math.Max(1m, (decimal)(today - start.Value).TotalDays / 30.44m); // PR-Q81
        var dto = new LifetimeMetricsDTO
        {
            NetCashPosition = moneyIn - moneyOut, CapitalInvested = capital, NetProfit = p.NetProfit, BusinessStart = start,
            MonthsActive = Math.Round(months, 1), AverageMonthlyProfit = Money.R2(p.NetProfit / months),
            DueOutstanding = metrics.TotalDueOutstanding()
        };
        dto.PaybackPercent = capital > 0 ? Math.Clamp(p.NetProfit / capital * 100m, 0, 100) : 0;
        var remaining = capital - p.NetProfit;
        dto.PaybackEstimate = capital <= 0 ? "No capital recorded"
            : remaining <= 0 ? "Capital paid back"
            : dto.AverageMonthlyProfit <= 0 ? "Not reachable at current profit" // CQ-14
            : $"About {Math.Ceiling(remaining / dto.AverageMonthlyProfit)} more month(s)";
        return dto;
    }
}
