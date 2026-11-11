using SwiftCheckoutPOS.BLL.Pricing;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services;

// ---- Insights page ---------------------------------------------------------------------------------------------
public sealed record HeatCellDTO(DayOfWeek Day, int Hour, decimal Total, int Sales);
public sealed record DayPointDTO(DateTime Day, decimal Total, int Sales);
public sealed record ShareDTO(string Name, decimal Amount, int Key = 0);
public sealed record ProductInsightDTO(int ProductId, string Name, string Category, int CategoryId, int Units, decimal Revenue, decimal? Profit)
{
    public decimal? MarginPercent => Profit is { } p && Revenue > 0 ? Math.Round(p / Revenue * 100m, 1) : null;
}
public sealed record CategoryInsightDTO(int CategoryId, string Name, int Units, decimal Revenue, decimal? Profit);
public sealed record CashierInsightDTO(string Name, int Sales, decimal Total)
{
    public decimal AverageBasket => Sales == 0 ? 0 : Money.R2(Total / Sales);
}
public sealed record CustomerInsightDTO(int CustomerId, string Name, int Sales, decimal Total);

public sealed class InsightsDTO
{
    public string Period { get; set; } = "30days";
    public DateTime From { get; set; }
    public DateTime ToExclusive { get; set; }
    public int Days { get; set; }
    public PeriodMetricsDTO Now { get; set; } = new();
    public PeriodMetricsDTO Before { get; set; } = new();
    public decimal AverageBasket { get; set; }
    public decimal AverageBasketBefore { get; set; }
    public decimal ItemsPerSale { get; set; }
    public decimal MarginPercent { get; set; }
    public decimal MarginPercentBefore { get; set; }
    public decimal RefundRatePercent { get; set; }
    public List<DayPointDTO> Daily { get; set; } = new();
    /// <summary>Bill totals by day (up to 30 days), week (90 days) or month (a year), for the chart.</summary>
    public SwiftCheckoutPOS.BLL.DTOs.SalesTrendDTO Trend { get; set; } = new();
    public List<HeatCellDTO> Heat { get; set; } = new();
    public int FirstHour { get; set; } = 8;
    public int LastHour { get; set; } = 21;
    public HeatCellDTO? BusiestSlot { get; set; }
    public DayPointDTO? BestDay { get; set; }
    public List<ShareDTO> Tenders { get; set; } = new();
    public List<CategoryInsightDTO> Categories { get; set; } = new();
    public List<ProductInsightDTO> TopByRevenue { get; set; } = new();
    public List<ProductInsightDTO> TopByProfit { get; set; } = new();
    public List<ProductInsightDTO> AllProducts { get; set; } = new();
    public List<CashierInsightDTO> Cashiers { get; set; } = new();
    public List<CustomerInsightDTO> Customers { get; set; } = new();
    public decimal WalkInTotal { get; set; }
    public decimal RegisteredTotal { get; set; }
}

// ---- Stock health page -----------------------------------------------------------------------------------------
public sealed class StockLineDTO
{
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Barcode { get; set; } = "";
    public string Category { get; set; } = "";
    public int CategoryId { get; set; }
    public string Vendor { get; set; } = "";
    public string Unit { get; set; } = "pcs";
    public int Available { get; set; }
    public int Reserved { get; set; }
    public int ReorderLevel { get; set; }
    public int Sold30 { get; set; }
    public decimal PerDay { get; set; }
    /// <summary>Days the available stock lasts at the last 30 days' pace; null when nothing sold.</summary>
    public int? DaysOfCover { get; set; }
    public int SuggestedOrder { get; set; }
    public DateTime? LastSold { get; set; }
    public decimal ValueAtCost { get; set; }
    public decimal ValueAtRetail { get; set; }
    /// <summary>Out | Reorder | Low | Slow | Healthy.</summary>
    public string Status { get; set; } = "Healthy";
}

public sealed class StockHealthDTO
{
    public List<StockLineDTO> Lines { get; set; } = new();
    public int Products { get; set; }
    public int Units { get; set; }
    public decimal ValueAtCost { get; set; }
    public decimal ValueAtRetail { get; set; }
    public decimal SlowValueAtCost { get; set; }
    public int Out => Lines.Count(l => l.Status == "Out");
    public int Reorder => Lines.Count(l => l.Status == "Reorder");
    public int Low => Lines.Count(l => l.Status == "Low");
    public int Slow => Lines.Count(l => l.Status == "Slow");
    public List<ShareDTO> ValueByCategory { get; set; } = new();
}

