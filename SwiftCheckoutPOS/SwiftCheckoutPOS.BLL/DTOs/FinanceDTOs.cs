// Project: SwiftCheckoutPOS.BLL
// File: DTOs/FinanceDTOs.cs
using System;
using System.Collections.Generic;

namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class LedgerEntryDTO
    {
        public int EntryId { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Category { get; set; } = null!;
        public string TransactionType { get; set; } = null!;
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public string? RecordedBy { get; set; }
    }

    public class FinanceDashboardDTO
    {
        // Periodic Metrics (Based on UI Date Filters)
        public decimal TotalCapitalInflow { get; set; }
        public decimal OperatingExpenses { get; set; }
        public decimal InventoryCosts { get; set; }
        public decimal NetLedgerBalance { get; set; }
        public List<LedgerEntryDTO> Entries { get; set; } = new List<LedgerEntryDTO>();

        // ARCHITECT NEW: Lifetime Break-Even (BEP) Metrics
        public decimal LifetimeCapital { get; set; }
        public decimal LifetimeNetProfit { get; set; }
        public decimal AverageMonthlyProfit { get; set; }
        public decimal PaybackPercentage { get; set; }
        public int EstimatedMonthsToBreakEven { get; set; }
    }
}