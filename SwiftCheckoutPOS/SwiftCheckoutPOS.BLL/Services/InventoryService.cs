// Project: SwiftCheckoutPOS.BLL
// File: Services/InventoryService.cs
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.DAL.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SwiftCheckoutPOS.BLL.Services
{
    public class InventoryService
    {
        private readonly InventoryRepository _inventoryRepo;
        private readonly FinanceService _financeService;

        // ARCHITECT FIX: Cross-Domain Dependency Injection
        public InventoryService(InventoryRepository inventoryRepo, FinanceService financeService)
        {
            _inventoryRepo = inventoryRepo;
            _financeService = financeService;
        }

        public IEnumerable<ProductDTO> GetAllSystemProducts(string searchQuery = null, string sortOrder = null)
        {
            var products = _inventoryRepo.GetAllProducts();

            var mappedProducts = products.Select(p => new ProductDTO
            {
                ProductId = p.ProductId,
                Barcode = p.Barcode,
                ProductName = p.ProductName,
                CategoryId = p.CategoryId,
                CategoryName = p.Category?.CategoryName ?? "Uncategorized",
                VendorId = p.VendorId,
                VendorName = p.Vendor?.VendorName ?? "Unknown",
                CostPrice = Math.Round(p.CostPrice),
                SellingPrice = Math.Round(p.SellingPrice),
                MarginPercentage = p.SellingPrice > 0 ? ((p.SellingPrice - p.CostPrice) / p.SellingPrice) * 100 : 0,
                StockQuantity = p.StockQuantity,
                IsActive = p.IsActive,
                ImagePath = p.ImagePath,
                TaxRate = Math.Round(p.TaxRate),
                UnitOfMeasure = p.UnitOfMeasure,
                ReorderLevel = p.ReorderLevel,
                Description = p.Description
            }).ToList();

            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var q = searchQuery.Trim().ToLower();
                mappedProducts = mappedProducts.Where(p =>
                    p.ProductName.ToLower().Contains(q) ||
                    p.Barcode.ToLower().Contains(q) ||
                    p.CategoryName.ToLower().Contains(q)
                ).ToList();
            }

            return sortOrder switch
            {
                "name_desc" => mappedProducts.OrderByDescending(p => p.ProductName),
                "barcode_asc" => mappedProducts.OrderBy(p => p.Barcode),
                "barcode_desc" => mappedProducts.OrderByDescending(p => p.Barcode),
                "cost_asc" => mappedProducts.OrderBy(p => p.CostPrice),
                "cost_desc" => mappedProducts.OrderByDescending(p => p.CostPrice),
                "retail_asc" => mappedProducts.OrderBy(p => p.SellingPrice),
                "retail_desc" => mappedProducts.OrderByDescending(p => p.SellingPrice),
                "margin_asc" => mappedProducts.OrderBy(p => p.MarginPercentage),
                "margin_desc" => mappedProducts.OrderByDescending(p => p.MarginPercentage),
                "stock_asc" => mappedProducts.OrderBy(p => p.StockQuantity),
                "stock_desc" => mappedProducts.OrderByDescending(p => p.StockQuantity),
                "status_asc" => mappedProducts.OrderByDescending(p => p.IsActive),
                "status_desc" => mappedProducts.OrderBy(p => p.IsActive),
                _ => mappedProducts.OrderBy(p => p.ProductName)
            };
        }

        public IEnumerable<CategoryDTO> GetAllCategories()
        {
            var categories = _inventoryRepo.GetAllCategories();
            return categories.Select(c => new CategoryDTO { CategoryId = c.CategoryId, CategoryName = c.CategoryName }).ToList();
        }

        public ProductDTO GetProductForEdit(int productId)
        {
            var p = _inventoryRepo.GetProductById(productId);
            if (p == null) throw new Exception("Product not found.");

            return new ProductDTO
            {
                ProductId = p.ProductId,
                Barcode = p.Barcode,
                ProductName = p.ProductName,
                CategoryId = p.CategoryId,
                VendorId = p.VendorId,
                CostPrice = Math.Round(p.CostPrice),
                SellingPrice = Math.Round(p.SellingPrice),
                TaxRate = Math.Round(p.TaxRate),
                StockQuantity = p.StockQuantity,
                UnitOfMeasure = p.UnitOfMeasure,
                ReorderLevel = p.ReorderLevel,
                Description = p.Description,
                ImagePath = p.ImagePath,
                IsActive = p.IsActive
            };
        }

        public void AddNewProduct(ProductDTO newProductDto, int categoryId)
        {
            var existingProduct = _inventoryRepo.GetProductByBarcode(newProductDto.Barcode);
            if (existingProduct != null) throw new InvalidOperationException($"Barcode '{newProductDto.Barcode}' is already in use.");
            if (newProductDto.SellingPrice < newProductDto.CostPrice) throw new ArgumentException("Selling price cannot be lower than wholesale cost price.");

            var entity = new Product
            {
                Barcode = newProductDto.Barcode,
                ProductName = newProductDto.ProductName,
                CategoryId = categoryId,
                VendorId = newProductDto.VendorId,
                TaxRate = Math.Round(newProductDto.TaxRate),
                UnitOfMeasure = newProductDto.UnitOfMeasure,
                ReorderLevel = newProductDto.ReorderLevel,
                Description = newProductDto.Description,
                ImagePath = newProductDto.ImagePath,
                CostPrice = Math.Round(newProductDto.CostPrice),
                SellingPrice = Math.Round(newProductDto.SellingPrice),
                StockQuantity = newProductDto.StockQuantity,
                IsActive = true
            };

            _inventoryRepo.AddProduct(entity);

            // Seed initial stock value to the ledger if quantity exists
            if (entity.StockQuantity > 0)
            {
                _financeService.RecordSystemInventoryCost(entity.StockQuantity * entity.CostPrice, $"Initial Stock - {entity.Barcode}");
            }
        }

        public void UpdateProduct(ProductDTO dto, int categoryId)
        {
            var entity = _inventoryRepo.GetProductById(dto.ProductId);
            if (entity == null) throw new Exception("Product record missing.");

            if (entity.Barcode != dto.Barcode)
            {
                if (_inventoryRepo.GetProductByBarcode(dto.Barcode) != null) throw new InvalidOperationException($"Barcode '{dto.Barcode}' is already assigned.");
            }

            if (dto.SellingPrice < dto.CostPrice) throw new ArgumentException("Loss Prevention: Selling price cannot be lower than cost price.");

            entity.Barcode = dto.Barcode; entity.ProductName = dto.ProductName; entity.CategoryId = categoryId; entity.VendorId = dto.VendorId; entity.CostPrice = Math.Round(dto.CostPrice); entity.SellingPrice = Math.Round(dto.SellingPrice); entity.TaxRate = Math.Round(dto.TaxRate); entity.StockQuantity = dto.StockQuantity; entity.UnitOfMeasure = dto.UnitOfMeasure; entity.ReorderLevel = dto.ReorderLevel; entity.Description = dto.Description; entity.IsActive = dto.IsActive;

            if (!string.IsNullOrWhiteSpace(dto.ImagePath)) entity.ImagePath = dto.ImagePath;

            _inventoryRepo.UpdateProduct(entity);
        }

        // --- ARCHITECT NEW: Hardware-speed Restock Engine using WAC ---
        public void RestockProduct(string barcode, int addedQty, decimal totalBatchCost, decimal? newRetailPrice)
        {
            var product = _inventoryRepo.GetProductByBarcode(barcode.Trim());
            if (product == null) throw new InvalidOperationException("Unrecognized barcode. Product not found.");
            if (addedQty <= 0) throw new ArgumentException("Restock quantity must be greater than zero.");
            if (totalBatchCost < 0) throw new ArgumentException("Batch cost cannot be negative.");

            // Weighted Average Cost (WAC) Algorithm
            decimal currentTotalValue = Math.Max(0, product.StockQuantity) * product.CostPrice;
            int resultingQty = Math.Max(0, product.StockQuantity) + addedQty;
            decimal weightedAverageCost = (currentTotalValue + totalBatchCost) / resultingQty;

            product.CostPrice = Math.Round(weightedAverageCost, 2);
            product.StockQuantity += addedQty;

            if (newRetailPrice.HasValue && newRetailPrice.Value > 0)
            {
                if (newRetailPrice.Value < product.CostPrice)
                    throw new InvalidOperationException("Loss Prevention Alert: Target retail price is below the new blended wholesale cost.");

                product.SellingPrice = Math.Round(newRetailPrice.Value, 2);
            }

            _inventoryRepo.UpdateProduct(product);

            // Orchestrate Cross-Domain Action (Outflow)
            _financeService.RecordSystemInventoryCost(totalBatchCost, $"Restock: {barcode}");
        }

        // --- ARCHITECT NEW: Hardware-speed RTV Engine ---
        public void ReturnToVendor(string barcode, int returnQty, decimal refundAmount)
        {
            var product = _inventoryRepo.GetProductByBarcode(barcode.Trim());
            if (product == null) throw new InvalidOperationException("Unrecognized barcode. Product not found.");
            if (returnQty <= 0) throw new ArgumentException("Return quantity must be greater than zero.");
            if (product.StockQuantity < returnQty) throw new InvalidOperationException($"Insufficient stock. Current inventory holds {product.StockQuantity} units.");

            product.StockQuantity -= returnQty;
            _inventoryRepo.UpdateProduct(product);

            // Orchestrate Cross-Domain Action (Inflow)
            if (refundAmount > 0)
            {
                _financeService.RecordSystemVendorRefund(refundAmount, $"RTV: {barcode}");
            }
        }

        public IEnumerable<VendorDTO> GetAllVendors()
        {
            var vendors = _inventoryRepo.GetAllVendors();
            return vendors.Select(v => new VendorDTO { VendorId = v.VendorId, VendorName = v.VendorName }).ToList();
        }

        public void AddCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) throw new ArgumentException("Category name cannot be empty.");
            if (_inventoryRepo.CategoryExists(categoryName)) throw new InvalidOperationException("This category already exists.");
            _inventoryRepo.AddCategory(new Category { CategoryName = categoryName });
        }

        public void AddVendor(string vendorName)
        {
            if (string.IsNullOrWhiteSpace(vendorName)) throw new ArgumentException("Vendor name cannot be empty.");
            if (_inventoryRepo.VendorExists(vendorName)) throw new InvalidOperationException("This vendor already exists.");
            _inventoryRepo.AddVendor(new Vendor { VendorName = vendorName });
        }
    }
}