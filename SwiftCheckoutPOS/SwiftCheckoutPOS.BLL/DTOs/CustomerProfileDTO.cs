// Project: SwiftCheckoutPOS.BLL
// File: DTOs/CustomerProfileDTO.cs
using System.Collections.Generic;

namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class CustomerProfileDTO
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string? Address { get; set; }
        public int LoyaltyPoints { get; set; }

        public decimal TotalRevenue { get; set; }
        public decimal EstimatedProfit { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal TotalRefunded { get; set; } // Expanded BI
        public int TotalPurchases { get; set; }
        public int TotalItemsSold { get; set; }

        public List<SaleDTO> PurchaseHistory { get; set; } = new List<SaleDTO>();
    }
}