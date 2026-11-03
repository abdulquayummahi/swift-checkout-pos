// Project: SwiftCheckoutPOS.UI
// File: Controllers/ReportController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SwiftCheckoutPOS.UI.Controllers
{
    /// <summary>
    /// Retired (BP-07): the old Z-report page and the till count that always used user 1 are replaced by
    /// ReportAdmin (financial view policy) and Shift (blind close, BP-19). Old bookmarks are redirected.
    /// </summary>
    [Authorize]
    public class ReportController : Controller
    {
        [HttpGet]
        public IActionResult ZReport() => RedirectToAction("Index", "ReportAdmin");

        [HttpGet]
        public IActionResult TillCount() => RedirectToAction("Index", "Shift");

        [HttpPost]
        public IActionResult SubmitTillCount() => RedirectToAction("Index", "Shift");
    }
}
