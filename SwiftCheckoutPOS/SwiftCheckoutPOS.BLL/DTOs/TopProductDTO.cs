// Project: SwiftCheckoutPOS.BLL
// File: DTOs/TopProductDTO.cs
namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class TopProductDTO
    {
        public string ProductName { get; set; } = null!;
        public int QuantitySold { get; set; }

        // Used exclusively by the Presentation Layer to calculate the width of the CSS bar charts
        public decimal Percentage { get; set; }
    }
}