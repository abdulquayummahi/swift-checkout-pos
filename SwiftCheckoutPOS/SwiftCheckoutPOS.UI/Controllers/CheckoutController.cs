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
    ILogger<CheckoutController> logger, CustomerService? customers = null) : Controller
{
    private const string ActiveCartKey = "ActiveCart";

    [HttpGet]
    public IActionResult Index()
    {
        var cart = GetActiveSession();
        return View(new CheckoutViewModel
        {
            AvailableProducts = checkoutService.GetAvailableProducts(),
            Cart = cart,
            HeldOrders = checkoutService.GetHeldOrders(),
            CustomerDue = cart.CustomerId is int id ? checkoutService.GetDueBalance(id) : 0
        });
    }

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
            return Json(new { success = true, name = customer.FullName, points = customer.LoyaltyPoints, due = customer.DueBalance });
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
    public IActionResult Finalize(string paymentMethod, string invoiceNumber, string cartVersion,
        decimal? cashTendered = null, string? reference = null, string? secondTender = null, decimal? secondAmount = null,
        string? secondReference = null, string? approverUsername = null, string? approverPassword = null)
    {
        // BP-17: up to two tenders. Cash (if used) takes the remainder; otherwise the first line does.
        var plan = new PaymentPlanDTO();
        plan.Lines.Add(new TenderLineDTO { Tender = paymentMethod ?? "", Reference = reference,
            Tendered = paymentMethod == "Cash" ? cashTendered : null });
        if (!string.IsNullOrWhiteSpace(secondTender))
            plan.Lines.Add(new TenderLineDTO { Tender = secondTender, Amount = secondTender == "Cash" ? null : secondAmount,
                Reference = secondReference, Tendered = secondTender == "Cash" ? cashTendered : null });
        if (plan.Lines.Count == 2 && plan.Lines[1].Tender == "Cash") plan.Lines[0].Amount = secondAmount; // cash is the remainder
        var approval = string.IsNullOrWhiteSpace(approverUsername) ? null
            : new ApprovalRequestDTO { Username = approverUsername, Password = approverPassword };
        paymentMethod = plan.Summary;
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

            var role = User.FindFirstValue(ClaimTypes.Role) ?? "Cashier";
            var result = checkoutService.CompleteSale(cart, plan, actor, role, approval);
            var change = result.Receipt.ReceiptPayments?.Sum(p => p.Change) ?? 0;
            TempData["Success"] = $"Transaction complete. Receipt: {result.ReceiptNumber} | Payment: {paymentMethod}"
                + (change > 0 ? $" | Change: {SwiftCheckoutPOS.BLL.Pricing.Money.Bdt(change)}" : "");
            ClearSavedSession();
            HttpContext.Session.SetString("LastReceipt", result.ReceiptNumber);
            if (!result.AlreadyCompleted) PrintCommittedSale(result.Receipt, plan.Lines.Any(l => l.Tender == "Cash"));
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

    /// <summary>PR-Q48/Q83: reprint any saved receipt (Cashier and Manager may); marked REPRINT and logged.</summary>
    [HttpPost]
    public IActionResult Reprint(string? receiptNumber, [FromServices] ReceiptService receipts)
    {
        try
        {
            var number = string.IsNullOrWhiteSpace(receiptNumber) ? HttpContext.Session.GetString("LastReceipt") : receiptNumber.Trim();
            if (string.IsNullOrEmpty(number)) throw new CheckoutValidationException("Enter the receipt number to reprint.");
            var copy = receipts.Reprint(number, Cashier());
            printerFactory().PrintReceipt(copy);
            TempData["Success"] = $"Receipt {copy.ReceiptNumber} reprinted (marked REPRINT / COPY).";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { TempData["Error"] = ex.Message; }
        catch (Exception ex) { LogFailure(ex); TempData["Error"] = "The printer did not respond. The sale is unchanged."; }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>PR-Q50: cashiers may add a customer at the till; it is linked to the current cart.</summary>
    [HttpPost]
    public IActionResult AddCustomer(string fullName, string phoneNumber, string? address) => ChangeCart(cart =>
    {
        try { (customers ?? throw new CheckoutValidationException("Customer service unavailable.")).AddNewCustomer(fullName, phoneNumber, address ?? ""); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { throw new CheckoutValidationException(ex.Message); }
        checkoutService.LinkCustomer(cart, phoneNumber);
        TempData["Success"] = $"Customer {fullName?.Trim()} added and linked.";
    });

    /// <summary>BP-17/CQ-10: collect money against a customer's Due balance.</summary>
    [HttpPost]
    public IActionResult CollectDue(string phone, decimal amount, string tender, string? reference)
    {
        try
        {
            var actor = Cashier();
            var customer = checkoutService.FindCustomerByPhone(phone);
            var balance = checkoutService.CollectDue(customer.CustomerId, amount, tender, reference, actor);
            TempData["Success"] = $"Due collected from {customer.FullName}: {SwiftCheckoutPOS.BLL.Pricing.Money.Bdt(amount)} by {tender}. Remaining due: {SwiftCheckoutPOS.BLL.Pricing.Money.Bdt(balance)}.";
        }
        catch (Exception ex) { ReportFailure(ex); }
        return RedirectToAction(nameof(Index));
    }

    private void PrintCommittedSale(CheckoutSessionDTO receipt, bool cashInvolved)
    {
        try
        {
            var hardwarePrinter = printerFactory();
            if (!hardwarePrinter.IsPrinterOnline()) throw new IOException("Printer unavailable.");
            hardwarePrinter.PrintReceipt(receipt);
            if (cashInvolved) hardwarePrinter.OpenCashDrawer(); // PR-Q47/DR-30: drawer opens for cash only
        }
        catch (Exception ex)
        {
            LogFailure(ex);
            TempData["Warning"] = $"Sale {receipt.ReceiptNumber} is saved. Printing or drawer delivery could not be confirmed; do not charge again.";
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
