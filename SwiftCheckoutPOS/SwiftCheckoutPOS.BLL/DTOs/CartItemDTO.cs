// Project: SwiftCheckoutPOS.BLL
// File: DTOs/CartItemDTO.cs
namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class CartItemDTO
    {
        public int ProductId { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TaxRate { get; set; }

        // Item-level discount (Flat amount per unit for this example)
        public decimal ItemDiscount { get; set; }

        public decimal LineTotal => Math.Round(Math.Max(0, UnitPrice - ItemDiscount) * Math.Max(0, Quantity), 2);
    }
}
