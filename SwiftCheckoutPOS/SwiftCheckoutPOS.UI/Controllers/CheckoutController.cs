// Project: SwiftCheckoutPOS.UI
// File: Controllers/CheckoutController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Interfaces;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.UI.Models;
using System;
using System.Linq;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly CheckoutService _checkoutService;
        private readonly InventoryService _inventoryService;
        private readonly CustomerService _customerService;
        private readonly IReceiptPrinter _hardwarePrinter;

        private const string ActiveCartKey = "ActiveCart";

        public CheckoutController(
            CheckoutService checkoutService,
            InventoryService inventoryService,
            CustomerService customerService,
            IReceiptPrinter hardwarePrinter)
        {
            _checkoutService = checkoutService;
            _inventoryService = inventoryService;
            _customerService = customerService;
            _hardwarePrinter = hardwarePrinter;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var products = _inventoryService.GetAllSystemProducts()
                .Where(p => p.IsActive && p.StockQuantity > 0)
                .OrderBy(p => p.ProductName);

            var model = new CheckoutViewModel
            {
                AvailableProducts = products,
                Cart = GetActiveSession(),
                HeldOrders = _checkoutService.GetHeldOrders()
            };

            return View(model);
        }

        // --- CART OPERATIONS ---
        [HttpPost]
        public IActionResult ScanBarcode(string barcode)
        {
            var cart = GetActiveSession();
            try
            {
                _checkoutService.AddToCartByBarcode(cart, barcode);
                SaveActiveSession(cart);
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult AddById(int productId)
        {
            var cart = GetActiveSession();
            try
            {
                _checkoutService.AddToCart(cart, productId);
                SaveActiveSession(cart);
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult UpdateQuantity(int productId, int change)
        {
            var cart = GetActiveSession();
            try
            {
                if (change > 0)
                {
                    _checkoutService.AddToCart(cart, productId, change);
                }
                else
                {
                    var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                    if (item != null)
                    {
                        item.Quantity += change;
                        if (item.Quantity <= 0) cart.Items.Remove(item);
                    }
                }
                SaveActiveSession(cart);
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult RemoveItem(int productId)
        {
            var cart = GetActiveSession();
            cart.Items.RemoveAll(i => i.ProductId == productId);
            SaveActiveSession(cart);
            return RedirectToAction(nameof(Index));
        }

        // --- LOYALTY & DISCOUNTS ---
        [HttpPost]
        public IActionResult SetCustomerType(string type)
        {
            var cart = GetActiveSession();
            cart.CustomerType = type;
            if (type == "Walk-in")
            {
                cart.CustomerId = null;
                cart.CustomerName = "";
            }
            SaveActiveSession(cart);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult LookupLoyaltyCustomer(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return Json(new { success = false });
            var customer = _customerService.LookupCustomerByPhone(phone.Trim());

            if (customer != null)
            {
                var cart = GetActiveSession();
                cart.CustomerType = "Loyalty";
                cart.CustomerName = customer.FullName;
                cart.CustomerId = customer.CustomerId;
                SaveActiveSession(cart);
                return Json(new { success = true, name = customer.FullName, points = customer.LoyaltyPoints });
            }
            return Json(new { success = false, message = "Customer not found." });
        }

        [HttpPost]
        public IActionResult ApplyBillDiscount(decimal discountValue, string discountType)
        {
            var cart = GetActiveSession();
            if (discountType == "Percent")
            {
                cart.BillDiscountPercentage = discountValue;
                cart.BillDiscountAmount = 0;
            }
            else
            {
                cart.BillDiscountAmount = discountValue;
                cart.BillDiscountPercentage = 0;
            }
            SaveActiveSession(cart);
            return RedirectToAction(nameof(Index));
        }

        // --- SESSION MANAGEMENT ---
        [HttpPost]
        public IActionResult HoldOrder()
        {
            var activeCart = GetActiveSession();
            if (!activeCart.Items.Any()) return RedirectToAction(nameof(Index));

            try
            {
                // ARCHITECT FIX: Inject authenticated user identity into BLL
                string cashierName = User.Identity?.Name ?? "System Admin";

                _checkoutService.HoldOrder(activeCart, cashierName);
                HttpContext.Session.Remove(ActiveCartKey);
                TempData["Success"] = $"Order {activeCart.InvoiceNumber} safely stored in database.";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult ResumeOrder(int saleId)
        {
            try
            {
                var resumedCart = _checkoutService.ResumeOrder(saleId);
                var activeCart = GetActiveSession();

                if (activeCart.Items.Any())
                {
                    // ARCHITECT FIX: Inject authenticated user identity into BLL for auto-hold
                    string cashierName = User.Identity?.Name ?? "System Admin";
                    _checkoutService.HoldOrder(activeCart, cashierName);

                    TempData["Success"] = $"Active order safely held. Resumed Order {resumedCart.InvoiceNumber}.";
                }
                else
                {
                    TempData["Success"] = $"Resumed Order {resumedCart.InvoiceNumber}.";
                }

                SaveActiveSession(resumedCart);
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult ClearCart()
        {
            HttpContext.Session.Remove(ActiveCartKey);
            return RedirectToAction(nameof(Index));
        }

        // --- HARDWARE & FINALIZATION ---
        [HttpGet]
        public IActionResult GetInvoicePreview()
        {
            var session = GetActiveSession();
            if (session == null || !session.Items.Any())
            {
                return Json(new { success = false, message = "Active cart not found." });
            }

            string previewText = _hardwarePrinter.GenerateReceiptPreview(session);
            return Json(new { success = true, preview = previewText });
        }

        [HttpPost]
        public IActionResult TriggerHardwarePrint(string receiptNumber, decimal totalAmount)
        {
            try
            {
                if (!_hardwarePrinter.IsPrinterOnline())
                {
                    return Json(new { success = false, message = "HARDWARE FAULT: Printer offline or COM port inaccessible." });
                }

                var session = GetActiveSession();
                if (session == null || !session.Items.Any())
                {
                    return Json(new { success = false, message = "STATE ERROR: Active cart not found in memory." });
                }

                if (session.InvoiceNumber != receiptNumber)
                {
                    return Json(new { success = false, message = "DATA DESYNC: Session Invoice Number mismatch during print job." });
                }

                _hardwarePrinter.OpenCashDrawer();
                _hardwarePrinter.PrintReceipt(session);

                return Json(new { success = true, message = "Hardware triggered successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"CRITICAL HARDWARE EXCEPTION: {ex.Message}" });
            }
        }

        [HttpPost]
        public IActionResult Finalize(string paymentMethod)
        {
            var cart = GetActiveSession();

            if (cart.CustomerType == "Loyalty" && cart.CustomerId == null)
            {
                TempData["Error"] = "Validation Failed: You must link a valid loyalty phone number before processing payment.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // ARCHITECT FIX: Inject authenticated user identity into BLL
                string cashierName = User.Identity?.Name ?? "System Admin";

                var receipt = _checkoutService.FinalizeTransaction(cart, paymentMethod, cashierName);

                HttpContext.Session.Remove(ActiveCartKey);
                TempData["Success"] = $"Transaction Complete. Receipt: {receipt} | Payment: {paymentMethod}";
            }
            catch (Exception ex) { TempData["Error"] = ex.Message; }

            return RedirectToAction(nameof(Index));
        }

        // --- State Helpers ---
        private CheckoutSessionDTO GetActiveSession()
        {
            var value = HttpContext.Session.GetString(ActiveCartKey);
            return value == null ? new CheckoutSessionDTO() : JsonConvert.DeserializeObject<CheckoutSessionDTO>(value);
        }

        private void SaveActiveSession(CheckoutSessionDTO session)
        {
            HttpContext.Session.SetString(ActiveCartKey, JsonConvert.SerializeObject(session));
        }
    }
}