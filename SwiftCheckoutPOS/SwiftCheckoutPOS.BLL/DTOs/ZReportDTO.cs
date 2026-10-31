// Project: SwiftCheckoutPOS.BLL
// File: DTOs/ZReportDTO.cs
using System;

namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class ZReportDTO
    {
        public DateTime ReportDate { get; set; } // Kept for legacy backward compatibility

        // ARCHITECT NEW: Dynamic Date Range Support
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public int TotalTransactions { get; set; }
        public decimal NetRevenue { get; set; } // Maps to Gross SubTotal

        // ARCHITECT NEW: Deep Discount Auditing
        public decimal TotalDiscounts { get; set; }

        public decimal TotalTaxCollected { get; set; }
        public decimal GrossRevenue { get; set; } // Maps to Net Collected

        // Tender Breakdowns for Drawer Reconciliation
        public decimal CashTotal { get; set; }
        public decimal CardTotal { get; set; }
        public decimal MobileWalletTotal { get; set; }
    }
}