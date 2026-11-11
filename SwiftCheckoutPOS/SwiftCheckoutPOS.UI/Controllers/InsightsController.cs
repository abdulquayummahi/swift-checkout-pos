using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.UI.Infrastructure;

namespace SwiftCheckoutPOS.UI.Controllers;

/// <summary>
/// Owner analytics: what sells, when, who sells it, and what to reorder. Everything here shows period sales
/// figures or quantities, so the whole controller needs the financial view (PR-Q92, CQ-15).
/// </summary>
[Authorize(Roles = "Admin,Manager")]
[Authorize(Policy = FinancialViewRequirement.Policy)]
public class InsightsController(InsightsService insights, AuditService audit) : Controller
{
    [HttpGet]
    public IActionResult Index(string? period) => View(insights.Get(period));

    [HttpGet]
    public IActionResult Stock() => View(insights.StockHealth());

    [HttpGet]
    public IActionResult ExportProductSales(string? period)
    {
        var dto = insights.Get(period);
        return Download(insights.ProductSalesCsv(dto, showProfit: true), $"product-sales-{dto.From:yyyyMMdd}-{dto.ToExclusive.AddDays(-1):yyyyMMdd}.csv", "Product sales");
    }

    [HttpGet]
    public IActionResult ExportReorder() => Download(insights.ReorderCsv(), $"reorder-list-{DateTime.Today:yyyyMMdd}.csv", "Reorder list");

    [HttpGet]
    public IActionResult ExportSales(DateTime? startDate, DateTime? endDate)
    {
        var (from, to) = MetricsService.Days(startDate, endDate);
        return Download(insights.SalesCsv(from, to), $"sales-{from:yyyyMMdd}-{to.AddDays(-1):yyyyMMdd}.csv", "Sales");
    }

    /// <summary>Exports leave the program, so each one is written to the activity log.</summary>
    private FileContentResult Download(byte[] csv, string fileName, string what)
    {
        audit.Record(User.Actor(), "DataExported", "Export", what, details: fileName);
        return File(csv, "text/csv; charset=utf-8", fileName);
    }
}
