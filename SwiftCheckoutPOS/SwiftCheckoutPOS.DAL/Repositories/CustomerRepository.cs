// Project: SwiftCheckoutPOS.DAL
// File: Repositories/CustomerRepository.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Entities;

namespace SwiftCheckoutPOS.DAL.Repositories
{
    public class CustomerRepository
    {
        private readonly ApplicationDbContext _context;

        public CustomerRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // --- NEW CRM METHODS ---

        public sealed record CustomerTotalsRow(int CustomerId, decimal Revenue, decimal Discounts, decimal Refunded, int Purchases, decimal Cost, int Items);

        /// <summary>
        /// BP-11: one search query plus two grouped SQL aggregates, instead of loading every customer sale and item
        /// into memory. Same definitions as before: completed = Completed/PartiallyRefunded, refunded = Refunded.
        /// </summary>
        public (IReadOnlyList<Customer> Customers, IReadOnlyDictionary<int, CustomerTotalsRow> Totals) SearchWithTotals(string? search)
        {
            var query = _context.Customers.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var q = search.Trim();
                query = query.Where(c => c.FullName.Contains(q) || c.PhoneNumber.Contains(q) || (c.Address != null && c.Address.Contains(q)));
            }
            var customers = query.OrderBy(c => c.FullName).ToList();

            var sales = _context.Sales.AsNoTracking().Where(s => s.CustomerId != null)
                .GroupBy(s => s.CustomerId!.Value)
                .Select(g => new
                {
                    Id = g.Key,
                    Revenue = g.Sum(s => s.OrderStatus == "Completed" || s.OrderStatus == "PartiallyRefunded" ? s.TotalAmount - s.TaxAmount : 0m),
                    Discounts = g.Sum(s => s.OrderStatus == "Completed" || s.OrderStatus == "PartiallyRefunded" ? s.SubTotal - s.TotalAmount : 0m),
                    Refunded = g.Sum(s => s.OrderStatus == "Refunded" ? s.TotalAmount : 0m),
                    Purchases = g.Sum(s => s.OrderStatus == "Completed" || s.OrderStatus == "PartiallyRefunded" ? 1 : 0)
                }).ToList();
            var items = _context.SaleItems.AsNoTracking()
                .Where(i => i.Sale.CustomerId != null && (i.Sale.OrderStatus == "Completed" || i.Sale.OrderStatus == "PartiallyRefunded"))
                .GroupBy(i => i.Sale.CustomerId!.Value)
                .Select(g => new { Id = g.Key, Cost = g.Sum(i => i.CostAtSale * i.Quantity), Items = g.Sum(i => i.Quantity) })
                .ToDictionary(x => x.Id);
            var totals = sales.ToDictionary(s => s.Id, s =>
            {
                items.TryGetValue(s.Id, out var it);
                return new CustomerTotalsRow(s.Id, s.Revenue, s.Discounts, s.Refunded, s.Purchases, it?.Cost ?? 0m, it?.Items ?? 0);
            });
            return (customers, totals);
        }

        public IEnumerable<Customer> GetAllCustomers()
        {
            return _context.Customers
                .AsNoTracking()
                .OrderBy(c => c.FullName)
                .ToList();
        }

        public Customer? GetCustomerById(int customerId)
        {
            // ARCHITECT DIRECTIVE: Do not use AsNoTracking() here. 
            // The BLL fetches this entity to mutate and update it.
            return _context.Customers
                .FirstOrDefault(c => c.CustomerId == customerId);
        }

        public void AddCustomer(Customer customer)
        {
            // Ensure the dashboard metrics have an accurate timestamp to count
            if (customer.RegisteredAt == default)
            {
                customer.RegisteredAt = DateTime.Now;
            }

            _context.Customers.Add(customer);
            _context.SaveChanges();
        }

        public void UpdateCustomer(Customer customer)
        {
            _context.Customers.Update(customer);
            _context.SaveChanges();
        }

        // --- EXISTING METHODS PRESERVED ---

        public Customer? GetCustomerByPhone(string phoneNumber)
        {
            return _context.Customers
                .AsNoTracking()
                .FirstOrDefault(c => c.PhoneNumber == phoneNumber);
        }

        // Counts new loyalty customers registered within a date range
        public int GetNewCustomerCount(DateTime startDate, DateTime endDate)
        {
            return _context.Customers
                .AsNoTracking()
                .Count(c => c.RegisteredAt >= startDate && c.RegisteredAt <= endDate);
        }
    }
}