public sealed record AlertDTO(string Level, string Icon, string Text, string Controller, string Action = "Index");
public sealed record SearchHitDTO(string Group, string Title, string Detail, string Url, string Icon);

/// <summary>Analytics for the owner: what sells, when, who sells it and what to reorder. Read-only.</summary>
public sealed class InsightsService(InsightsRepository insights, MetricsService metrics)
{
    /// <summary>How many days of sales a suggested order should cover.</summary>
    public const int CoverTargetDays = 14;
    /// <summary>Stock with fewer days of cover than this is flagged "Reorder" even above its reorder level.</summary>
    public const int ReorderWithinDays = 7;
    public const int VelocityDays = 30;

    public static readonly (string Key, string Text, int Days)[] Periods =
        [("7days", "7 days", 7), ("30days", "30 days", 30), ("90days", "90 days", 90), ("1year", "1 year", 365)];

    public InsightsDTO Get(string? period, DateTime? now = null)
    {
        var chosen = Periods.FirstOrDefault(p => p.Key == period);
        if (chosen.Key is null) chosen = Periods[1];
        var today = (now ?? DateTime.Now).Date;
        var to = today.AddDays(1);
        var from = to.AddDays(-chosen.Days);
        var dto = new InsightsDTO { Period = chosen.Key, From = from, ToExclusive = to, Days = chosen.Days };

        dto.Now = metrics.Period(from, to);
        dto.Before = metrics.Period(from.AddDays(-chosen.Days), from);
        dto.AverageBasket = dto.Now.SalesCount == 0 ? 0 : Money.R2(dto.Now.GrossSalesInclVat / dto.Now.SalesCount);
        dto.AverageBasketBefore = dto.Before.SalesCount == 0 ? 0 : Money.R2(dto.Before.GrossSalesInclVat / dto.Before.SalesCount);
        dto.ItemsPerSale = dto.Now.SalesCount == 0 ? 0 : Math.Round((decimal)dto.Now.ItemsSold / dto.Now.SalesCount, 1);
        dto.MarginPercent = Percent(dto.Now.GrossProfit, dto.Now.NetSales);
        dto.MarginPercentBefore = Percent(dto.Before.GrossProfit, dto.Before.NetSales);
        dto.RefundRatePercent = dto.Now.SalesCount == 0 ? 0 : Math.Round((decimal)dto.Now.RefundCount / dto.Now.SalesCount * 100m, 1);
        dto.Tenders = dto.Now.Tenders.Where(t => t.Value != 0).OrderByDescending(t => t.Value).Select(t => new ShareDTO(t.Key, t.Value)).ToList();

        var sales = insights.Sales(from, to);
        dto.Daily = Daily(sales.Select(s => (s.SaleDate, s.Total)), from, to);
        dto.Trend = SalesTrend.Build(sales.Select(s => (s.SaleDate, s.Total)).ToList(), from, now ?? DateTime.Now, chosen.Days <= 30 ? "day" : chosen.Days <= 90 ? "week" : "month");
        dto.BestDay = dto.Daily.Where(d => d.Sales > 0).OrderByDescending(d => d.Total).FirstOrDefault();
        dto.Heat = Heat(sales.Select(s => (s.SaleDate, s.Total)));
        if (dto.Heat.Count > 0)
        {
            dto.FirstHour = Math.Min(8, dto.Heat.Min(h => h.Hour));
            dto.LastHour = Math.Max(21, dto.Heat.Max(h => h.Hour));
            dto.BusiestSlot = dto.Heat.OrderByDescending(h => h.Total).First();
        }
        dto.Cashiers = sales.GroupBy(s => string.IsNullOrWhiteSpace(s.Cashier) ? "Unknown" : s.Cashier!)
            .Select(g => new CashierInsightDTO(g.Key, g.Count(), g.Sum(s => s.Total))).OrderByDescending(c => c.Total).ToList();
        dto.WalkInTotal = sales.Where(s => s.CustomerId is null).Sum(s => s.Total);
        dto.RegisteredTotal = sales.Where(s => s.CustomerId is not null).Sum(s => s.Total);
        var byCustomer = sales.Where(s => s.CustomerId is not null).GroupBy(s => s.CustomerId!.Value)
            .Select(g => (Id: g.Key, Sales: g.Count(), Total: g.Sum(s => s.Total))).OrderByDescending(c => c.Total).Take(8).ToList();
        var names = insights.CustomerNames(byCustomer.Select(c => c.Id));
        dto.Customers = byCustomer.Select(c => new CustomerInsightDTO(c.Id, names.GetValueOrDefault(c.Id, "Customer"), c.Sales, c.Total)).ToList();

        var products = insights.Products().ToDictionary(p => p.ProductId);
        dto.AllProducts = insights.ProductSales(from, to).Select(r =>
        {
            products.TryGetValue(r.ProductId, out var p);
            // Profit only where the cost at the time of sale is known for every unit (PR-Q38).
            decimal? profit = r.UnknownCostQuantity == 0 ? Money.R2(r.Revenue - r.Cost) : null;
            return new ProductInsightDTO(r.ProductId, p?.Name ?? "Deleted product", p?.Category ?? "Other", p?.CategoryId ?? 0, r.Quantity, Money.R2(r.Revenue), profit);
        }).OrderByDescending(p => p.Revenue).ToList();
        dto.TopByRevenue = dto.AllProducts.Take(10).ToList();
        dto.TopByProfit = dto.AllProducts.Where(p => p.Profit is not null).OrderByDescending(p => p.Profit).Take(10).ToList();
        dto.Categories = dto.AllProducts.GroupBy(p => (p.CategoryId, p.Category))
            .Select(g => new CategoryInsightDTO(g.Key.CategoryId, g.Key.Category, g.Sum(p => p.Units), g.Sum(p => p.Revenue),
                g.All(p => p.Profit is not null) ? g.Sum(p => p.Profit) : null))
            .OrderByDescending(c => c.Revenue).ToList();
        return dto;
    }

