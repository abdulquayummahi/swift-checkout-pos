using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Services;

namespace SwiftCheckoutPOS.UI.Controllers;

/// <summary>BP-24: read-only view of sensitive actions (Admin only).</summary>
[Authorize(Roles = "Admin")]
public sealed class AuditLogController(AuditService audit) : Controller
{
    public IActionResult Index(string? type) => View(audit.Recent(300, type));
}
