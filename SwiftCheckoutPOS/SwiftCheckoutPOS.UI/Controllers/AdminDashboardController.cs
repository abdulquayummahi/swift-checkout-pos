// Project: SwiftCheckoutPOS.UI
// File: Controllers/AdminDashboardController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Services;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly DashboardService _dashboardService;

        public AdminDashboardController(DashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet]
        public IActionResult Index(string filter = "today")
        {
            var metrics = _dashboardService.GetMetrics(filter);

            // Pass the filter back to the view so the dropdown stays selected
            ViewBag.CurrentFilter = filter;

            return View(metrics);
        }
    }
}