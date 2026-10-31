// Project: SwiftCheckoutPOS.DAL
// File: Entities/Product.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SwiftCheckoutPOS.DAL.Entities
{
    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Barcode { get; set; } = null!;

        [Required]
        [MaxLength(150)]
        public string ProductName { get; set; } = null!;

        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;

        // NEW: Supplier Mapping
        public int VendorId { get; set; }
        public Vendor Vendor { get; set; } = null!;

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal CostPrice { get; set; }

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal SellingPrice { get; set; }

        // NEW: Tax and Logistics
        [Column(TypeName = "decimal(5, 2)")]
        public decimal TaxRate { get; set; }

        [MaxLength(20)]
        public string UnitOfMeasure { get; set; } = "pcs";

        public int ReorderLevel { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(255)]
        public string? ImagePath { get; set; }

        public int StockQuantity { get; set; }
        public bool IsActive { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;

        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }
}
