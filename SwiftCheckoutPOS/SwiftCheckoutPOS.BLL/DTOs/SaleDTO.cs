// Project: SwiftCheckoutPOS.BLL
// File: DTOs/SaleDTO.cs
using System;
using System.Collections.Generic;

namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class SaleDTO
    {
        public int SaleId { get; set; }
        public string ReceiptNumber { get; set; } = null!;
        public DateTime SaleDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = null!;
        public string OrderStatus { get; set; } = null!;

        // Customer Details
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }

        // ARCHITECT NEW: Immutable Audit Trail
        public string? CashierName { get; set; }

        // Financial Aggregates
        public decimal DiscountAmount { get; set; }
        public decimal EstimatedProfit { get; set; }

        public List<SaleItemDTO> SaleItems { get; set; } = new List<SaleItemDTO>();
    }
}