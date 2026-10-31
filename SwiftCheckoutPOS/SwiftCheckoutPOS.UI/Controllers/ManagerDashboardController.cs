// Project: SwiftCheckoutPOS.UI
// File: Controllers/ManagerDashboardController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [Authorize(Roles = "Manager")]
    public class ManagerDashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}