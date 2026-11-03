// Project: SwiftCheckoutPOS.BLL
// File: DTOs/ProductDTO.cs
namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class ProductDTO
    {
        public int ProductId { get; set; }
        /// <summary>Base64 SQL rowversion the edit form was loaded with (IMM-06).</summary>
        public string? RowVersion { get; set; }
        public int ReservedQuantity { get; set; }
        /// <summary>CQ-03: keep the stored cost (Manager without financial view edits without seeing it).</summary>
        public bool KeepCost { get; set; }
        public string Barcode { get; set; } = null!;
        public string ProductName { get; set; } = null!;
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }

        public int VendorId { get; set; }
        public string? VendorName { get; set; }
        public decimal TaxRate { get; set; }
        public string UnitOfMeasure { get; set; } = "pcs";
        public int ReorderLevel { get; set; }
        public string? Description { get; set; }
        public string? ImagePath { get; set; }

        public decimal CostPrice { get; set; }
        public decimal SellingPrice { get; set; }

        // ARCHITECT NEW: Derived Financial Metric
        public decimal MarginPercentage { get; set; }

        public int StockQuantity { get; set; }
        public bool IsActive { get; set; }
    }

    public class VendorDTO
    {
        public int VendorId { get; set; }
        public string VendorName { get; set; } = null!;
    }
}