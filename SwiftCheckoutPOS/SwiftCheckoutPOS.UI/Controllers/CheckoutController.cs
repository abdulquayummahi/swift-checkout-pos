using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Interfaces;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.UI.Models;
using System.Security.Claims;

namespace SwiftCheckoutPOS.UI.Controllers;

[Authorize]
public class CheckoutController(
    CheckoutService checkoutService,
    Func<IReceiptPrinter> printerFactory,
    ILogger<CheckoutController> logger) : Controller
{
    private const string ActiveCartKey = "ActiveCart";

    [HttpGet]
    public IActionResult Index() => View(new CheckoutViewModel
    {
        AvailableProducts = checkoutService.GetAvailableProducts(),
        Cart = GetActiveSession(),
        HeldOrders = checkoutService.GetHeldOrders()
    });

    [HttpPost]
    public IActionResult ScanBarcode(string barcode) =>
        ChangeCart(cart => checkoutService.AddToCartByBarcode(cart, barcode));

    [HttpPost]
    public IActionResult AddById(int productId) =>
        ChangeCart(cart => checkoutService.AddToCart(cart, productId));

    [HttpPost]
    public IActionResult UpdateQuantity(int productId, int change) =>
        ChangeCart(cart => checkoutService.UpdateQuantity(cart, productId, change));

    [HttpPost]
    public IActionResult RemoveItem(int productId) =>
        ChangeCart(cart => checkoutService.RemoveItem(cart, productId));

    [HttpPost]
    public IActionResult SetCustomerType(string type) =>
        ChangeCart(cart => checkoutService.SetCustomerType(cart, type));

    [HttpPost]
    public IActionResult ApplyBillDiscount(decimal discountValue, string discountType) =>
        ChangeCart(cart => checkoutService.ApplyBillDiscount(cart, discountValue, discountType));

    [HttpPost]
    public IActionResult LookupLoyaltyCustomer(string phone)
    {
        if (!ModelState.IsValid) return Json(new { success = false, message = "Check the phone number." });
        try
        {
            var cart = GetActiveSession();
            EnsureEditable(cart);
            var customer = checkoutService.LinkCustomer(cart, phone);
            cart.Revision = Guid.NewGuid().ToString("N");
            SaveActiveSession(cart);
            return Json(new { success = true, name = customer.FullName, points = customer.LoyaltyPoints });
        }
        catch (CheckoutValidationException ex) { return Json(new { success = false, message = ex.Message }); }
        catch (Exception ex)
        {
            LogFailure(ex);
            return Json(new { success = false, message = "Customer lookup is unavailable. Try again." });
        }
    }

    [HttpPost]
    public IActionResult HoldOrder()
    {
        try
        {
            if (!ModelState.IsValid) throw new CheckoutValidationException("Check the submitted values.");
            var cart = GetActiveSession();
            EnsureEditable(cart);
            checkoutService.HoldOrder(cart, Cashier());
            TempData["Success"] = $"Order {cart.InvoiceNumber} is saved.";
            ClearSavedSession(); // Failure here cannot undo the committed reservation.
        }
        catch (Exception ex) { ReportFailure(ex); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult ResumeOrder(int saleId)
    {
        try
        {
            if (!ModelState.IsValid) throw new CheckoutValidationException("Choose a valid order.");
            var active = GetActiveSession();
            EnsureEditable(active);
            if (active.PersistedSaleId == saleId)
            {
                TempData["Success"] = "This order is already active. Its current cart edits have been kept.";
                return RedirectToAction(nameof(Index));
            }
            // Preserve the active cart FIRST. A failure must not strand the selected order.
            if (active.Items.Count > 0 && active.PersistedSaleId != saleId)
            {
                checkoutService.HoldOrder(active, Cashier());
                ClearSavedSession();
            }
            var resumed = checkoutService.ResumeOrder(saleId);
            SaveActiveSession(resumed);
            TempData["Success"] = $"Resumed order {resumed.InvoiceNumber}.";
        }
        catch (Exception ex) { ReportFailure(ex); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public IActionResult ClearCart()
    {
        try
        {
            var cart = GetActiveSession();
            EnsureEditable(cart);
            checkoutService.CancelCart(cart);
            ClearSavedSession();
        }
        catch (Exception ex) { ReportFailure(ex); }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult GetInvoicePreview()
    {
        try
        {
            var cart = GetActiveSession();
            if (cart.Items.Count == 0) return Json(new { success = false, message = "Add items first." });
            return Json(new { success = true, preview = printerFactory().GenerateReceiptPreview(cart) });
        }
        catch (Exception ex)
        {
            LogFailure(ex);
            return Json(new { success = false, message = "Receipt preview is unavailable. Check this order's status before confirming payment." });
        }
    }

    [HttpPost]
    public IActionResult TriggerHardwarePrint() => Json(new
    {
        success = false,
        message = "Checkout prints only after the sale is saved. Confirm payment to finish this order."
    });

    [HttpPost]
    public IActionResult Finalize(string paymentMethod, string invoiceNumber, string cartVersion)
    {
        CheckoutSessionDTO? pendingCart = null;
        var databaseAttempted = false;
        try
        {
            if (!ModelState.IsValid) throw new CheckoutValidationException("Choose a valid payment method and order.");
            var cart = GetActiveSession();
            var actor = Cashier();
            // A stale form must never complete the next cart after a previous POST cleared Session.
            if (!string.Equals(cart.InvoiceNumber, invoiceNumber, StringComparison.Ordinal))
            {
                if (checkoutService.IsCompleted(invoiceNumber, paymentMethod, actor))
                {
                    TempData["Success"] = $"Receipt {invoiceNumber} was already completed. No second sale was created.";
                    return RedirectToAction(nameof(Index));
                }
                throw new CheckoutValidationException("The cart changed. Reload checkout before confirming payment.");
            }

            if (cart.Revision != cartVersion)
                throw new CheckoutValidationException("The cart changed in another tab. Reload and review the total before paying.");
            if (cart.AwaitingConfirmation && cart.PendingPaymentMethod != paymentMethod)
                throw new CheckoutValidationException("Confirm the pending receipt with its original payment method before making changes.");

            cart.AwaitingConfirmation = true;
            cart.PendingPaymentMethod = paymentMethod;
            SaveActiveSession(cart); // Preserve identity/tender BEFORE attempting any database write.
            pendingCart = cart;
            databaseAttempted = true;

            var result = checkoutService.CompleteSale(cart, paymentMethod, actor);
            TempData["Success"] = $"Transaction complete. Receipt: {result.ReceiptNumber} | Payment: {paymentMethod}";
            ClearSavedSession();
            if (!result.AlreadyCompleted) PrintCommittedSale(result.Receipt);
        }
        catch (Exception ex)
        {
            if (databaseAttempted && pendingCart is not null &&
                (ex is CheckoutValidationException || ex is DbUpdateConcurrencyException))
            {
                // These failures have a known non-commit outcome for this attempt.
                pendingCart.AwaitingConfirmation = false;
                pendingCart.PendingPaymentMethod = null;
                try { SaveActiveSession(pendingCart); } catch (Exception sessionError) { LogFailure(sessionError); }
            }
            ReportFailure(ex);
        }
        return RedirectToAction(nameof(Index));
    }

    private void PrintCommittedSale(CheckoutSessionDTO receipt)
    {
        try
        {
            var hardwarePrinter = printerFactory();
            if (!hardwarePrinter.IsPrinterOnline()) throw new IOException("Printer unavailable.");
            hardwarePrinter.PrintReceipt(receipt);
            // Preserve existing drawer behavior; tender-specific policy remains PR-Q47.
            hardwarePrinter.OpenCashDrawer();
        }
        catch (Exception ex)
        {
            LogFailure(ex);
            TempData["Warning"] = $"Sale {receipt.InvoiceNumber} is saved. Printing or drawer delivery could not be confirmed; do not charge again.";
        }
    }

    private IActionResult ChangeCart(Action<CheckoutSessionDTO> change)
    {
        try
        {
            if (!ModelState.IsValid) throw new CheckoutValidationException("Check the submitted values.");
            var cart = GetActiveSession();
            EnsureEditable(cart);
            change(cart);
            cart.Revision = Guid.NewGuid().ToString("N");
            SaveActiveSession(cart);
        }
        catch (Exception ex) { ReportFailure(ex); }
        return RedirectToAction(nameof(Index));
    }

    private void ReportFailure(Exception exception)
    {
        TempData["Error"] = exception switch
        {
            CheckoutValidationException validation => validation.Message,
            DbUpdateConcurrencyException => "The stock or order changed. Reload and review it before retrying.",
            _ => "The operation could not be confirmed. Keep this receipt and retry the same order; do not charge again until its status is checked."
        };
        if (exception is not CheckoutValidationException) LogFailure(exception);
    }

    private static void EnsureEditable(CheckoutSessionDTO cart)
    {
        if (cart.AwaitingConfirmation)
            throw new CheckoutValidationException("This receipt has an unconfirmed payment outcome. Retry the same receipt and payment method before changing or clearing the cart.");
    }

    private void LogFailure(Exception exception)
    {
        // Never include database exception text, request bodies or customer data.
        try { logger.LogError("Checkout component failed ({ErrorType}).", exception.GetType().Name); }
        catch { /* Diagnostic sink failure cannot change a committed financial outcome. */ }
    }

    private string Cashier()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(id, out var userId) || userId <= 0)
            throw new CheckoutValidationException("Sign in again before processing this order.");
        var actor = $"User {userId}: {User.Identity?.Name}";
        return actor.Length <= 100 ? actor : actor[..100]; // Stable ID is always retained.
    }

    private CheckoutSessionDTO GetActiveSession()
    {
        var value = HttpContext.Session.GetString(ActiveCartKey);
        if (value is not null)
            return JsonConvert.DeserializeObject<CheckoutSessionDTO>(value)
                ?? throw new CheckoutValidationException("The cart is unavailable. Reload checkout.");
        var cart = new CheckoutSessionDTO();
        SaveActiveSession(cart); // Establish its stable receipt before displaying a form.
        return cart;
    }

    private void SaveActiveSession(CheckoutSessionDTO cart) =>
        HttpContext.Session.SetString(ActiveCartKey, JsonConvert.SerializeObject(cart));

    private void ClearSavedSession()
    {
        try { HttpContext.Session.Remove(ActiveCartKey); }
        catch (Exception ex)
        {
            LogFailure(ex);
            TempData["Warning"] = "The order is saved, but the screen could not be cleared. Reload and check this receipt before starting another sale.";
        }
    }
}
