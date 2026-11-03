// Project: SwiftCheckoutPOS.UI
// File: Controllers/CustomerController.cs
using SwiftCheckoutPOS.UI.Infrastructure;
using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Services;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class CustomerController : Controller
    {
        private readonly CustomerService _customerService;

        private readonly SettingsService _settings;

        public CustomerController(CustomerService customerService, SettingsService settings)
        {
            _customerService = customerService;
            _settings = settings;
        }

        [HttpGet]
        public IActionResult Index(string searchQuery, string sortOrder, int? page)
        {
            ViewBag.SearchQuery = searchQuery;
            var canFin = SwiftCheckoutPOS.UI.Infrastructure.FinancialViewExtensions.CanViewFinancials(User, _settings);
            ViewBag.CanViewFinancials = canFin;
            if (!canFin && sortOrder is not null && sortOrder.StartsWith("profit")) sortOrder = null; // PR-Q92
            ViewBag.SortOrder = sortOrder;
            var customers = _customerService.GetAllCustomers(searchQuery, sortOrder).ToList();
            if (!canFin) foreach (var c in customers) c.EstimatedProfit = 0;
            var (shown, info) = SwiftCheckoutPOS.UI.Models.PageInfo.Of(customers, page, nameof(Index),
                new Dictionary<string, string?> { ["searchQuery"] = searchQuery, ["sortOrder"] = sortOrder });
            ViewBag.Page = info;
            return View(shown);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(string fullName, string phoneNumber, string address)
        {
            try
            {
                _customerService.AddNewCustomer(fullName, phoneNumber, address);
                TempData["Success"] = $"Customer '{fullName.Trim()}' successfully enrolled.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex is ArgumentException or InvalidOperationException ? this.SafeError(ex) : "The customer could not be saved.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            try
            {
                var profile = _customerService.GetCustomerProfile(id);
                var canFin = SwiftCheckoutPOS.UI.Infrastructure.FinancialViewExtensions.CanViewFinancials(User, _settings);
                ViewBag.CanViewFinancials = canFin;
                if (!canFin)
                {
                    profile.EstimatedProfit = 0;
                    foreach (var t in profile.PurchaseHistory) t.EstimatedProfit = 0;
                }
                return View(profile);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Customer not found.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int customerId, string fullName, string phoneNumber, string address)
        {
            try
            {
                _customerService.UpdateCustomer(customerId, fullName, phoneNumber, address);
                TempData["Success"] = "Customer profile successfully updated.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Update Fault: {this.SafeError(ex)}";
            }
            return RedirectToAction(nameof(Details), new { id = customerId });
        }
    }
}