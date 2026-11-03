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
        /// <summary>PR-Q86: unpaid Due owed by this customer (max BDT 2,500).</summary>
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18, 2)")]
        public decimal DueBalance { get; set; }
        public DateTime RegisteredAt { get; set; }

        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    }
}