    private static decimal Percent(decimal part, decimal whole) => whole <= 0 ? 0 : Math.Round(part / whole * 100m, 1);

    /// <summary>One point per calendar day in [from, to), including days with no sales.</summary>
    public static List<DayPointDTO> Daily(IEnumerable<(DateTime When, decimal Total)> sales, DateTime from, DateTime to)
    {
        var byDay = sales.GroupBy(s => s.When.Date).ToDictionary(g => g.Key, g => (Total: g.Sum(s => s.Total), Count: g.Count()));
        var points = new List<DayPointDTO>();
        for (var d = from.Date; d < to; d = d.AddDays(1))
            points.Add(byDay.TryGetValue(d, out var v) ? new DayPointDTO(d, v.Total, v.Count) : new DayPointDTO(d, 0, 0));
        return points;
    }

    /// <summary>Sales by weekday and hour of the day (only the cells that have sales).</summary>
    public static List<HeatCellDTO> Heat(IEnumerable<(DateTime When, decimal Total)> sales) =>
        sales.GroupBy(s => (s.When.DayOfWeek, s.When.Hour))
            .Select(g => new HeatCellDTO(g.Key.DayOfWeek, g.Key.Hour, g.Sum(s => s.Total), g.Count())).ToList();

    public StockHealthDTO StockHealth(DateTime? now = null)
    {
        var today = (now ?? DateTime.Now).Date;
        var from = today.AddDays(1 - VelocityDays); var to = today.AddDays(1);
        var sold = insights.ProductSales(from, to).ToDictionary(r => r.ProductId, r => r.Quantity);
        var refunded = insights.RefundedUnits(from, to);
        var last = insights.LastSold();
        var lines = insights.Products().Where(p => p.IsActive).Select(p =>
        {
            var line = Assess(p.Stock - p.Reserved, p.ReorderLevel, Math.Max(0, sold.GetValueOrDefault(p.ProductId) - refunded.GetValueOrDefault(p.ProductId)));
            line.ProductId = p.ProductId; line.Name = p.Name; line.Barcode = p.Barcode; line.Category = p.Category; line.CategoryId = p.CategoryId;
            line.Vendor = p.Vendor; line.Unit = p.Unit; line.Reserved = p.Reserved;
            line.LastSold = last.TryGetValue(p.ProductId, out var lastSale) ? lastSale : null;
            line.ValueAtCost = Money.R2(p.Stock * p.CostPrice); line.ValueAtRetail = Money.R2(p.Stock * p.SellingPrice);
            return line;
        }).ToList();
        var order = new Dictionary<string, int> { ["Out"] = 0, ["Reorder"] = 1, ["Low"] = 2, ["Slow"] = 3, ["Healthy"] = 4 };
        return new StockHealthDTO
        {
            Lines = lines.OrderBy(l => order[l.Status]).ThenBy(l => l.DaysOfCover ?? int.MaxValue).ThenBy(l => l.Name).ToList(),
            Products = lines.Count, Units = lines.Sum(l => l.Available + l.Reserved),
            ValueAtCost = lines.Sum(l => l.ValueAtCost), ValueAtRetail = lines.Sum(l => l.ValueAtRetail),
            SlowValueAtCost = lines.Where(l => l.Status == "Slow").Sum(l => l.ValueAtCost),
            ValueByCategory = lines.GroupBy(l => (l.CategoryId, l.Category)).Select(g => new ShareDTO(g.Key.Category, g.Sum(l => l.ValueAtCost), g.Key.CategoryId))
                .Where(s => s.Amount > 0).OrderByDescending(s => s.Amount).ToList()
        };
    }

