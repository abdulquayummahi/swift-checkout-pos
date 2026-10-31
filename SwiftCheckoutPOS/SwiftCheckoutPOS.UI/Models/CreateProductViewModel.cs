// Project: SwiftCheckoutPOS.UI
// File: Models/CreateProductViewModel.cs
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace SwiftCheckoutPOS.UI.Models
{
    public class CreateProductViewModel
    {
        [Required(ErrorMessage = "Hardware SKU/Barcode is required.")]
        public string Barcode { get; set; } = null!;

        [Required(ErrorMessage = "Product Name is required.")]
        public string ProductName { get; set; } = null!;

        [Required(ErrorMessage = "Please select a category.")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        // NEW: Vendor Mapping
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
        [Display(Name = "Initial Stock")]
        public int StockQuantity { get; set; }

        // NEW: Tax, Logistics, and Media
        [Range(0, 100, ErrorMessage = "Tax rate must be a percentage between 0 and 100.")]
        [Display(Name = "Tax Rate (%)")]
        public decimal TaxRate { get; set; }

        [Required]
        [Display(Name = "Unit of Measure")]
        public string UnitOfMeasure { get; set; } = "pcs";

        [Display(Name = "Low Stock Alert Level")]
        public int ReorderLevel { get; set; } = 10;

        [MaxLength(500)]
        public string? Description { get; set; }

        [Display(Name = "Product Image")]
        public IFormFile? ImageFile { get; set; }

        // Dropdown data populated by the Controller
        public SelectList? AvailableCategories { get; set; }
        public SelectList? AvailableVendors { get; set; }
    }
}