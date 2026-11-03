// Project: SwiftCheckoutPOS.UI
// File: Controllers/RefundController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.UI.Infrastructure;

namespace SwiftCheckoutPOS.UI.Controllers
{
    /// <summary>PR-Q27/Q87: refunds and voids need a Manager or Admin.</summary>
    [Authorize(Roles = "Admin,Manager")]
    public class RefundController : Controller
    {
        private readonly RefundService _refundService;

        public RefundController(RefundService refundService)
        {
            _refundService = refundService;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Lookup(string receiptNumber)
        {
            try
            {
                return View("Index", _refundService.LookupTransaction(receiptNumber));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                TempData["Error"] = this.SafeError(ex);
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmRefund(string receiptNumber, List<RefundRequestLineDTO> lines, string? reason)
        {
            try
            {
                var result = _refundService.ExecuteRefund(receiptNumber, lines ?? new(), User.Actor(), reason);
                TempData["Success"] = $"Refund of {receiptNumber} saved: {SwiftCheckoutPOS.BLL.Pricing.Money.Bdt(result.Amount)}."
                    + (result.DueCancelled > 0 ? $" Due cancelled: {SwiftCheckoutPOS.BLL.Pricing.Money.Bdt(result.DueCancelled)}." : "")
                    + (result.Payouts.Count > 0 ? $" Pay back: {result.Summary}." : "");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                TempData["Error"] = $"Refund failed: {this.SafeError(ex)}";
                return Reload(receiptNumber);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult VoidSale(string receiptNumber, string reason)
        {
            try
            {
                var result = _refundService.VoidSale(receiptNumber, reason, User.Actor());
                TempData["Success"] = $"Sale {receiptNumber} voided." + (result.Payouts.Count > 0 ? $" Give back: {result.Summary}." : "");
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                TempData["Error"] = $"Void failed: {this.SafeError(ex)}";
                return Reload(receiptNumber);
            }
        }

        private IActionResult Reload(string receiptNumber)
        {
            try { return View("Index", _refundService.LookupTransaction(receiptNumber)); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return RedirectToAction(nameof(Index)); }
        }
    }
}
