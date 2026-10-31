// Project: SwiftCheckoutPOS.UI
// File: Controllers/RefundController.cs
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Services;

namespace SwiftCheckoutPOS.UI.Controllers
{
    // ARCHITECT DIRECTIVE: Hard authorization lock. Cashiers cannot reverse financial ledgers.
    [Authorize(Roles = "Admin,Manager")]
    public class RefundController : Controller
    {
        private readonly RefundService _refundService;

        public RefundController(RefundService refundService)
        {
            _refundService = refundService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Lookup(string receiptNumber)
        {
            try
            {
                var saleData = _refundService.LookupTransaction(receiptNumber);
                return View("Index", saleData);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmRefund(string receiptNumber)
        {
            try
            {
                _refundService.ExecuteRefund(receiptNumber);
                TempData["Success"] = $"Transaction [{receiptNumber}] was successfully refunded. Stock levels have been restored.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Refund Failed: {ex.Message}";
                // Reload the view with the data intact so the operator sees the error context
                return View("Index", _refundService.LookupTransaction(receiptNumber));
            }
        }
    }
}