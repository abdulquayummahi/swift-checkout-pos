// Project: SwiftCheckoutPOS.UI
// File: Controllers/ReportController.cs
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Services;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [Authorize(Roles = "Admin")] // Adjust role as needed (e.g., "Manager")
    public class ReportController : Controller
    {
        private readonly ReportService _reportService;
        private readonly ReconciliationService _reconciliationService;

        public ReportController(ReportService reportService, ReconciliationService reconciliationService)
        {
            _reportService = reportService;
            _reconciliationService = reconciliationService;
        }

        // --- Existing Z-Report Endpoint ---
        [HttpGet]
        public IActionResult ZReport(DateTime? date)
        {
            DateTime reportDate = date ?? DateTime.Today;
            var reportDto = _reportService.GenerateDailyZReport(reportDate);
            return View(reportDto);
        }

        // --- NEW: Blind Till Count Endpoints ---
        [HttpGet]
        public IActionResult TillCount()
        {
            return View(); // Returns the Blind Count form we built previously
        }

        [HttpPost]
        public IActionResult SubmitTillCount(decimal countedAmount, string notes)
        {
            // In a production scenario, extract this from User.Claims. 
            // We will use ID 1 (Master Admin) for this implementation.
            int currentUserId = 1;

            // The BLL handles the secure calculation so the UI never sees the expected amount until after submission
            var reconciliationResult = _reconciliationService.PerformBlindReconciliation(currentUserId, countedAmount, notes ?? string.Empty);

            return View("TillCountSummary", reconciliationResult);
        }
    }
}