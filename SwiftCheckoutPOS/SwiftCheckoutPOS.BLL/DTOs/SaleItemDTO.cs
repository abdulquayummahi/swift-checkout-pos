// Project: SwiftCheckoutPOS.BLL
// File: DTOs/SaleItemDTO.cs
namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class SaleItemDTO
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }
}