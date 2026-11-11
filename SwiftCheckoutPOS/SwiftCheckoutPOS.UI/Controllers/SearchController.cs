using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.UI.Infrastructure;

namespace SwiftCheckoutPOS.UI.Controllers;

/// <summary>The search box (Ctrl+K): products for everyone signed in; customers and receipts for Managers and Admins.</summary>
public class SearchController(InsightsService insights, SettingsService settings) : Controller
{
    [HttpGet]
    public IActionResult Find(string? q)
    {
        var role = User.RoleName();
        var hits = insights.Search(q, manager: role is "Admin" or "Manager", canViewFinancials: settings.CanViewFinancials(role));
        return Json(hits.Select(h => new { group = h.Group, title = h.Title, detail = h.Detail, url = h.Url, icon = h.Icon }));
    }
}
