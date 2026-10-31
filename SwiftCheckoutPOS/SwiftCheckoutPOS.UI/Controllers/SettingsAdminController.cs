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
            return View();
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