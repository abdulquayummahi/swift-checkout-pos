using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SwiftCheckoutPOS.UI.Models;
using System.Diagnostics;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
