using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.UI.Infrastructure;

namespace SwiftCheckoutPOS.UI.Controllers;

/// <summary>Spreadsheet exports open to Managers. Cost prices are included only with the financial view (PR-Q92).</summary>
[Authorize(Roles = "Admin,Manager")]
public class ExportController(InsightsService insights, SettingsService settings, AuditService audit) : Controller
{
    [HttpGet]
    public IActionResult Products()
    {
        var fileName = $"products-{DateTime.Today:yyyyMMdd}.csv";
        audit.Record(User.Actor(), "DataExported", "Export", "Products", details: fileName);
        return File(insights.ProductsCsv(showCost: User.CanViewFinancials(settings)), "text/csv; charset=utf-8", fileName);
    }
}
