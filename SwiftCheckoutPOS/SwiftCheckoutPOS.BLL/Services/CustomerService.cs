// Project: SwiftCheckoutPOS.BLL
// File: Services/CustomerService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services
{
    public class CustomerService
    {
        private readonly CustomerRepository _customerRepo;
        private readonly SalesRepository _salesRepo;

        public CustomerService(CustomerRepository customerRepo, SalesRepository salesRepo)
        {
            _customerRepo = customerRepo;
            _salesRepo = salesRepo;
        }

        public CustomerDTO LookupCustomerByPhone(string phone)
        {
            var customer = _customerRepo.GetCustomerByPhone(phone);
            if (customer == null) return null;

            return new CustomerDTO
            {
                CustomerId = customer.CustomerId,
                PhoneNumber = customer.PhoneNumber,
                FullName = customer.FullName,
                LoyaltyPoints = customer.LoyaltyPoints
            };
        }

        // ARCHITECT FIX: Upgraded to accept sorting directives and handle derived metric ordering in RAM
        public IEnumerable<CustomerProfileDTO> GetAllCustomers(string searchQuery = null, string sortOrder = null)
        {
            var customers = _customerRepo.GetAllCustomers();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var q = searchQuery.Trim().ToLower();
                customers = customers.Where(c =>
                    c.FullName.ToLower().Contains(q) ||
                    c.PhoneNumber.ToLower().Contains(q) ||
                    (c.Address != null && c.Address.ToLower().Contains(q))
                ).ToList();
            }

            // Execute single RAM-load of historical data to prevent N+1 DB Locks
            var allLoyaltySales = _salesRepo.GetAllLoyaltySalesWithProducts();

            var aggregatedProfiles = customers.Select(c => {
                var customerSales = allLoyaltySales.Where(s => s.CustomerId == c.CustomerId).ToList();
                var completedSales = customerSales.Where(s => s.OrderStatus == "Completed").ToList();
                var refundedSales = customerSales.Where(s => s.OrderStatus == "Refunded").ToList();

                decimal totalDiscount = completedSales.Sum(s => s.SubTotal + s.TaxAmount - s.TotalAmount);
                decimal totalCost = completedSales.SelectMany(s => s.SaleItems).Sum(si => (si.Product?.CostPrice ?? 0) * si.Quantity);

                return new CustomerProfileDTO
                {
                    CustomerId = c.CustomerId,
                    FullName = c.FullName,
                    PhoneNumber = c.PhoneNumber,
                    Address = c.Address,
                    LoyaltyPoints = c.LoyaltyPoints,
                    TotalRevenue = completedSales.Sum(s => s.TotalAmount),
                    EstimatedProfit = completedSales.Sum(s => s.SubTotal) - totalCost - totalDiscount,
                    TotalDiscounts = totalDiscount,
                    TotalRefunded = refundedSales.Sum(s => s.TotalAmount),
                    TotalPurchases = completedSales.Count,
                    TotalItemsSold = completedSales.SelectMany(s => s.SaleItems).Sum(si => si.Quantity)
                };
            }).ToList();

            // Apply specific sorting logic to the derived metrics
            return sortOrder switch
            {
                "spent_desc" => aggregatedProfiles.OrderByDescending(p => p.TotalRevenue),
                "spent_asc" => aggregatedProfiles.OrderBy(p => p.TotalRevenue),
                "profit_desc" => aggregatedProfiles.OrderByDescending(p => p.EstimatedProfit),
                "profit_asc" => aggregatedProfiles.OrderBy(p => p.EstimatedProfit),
                "discount_desc" => aggregatedProfiles.OrderByDescending(p => p.TotalDiscounts),
                "discount_asc" => aggregatedProfiles.OrderBy(p => p.TotalDiscounts),
                "refunded_desc" => aggregatedProfiles.OrderByDescending(p => p.TotalRefunded),
                "refunded_asc" => aggregatedProfiles.OrderBy(p => p.TotalRefunded),
                "points_desc" => aggregatedProfiles.OrderByDescending(p => p.LoyaltyPoints),
                "points_asc" => aggregatedProfiles.OrderBy(p => p.LoyaltyPoints),
                _ => aggregatedProfiles.OrderBy(p => p.FullName) // Default A-Z fallback
            };
        }

        public void AddNewCustomer(string fullName, string phoneNumber, string address)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber)) throw new ArgumentException("Phone number is required.");

            var existing = _customerRepo.GetCustomerByPhone(phoneNumber.Trim());
            if (existing != null) throw new InvalidOperationException("A customer with this phone number already exists.");

            var customer = new Customer
            {
                FullName = fullName.Trim(),
                PhoneNumber = phoneNumber.Trim(),
                Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
                LoyaltyPoints = 0
            };

            _customerRepo.AddCustomer(customer);
        }

        public void UpdateCustomer(int customerId, string fullName, string phoneNumber, string address)
        {
            var customer = _customerRepo.GetCustomerById(customerId);
            if (customer == null) throw new Exception("Customer not found.");

            if (customer.PhoneNumber != phoneNumber.Trim())
            {
                var existing = _customerRepo.GetCustomerByPhone(phoneNumber.Trim());
                if (existing != null) throw new InvalidOperationException("This phone number is registered to another customer.");
            }

            customer.FullName = fullName.Trim();
            customer.PhoneNumber = phoneNumber.Trim();
            customer.Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();

            _customerRepo.UpdateCustomer(customer);
        }

        public CustomerProfileDTO GetCustomerProfile(int customerId)
        {
            var customer = _customerRepo.GetCustomerById(customerId);
            if (customer == null) throw new Exception("Customer not found in the database.");

            var allSales = _salesRepo.GetSalesByCustomerId(customerId);
            var completedSales = allSales.Where(s => s.OrderStatus == "Completed").ToList();
            var refundedSales = allSales.Where(s => s.OrderStatus == "Refunded").ToList();

            decimal totalDiscount = completedSales.Sum(s => s.SubTotal + s.TaxAmount - s.TotalAmount);
            decimal totalCost = completedSales.SelectMany(s => s.SaleItems).Sum(si => (si.Product?.CostPrice ?? 0) * si.Quantity);

            return new CustomerProfileDTO
            {
                CustomerId = customer.CustomerId,
                FullName = customer.FullName,
                PhoneNumber = customer.PhoneNumber,
                Address = customer.Address,
                LoyaltyPoints = customer.LoyaltyPoints,

                TotalRevenue = completedSales.Sum(s => s.TotalAmount),
                EstimatedProfit = completedSales.Sum(s => s.SubTotal) - totalCost - totalDiscount,
                TotalDiscounts = totalDiscount,
                TotalRefunded = refundedSales.Sum(s => s.TotalAmount),
                TotalPurchases = completedSales.Count,
                TotalItemsSold = completedSales.SelectMany(s => s.SaleItems).Sum(si => si.Quantity),

                PurchaseHistory = allSales.Select(s => {
                    decimal discount = s.SubTotal + s.TaxAmount - s.TotalAmount;
                    decimal cost = s.SaleItems.Sum(si => (si.Product?.CostPrice ?? 0) * si.Quantity);
                    return new SaleDTO
                    {
                        SaleId = s.SaleId,
                        ReceiptNumber = s.ReceiptNumber,
                        SaleDate = s.SaleDate,
                        TotalAmount = s.TotalAmount,
                        PaymentMethod = s.PaymentMethod,
                        OrderStatus = s.OrderStatus,
                        CustomerName = customer.FullName,
                        CustomerPhone = customer.PhoneNumber,
                        DiscountAmount = discount,
                        EstimatedProfit = s.SubTotal - cost - discount,
                        SaleItems = s.SaleItems.Select(si => new SaleItemDTO
                        {
                            ProductId = si.ProductId,
                            ProductName = si.Product?.ProductName ?? "Unknown Item",
                            Quantity = si.Quantity,
                            UnitPrice = si.UnitPrice,
                            LineTotal = si.LineTotal
                        }).ToList()
                    };
                }).ToList()
            };
        }
    }
}