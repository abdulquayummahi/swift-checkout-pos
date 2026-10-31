// Project: SwiftCheckoutPOS.BLL
// File: DTOs/ReportDashboardDTO.cs
using System;
using System.Collections.Generic;

namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class ReportDashboardDTO
    {
        // ARCHITECT NEW: Server-Dictated State boundaries
        public DateTime AppliedStartDate { get; set; }
        public DateTime AppliedEndDate { get; set; }

        public decimal GrossRevenue { get; set; }
        public decimal EstimatedProfit { get; set; }
        public decimal TotalDiscounts { get; set; }
        public int CompletedOrders { get; set; }
        public int ItemsSold { get; set; }
        public decimal TaxLiabilities { get; set; }
        public int HeldTransactions { get; set; }
        public decimal HeldOrdersAmount { get; set; }
        public int RefundedTransactions { get; set; }
        public decimal RefundedOrdersAmount { get; set; }

        public List<SaleDTO> Transactions { get; set; } = new List<SaleDTO>();
    }
}