    /// <summary>
    /// The reorder rule, on its own so it can be tested: pace = units sold in the last 30 days / 30.
    /// Out: nothing to sell. Reorder: at or under the reorder level, or under 7 days of cover.
    /// Low: under 14 days of cover. Slow: stock on the shelf and nothing sold in 30 days.
    /// The suggested order brings stock up to 14 days of sales plus the reorder level.
    /// </summary>
    public static StockLineDTO Assess(int available, int reorderLevel, int sold30)
    {
        var perDay = sold30 / (decimal)VelocityDays;
        int? cover = sold30 > 0 ? (int)Math.Floor(Math.Max(available, 0) / perDay) : null;
        var status = available <= 0 ? "Out"
            : available <= reorderLevel || cover < ReorderWithinDays ? "Reorder"
            : cover < CoverTargetDays ? "Low"
            : sold30 == 0 ? "Slow" : "Healthy";
        var target = (int)Math.Ceiling(perDay * CoverTargetDays) + reorderLevel;
        return new StockLineDTO
        {
            Available = available, ReorderLevel = reorderLevel, Sold30 = sold30, PerDay = Math.Round(perDay, 2), DaysOfCover = cover, Status = status,
            SuggestedOrder = status is "Out" or "Reorder" or "Low" ? Math.Max(0, target - Math.Max(available, 0)) : 0
        };
    }

    // ---- alerts bell ---------------------------------------------------------------------------------------------
    public List<AlertDTO> Alerts(bool manager, bool admin, bool canViewFinancials, bool shiftOpen, int heldRetentionHours, IEnumerable<string>? backupWarnings = null, DateTime? now = null)
    {
        var alerts = new List<AlertDTO>();
        if (!shiftOpen) alerts.Add(new("warn", "shift", "No till shift is open. Payments need an open shift.", "Shift"));
        if (admin && backupWarnings is not null) alerts.AddRange(backupWarnings.Select(w => new AlertDTO("bad", "backup", w, "BackupAdmin")));
        if (manager)
        {
            // Stock health shows units sold, which is private from a Manager without the financial view (CQ-15).
            var (stockPage, stockAction) = canViewFinancials ? ("Insights", "Stock") : ("InventoryAdmin", "Index");
            var outOf = insights.OutOfStockCount(); var low = insights.LowStockCount();
            if (outOf > 0) alerts.Add(new("bad", "package-x", outOf == 1 ? "1 product is out of stock." : $"{outOf} products are out of stock.", stockPage, stockAction));
            if (low > 0) alerts.Add(new("warn", "products", low == 1 ? "1 product is at or below its reorder level." : $"{low} products are at or below their reorder level.", stockPage, stockAction));
        }
        var held = insights.HeldOrders();
        if (held > 0)
        {
            var oldest = insights.OldestHeldAt();
            var left = oldest is null ? (double?)null : heldRetentionHours - ((now ?? DateTime.Now) - oldest.Value).TotalHours;
            var soon = left is < 12.0 ? " The oldest is released in under 12 hours." : "";
            alerts.Add(new(left is < 12.0 ? "warn" : "info", "pause", (held == 1 ? "1 order is on hold." : $"{held} orders are on hold.") + soon, "Checkout"));
        }
        return alerts;
    }

