// Project: SwiftCheckoutPOS.BLL
// File: DTOs/CartItemDTO.cs
using SwiftCheckoutPOS.BLL.Pricing;

namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class CartItemDTO
    {
        public int ProductId { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        /// <summary>VAT-inclusive shelf price (PR-Q08).</summary>
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TaxRate { get; set; }

        /// <summary>Item-level discount, flat amount per unit (PR-Q13).</summary>
        public decimal ItemDiscount { get; set; }

        /// <summary>PR-Q05: each line total rounded to 2 decimals, half away from zero.</summary>
        public decimal LineTotal => Money.R2(Math.Max(0, UnitPrice - ItemDiscount) * Math.Max(0, Quantity));
    }
}
