// Project: SwiftCheckoutPOS.UI
// File: Models/EditProductViewModel.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SwiftCheckoutPOS.UI.Models
{
    public class EditProductViewModel
    {
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Hardware SKU/Barcode is required.")]
        public string Barcode { get; set; } = null!;

        [Required(ErrorMessage = "Product Name is required.")]
        public string ProductName { get; set; } = null!;

        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Please select a primary supplier.")]
        [Display(Name = "Primary Supplier (Vendor)")]
        public int VendorId { get; set; }

        [Required]
        [Range(0.01, 100000, ErrorMessage = "Cost price must be greater than zero.")]
        [Display(Name = "Wholesale Cost (BDT)")]
        public decimal CostPrice { get; set; }

        [Required]
        [Range(0.01, 100000, ErrorMessage = "Selling price must be greater than zero.")]
        [Display(Name = "Retail Price (BDT)")]
        public decimal SellingPrice { get; set; }

        [Required]
        [Range(0, 10000, ErrorMessage = "Invalid stock quantity.")]
        [Display(Name = "Units in Stock")]
        public int StockQuantity { get; set; }

        [Range(0, 100, ErrorMessage = "Tax rate must be a percentage between 0 and 100.")]
        [Display(Name = "Tax Rate (%)")]
        public decimal TaxRate { get; set; }

        [Required]
        [Display(Name = "Unit of Measure")]
        public string UnitOfMeasure { get; set; } = "pcs";

        [Display(Name = "Low Stock Alert Level")]
        public int ReorderLevel { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? ExistingImagePath { get; set; }

        [Display(Name = "Update Product Image")]
        public IFormFile? ImageFile { get; set; }

        [Display(Name = "Product is Active (Scannable at Register)")]
        public bool IsActive { get; set; }

        public SelectList? AvailableCategories { get; set; }
        public SelectList? AvailableVendors { get; set; }
    }
}