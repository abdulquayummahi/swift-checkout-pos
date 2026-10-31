// Project: SwiftCheckoutPOS.DAL
// File: Repositories/InventoryRepository.cs
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Entities;

namespace SwiftCheckoutPOS.DAL.Repositories
{
    public class InventoryRepository
    {
        private readonly ApplicationDbContext _context;

        public InventoryRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        // --- Product Management ---
        public IEnumerable<Product> GetAllProducts()
        {
            return _context.Products
                .Include(p => p.Category)
                .Include(p => p.Vendor)
                .OrderBy(p => p.ProductName)
                .ToList();
        }

        public Product GetProductById(int productId)
        {
            return _context.Products
                .Include(p => p.Category)
                .Include(p => p.Vendor)
                .FirstOrDefault(p => p.ProductId == productId);
        }

        public Product GetProductByBarcode(string barcode)
        {
            return _context.Products
                .FirstOrDefault(p => p.Barcode == barcode);
        }

        public void AddProduct(Product product)
        {
            _context.Products.Add(product);
            _context.SaveChanges();
        }

        public void UpdateProduct(Product product)
        {
            _context.Products.Update(product);
            _context.SaveChanges();
        }

        // --- Category Management ---
        public IEnumerable<Category> GetAllCategories()
        {
            return _context.Categories.OrderBy(c => c.CategoryName).ToList();
        }

        public void AddCategory(Category category)
        {
            _context.Categories.Add(category);
            _context.SaveChanges();
        }

        public bool CategoryExists(string categoryName)
        {
            return _context.Categories.Any(c => c.CategoryName.ToLower() == categoryName.ToLower());
        }

        // --- Vendor Management ---
        public IEnumerable<Vendor> GetAllVendors()
        {
            return _context.Vendors.OrderBy(v => v.VendorName).ToList();
        }

        public void AddVendor(Vendor vendor)
        {
            _context.Vendors.Add(vendor);
            _context.SaveChanges();
        }

        public bool VendorExists(string vendorName)
        {
            return _context.Vendors.Any(v => v.VendorName.ToLower() == vendorName.ToLower());
        }
    }
}