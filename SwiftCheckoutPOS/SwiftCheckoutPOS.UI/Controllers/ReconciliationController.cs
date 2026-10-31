// Project: SwiftCheckoutPOS.UI
// File: Controllers/ReconciliationController.cs
using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Services;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class ReconciliationController : Controller
    {
        private readonly TillReconciliationService _reconciliationService;

        public ReconciliationController(TillReconciliationService reconciliationService)
        {
            _reconciliationService = reconciliationService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var shiftData = _reconciliationService.GenerateCurrentShiftPreview();
            return View(shiftData);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CloseTill(decimal countedCash, string notes)
        {
            try
            {
                // In a production environment with proper Auth implementation, extract ID from Claims
                // Hardcoded to Admin ID 1 for now until User entity is fully mapped to Claims
                int userId = 1;

                _reconciliationService.CommitReconciliation(userId, countedCash, notes);

                TempData["Success"] = "Till successfully reconciled and Z-Report committed.";
                return RedirectToAction("Index", "AdminDashboard");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Reconciliation Fault: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}