    // ---- search box ----------------------------------------------------------------------------------------------
    public List<SearchHitDTO> Search(string? term, bool manager, bool canViewFinancials)
    {
        term = term?.Trim() ?? "";
        var hits = new List<SearchHitDTO>();
        if (term.Length < 2) return hits;
        foreach (var p in insights.FindProducts(term, 6))
            hits.Add(new("Products", p.Name, $"{p.Barcode} · ৳{p.SellingPrice:N2} · {p.Stock - p.Reserved} {p.Unit} available" + (p.IsActive ? "" : " · not for sale"),
                manager ? $"/InventoryAdmin/Edit/{p.ProductId}" : "/Checkout/Index", "products"));
        if (!manager) return hits;
        foreach (var c in insights.FindCustomers(term, 5))
            hits.Add(new("Customers", c.Name, c.Phone + (c.Due > 0 ? $" · owes ৳{c.Due:N2}" : ""), $"/Customer/Details/{c.CustomerId}", "customers"));
        foreach (var r in insights.FindReceipts(term, 5))
            hits.Add(new("Receipts", r.Receipt, $"{r.SaleDate:dd MMM yyyy, HH:mm} · {(canViewFinancials ? $"৳{r.Total:N2} · " : "")}{(r.Status == "PartiallyRefunded" ? "Part refunded" : r.Status)}",
                canViewFinancials ? $"/ReportAdmin/Index?startDate={r.SaleDate:yyyy-MM-dd}&endDate={r.SaleDate:yyyy-MM-dd}&searchQuery={Uri.EscapeDataString(r.Receipt)}" : "/Refund/Index", "receipt"));
        return hits;
    }

    // ---- exports -------------------------------------------------------------------------------------------------
    public byte[] SalesCsv(DateTime from, DateTime to) =>
        Csv.Write(["Receipt", "Date", "Status", "Customer", "Cashier", "Payment", "Subtotal", "VAT", "Total", "Cash rounding", "Refunded"],
            insights.SalesForExport(from, to, 50_000).Select(s => new object?[]
                { s.Receipt, s.SaleDate, s.Status, s.Customer ?? "Walk-in", Person(s.Cashier), s.Payment, s.SubTotal, s.Vat, s.Total, s.Rounding, s.Refunded }));

    public byte[] ProductsCsv(bool showCost)
    {
        var headers = new List<string> { "Barcode", "Product", "Category", "Supplier", "Unit", "Price", "In stock", "Held", "Reorder level", "For sale" };
        if (showCost) headers.InsertRange(5, ["Cost"]);
        return Csv.Write(headers, insights.Products().Select(p =>
        {
            var row = new List<object?> { p.Barcode, p.Name, p.Category, p.Vendor, p.Unit, p.SellingPrice, p.Stock, p.Reserved, p.ReorderLevel, p.IsActive ? "Yes" : "No" };
            if (showCost) row.Insert(5, p.CostPrice);
            return row;
        }));
    }

    public byte[] ReorderCsv() =>
        Csv.Write(["Product", "Barcode", "Supplier", "Status", "Available", "Reorder level", "Sold in 30 days", "Days of stock left", "Suggested order", "Unit"],
            StockHealth().Lines.Where(l => l.SuggestedOrder > 0).OrderBy(l => l.Vendor).ThenBy(l => l.Name).Select(l => new object?[]
                { l.Name, l.Barcode, l.Vendor, l.Status == "Out" ? "Out of stock" : l.Status == "Reorder" ? "Reorder now" : "Running low", l.Available, l.ReorderLevel,
                  l.Sold30, l.DaysOfCover, l.SuggestedOrder, l.Unit }));

    public byte[] ProductSalesCsv(InsightsDTO dto, bool showProfit)
    {
        var headers = new List<string> { "Product", "Category", "Units sold", "Sales" };
        if (showProfit) headers.AddRange(["Profit", "Margin %"]);
        return Csv.Write(headers, dto.AllProducts.Select(p =>
        {
            var row = new List<object?> { p.Name, p.Category, p.Units, p.Revenue };
            if (showProfit) { row.Add(p.Profit); row.Add(p.MarginPercent); }
            return row;
        }));
    }

    private static string Person(string? actor) =>
        string.IsNullOrWhiteSpace(actor) ? "" : System.Text.RegularExpressions.Regex.Replace(actor, @"^User \d+: ", "");
}
