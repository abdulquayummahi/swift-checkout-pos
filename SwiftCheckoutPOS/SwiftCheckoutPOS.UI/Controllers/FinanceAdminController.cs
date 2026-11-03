// Project: SwiftCheckoutPOS.UI
// File: Controllers/FinanceAdminController.cs
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.UI.Infrastructure;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class FinanceAdminController : Controller
    {
        private readonly FinanceService _financeService;
        private readonly SettingsService _settings;

        public FinanceAdminController(FinanceService financeService, SettingsService settings)
        {
            _financeService = financeService;
            _settings = settings;
        }

        [HttpGet]
        public IActionResult Index(DateTime? startDate, DateTime? endDate, string searchQuery)
        {
            // Default to current month for ledger to prevent loading years of data natively
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd") ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd");
            ViewBag.SearchQuery = searchQuery;

            // PR-Q92: without financial view a Manager gets only the expense form and their own entries today.
            if (!User.CanViewFinancials(_settings))
                return View("Expenses", _financeService.GetOwnEntriesToday(User.Actor()));
            try
            {
                var dashboard = _financeService.GetFinanceDashboard(startDate, endDate, searchQuery);
                return View(dashboard);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "The finance figures could not be loaded."; // never show raw exception text
                return View(new BLL.DTOs.FinanceDashboardDTO());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddEntry(string category, string? transactionType, decimal amount, string? description)
        {
            try
            {
                _financeService.RecordManualEntry(category, transactionType, amount, description, User.Actor(),
                    expensesOnly: !User.CanViewFinancials(_settings));
                TempData["Success"] = "Entry recorded.";
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                TempData["Error"] = this.SafeError(ex);
            }
            return RedirectToAction(nameof(Index));
        }
    }
}