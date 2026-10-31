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