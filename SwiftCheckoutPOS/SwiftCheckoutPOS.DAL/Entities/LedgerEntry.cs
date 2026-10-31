// Project: SwiftCheckoutPOS.DAL
// File: Entities/LedgerEntry.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace SwiftCheckoutPOS.DAL.Entities
{
    public class LedgerEntry
    {
        [Key] // ARCHITECT FIX: Explicitly maps the Primary Key, overriding EF Core naming conventions
        public int EntryId { get; set; }

        public DateTime TransactionDate { get; set; }
        public string Category { get; set; } = null!;
        public string TransactionType { get; set; } = null!; // "Inflow" or "Outflow"
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string? RecordedBy { get; set; }
    }
}