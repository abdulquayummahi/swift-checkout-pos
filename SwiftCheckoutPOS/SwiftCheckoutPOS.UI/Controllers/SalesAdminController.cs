// Project: SwiftCheckoutPOS.UI
// File: Controllers/SalesAdminController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SwiftCheckoutPOS.UI.Controllers
{
    /// <summary>The sales list lives in Reports; this old route redirects there.</summary>
    [Authorize(Roles = "Admin")]
    public class SalesAdminController : Controller
    {
        public IActionResult Index() => RedirectToAction("Index", "ReportAdmin");
    }
}
