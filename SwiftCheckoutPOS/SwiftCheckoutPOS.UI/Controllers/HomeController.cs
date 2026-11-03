using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SwiftCheckoutPOS.UI.Models;
using System.Diagnostics;

namespace SwiftCheckoutPOS.UI.Controllers
{
    public class HomeController : Controller
    {
        // The old template pages are retired: signed-in staff go straight to the till.
        public IActionResult Index() => RedirectToAction("Index", "Checkout");

        public IActionResult Privacy() => RedirectToAction("Index", "Checkout");

        /// <summary>BP-07: production error page. Shows a reference number only, never exception details.</summary>
        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? code)
        {
            Response.StatusCode = code is >= 400 and < 600 ? code.Value : StatusCodes.Status500InternalServerError;
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier, StatusCode = Response.StatusCode });
        }
    }
}
