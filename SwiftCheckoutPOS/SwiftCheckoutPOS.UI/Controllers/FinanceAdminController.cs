// Project: SwiftCheckoutPOS.UI
// File: Controllers/FinanceAdminController.cs
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Services;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class FinanceAdminController : Controller
    {
        private readonly FinanceService _financeService;

        public FinanceAdminController(FinanceService financeService)
        {
            _financeService = financeService;
        }

        [HttpGet]
        public IActionResult Index(DateTime? startDate, DateTime? endDate, string searchQuery)
        {
            // Default to current month for ledger to prevent loading years of data natively
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd") ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd");
            ViewBag.SearchQuery = searchQuery;

            try
            {
                var dashboard = _financeService.GetFinanceDashboard(startDate, endDate, searchQuery);
                return View(dashboard);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Ledger Fault: {ex.Message}";
                return View(new BLL.DTOs.FinanceDashboardDTO());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddEntry(string category, string transactionType, decimal amount, string description)
        {
            try
            {
                _financeService.RecordManualEntry(category, transactionType, amount, description, User.Identity?.Name ?? "Admin");
                TempData["Success"] = "Ledger entry recorded successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Transaction Fault: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}