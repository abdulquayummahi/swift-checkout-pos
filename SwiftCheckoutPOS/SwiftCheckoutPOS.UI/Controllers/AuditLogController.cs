using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.UI.Controllers;

/// <summary>BP-24: read-only view of sensitive actions (Admin only).</summary>
[Authorize(Roles = "Admin")]
public sealed class AuditLogController(AtomicUnit unit) : Controller
{
    public IActionResult Index(string? type) => View(unit.RecentAudit(300, string.IsNullOrWhiteSpace(type) ? null : type));
}
