// Project: SwiftCheckoutPOS.DAL
// File: Entities/TillReconciliation.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace SwiftCheckoutPOS.DAL.Entities
{
    public class TillReconciliation
    {
        [Key] // <-- Explicitly defines the Primary Key for EF Core
        public int ReconciliationId { get; set; }

        public int UserId { get; set; }
        public DateTime OpenTime { get; set; }
        public DateTime CloseTime { get; set; }

        // Expected vs Actual
        public decimal ExpectedCash { get; set; }
        public decimal CountedCash { get; set; }
        public decimal Discrepancy => CountedCash - ExpectedCash;

        public string Notes { get; set; } = string.Empty;
    }
}