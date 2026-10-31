// Project: SwiftCheckoutPOS.UI
// File: Controllers/CustomerController.cs
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

        public CustomerController(CustomerService customerService)
        {
            _customerService = customerService;
        }

        [HttpGet]
        public IActionResult Index(string searchQuery, string sortOrder)
        {
            ViewBag.SearchQuery = searchQuery;
            ViewBag.SortOrder = sortOrder; // Persist sorting state
            var customers = _customerService.GetAllCustomers(searchQuery, sortOrder);
            return View(customers);
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
                TempData["Error"] = $"Enrollment Fault: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            try
            {
                var profile = _customerService.GetCustomerProfile(id);
                return View(profile);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Data Retrieval Fault: {ex.Message}";
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
                TempData["Error"] = $"Update Fault: {ex.Message}";
            }
            return RedirectToAction(nameof(Details), new { id = customerId });
        }
    }
}