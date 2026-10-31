// Project: SwiftCheckoutPOS.DAL
// File: Entities/Customer.cs
using System;
using System.Collections.Generic;

namespace SwiftCheckoutPOS.DAL.Entities
{
    public class Customer
    {
        public int CustomerId { get; set; }
        public string PhoneNumber { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string? Address { get; set; } // ARCHITECT NEW: Address mapping
        public int LoyaltyPoints { get; set; }
        public DateTime RegisteredAt { get; set; }

        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    }
}