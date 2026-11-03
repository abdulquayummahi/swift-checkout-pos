// Project: SwiftCheckoutPOS.UI
// File: Controllers/SettingsAdminController.cs
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Services;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class SettingsAdminController : Controller
    {
        private readonly SettingsService _settingsService;

        public SettingsAdminController(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Fetch the current dynamic setting from the Tier 2 BLL
            ViewBag.RetentionHours = _settingsService.GetHeldOrderRetentionHours();
            ViewBag.IsAdmin = User.IsInRole("Admin");
            ViewBag.ManagerFinancialView = _settingsService.GetAllowManagerFinancialView();
            return View();
        }

        /// <summary>PR-Q92: Admin-only, audited.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public IActionResult SetManagerFinancialView(bool allow)
        {
            try
            {
                _settingsService.SetAllowManagerFinancialView(allow, SwiftCheckoutPOS.UI.Infrastructure.ActorExtensions.Actor(User), "Admin");
                TempData["Success"] = allow ? "Managers can now view financial reports." : "Financial reports are now Admin-only.";
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateRetention(int retentionHours)
        {
            try
            {
                if (retentionHours < 0) throw new ArgumentException("Retention hours cannot be negative.");

                _settingsService.SetHeldOrderRetentionHours(retentionHours);
                TempData["Success"] = $"System configuration updated. Terminal shift security window is now {retentionHours} hours.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Configuration Fault: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}