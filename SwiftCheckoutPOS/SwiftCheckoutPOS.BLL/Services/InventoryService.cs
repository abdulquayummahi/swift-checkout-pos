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
        private readonly AtomicUnit _unit;

        /// <summary>PR-Q82 stock-correction reasons.</summary>
        public static readonly string[] StockAdjustmentReasons = ["Count correction", "Damaged", "Expired", "Theft/Loss", "Found", "Other"];

        public InventoryService(InventoryRepository inventoryRepo, FinanceService financeService, AtomicUnit unit)
        {
            _inventoryRepo = inventoryRepo;
            _unit = unit;
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
                CostPrice = p.CostPrice,
                SellingPrice = p.SellingPrice,
                MarginPercentage = p.SellingPrice > 0 ? ((p.SellingPrice - p.CostPrice) / p.SellingPrice) * 100 : 0,
                StockQuantity = p.StockQuantity,
                ReservedQuantity = p.ReservedQuantity,
                IsActive = p.IsActive,
                ImagePath = p.ImagePath,
                TaxRate = p.TaxRate,
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
                CostPrice = p.CostPrice,
                SellingPrice = p.SellingPrice,
                TaxRate = p.TaxRate,
                StockQuantity = p.StockQuantity,
                UnitOfMeasure = p.UnitOfMeasure,
                ReorderLevel = p.ReorderLevel,
                Description = p.Description,
                ImagePath = p.ImagePath,
                IsActive = p.IsActive,
                RowVersion = Convert.ToBase64String(p.RowVersion ?? [])
            };
        }

        public void AddNewProduct(ProductDTO newProductDto, int categoryId, string actor = "SYSTEM")
        {
            ValidateMoney(newProductDto.CostPrice, newProductDto.SellingPrice, newProductDto.TaxRate);
            if (newProductDto.StockQuantity < 0) throw new ArgumentException("Opening stock cannot be negative.");
            if (newProductDto.SellingPrice < newProductDto.CostPrice) throw new ArgumentException("Selling price cannot be lower than wholesale cost price.");

            // T06: product, opening-stock ledger entry and audit are saved together or not at all.
            _unit.Run(() =>
            {
                if (_inventoryRepo.GetProductByBarcode(newProductDto.Barcode) != null)
                    throw new InvalidOperationException($"Barcode '{newProductDto.Barcode}' is already in use.");
                var entity = new Product
                {
                    Barcode = newProductDto.Barcode,
                    ProductName = newProductDto.ProductName,
                    CategoryId = categoryId,
                    VendorId = newProductDto.VendorId,
                    TaxRate = Pricing.Money.R2(newProductDto.TaxRate),
                    UnitOfMeasure = newProductDto.UnitOfMeasure,
                    ReorderLevel = newProductDto.ReorderLevel,
                    Description = newProductDto.Description,
                    ImagePath = newProductDto.ImagePath,
                    CostPrice = Pricing.Money.R2(newProductDto.CostPrice),
                    SellingPrice = Pricing.Money.R2(newProductDto.SellingPrice),
                    StockQuantity = newProductDto.StockQuantity,
                    IsActive = true
                };
                _inventoryRepo.AddProduct(entity);
                if (entity.StockQuantity > 0)
                    _financeService.RecordSystemInventoryCost(Pricing.Money.R2(entity.StockQuantity * entity.CostPrice), $"Initial Stock - {entity.Barcode}");
                _unit.Audit(actor, "ProductCreated", "Product", entity.Barcode,
                    details: $"Cost {entity.CostPrice:0.00}; price {entity.SellingPrice:0.00}; VAT {entity.TaxRate:0.00}%; opening stock {entity.StockQuantity}");
            });
        }

        /// <summary>
        /// Edits product details and prices. IMM-06: stock is NOT changed here (use AdjustStock with a reason),
        /// and the form's original RowVersion must still match, so a stale form cannot overwrite newer data.
        /// </summary>
        public void UpdateProduct(ProductDTO dto, int categoryId, string actor = "SYSTEM")
        {
            ValidateMoney(dto.CostPrice, dto.SellingPrice, dto.TaxRate);
            if (dto.SellingPrice < dto.CostPrice) throw new ArgumentException("Loss Prevention: Selling price cannot be lower than cost price.");
            try
            {
                _unit.Run(() =>
                {
                    var entity = _inventoryRepo.GetProductById(dto.ProductId) ?? throw new InvalidOperationException("Product record missing.");
                    if (!string.IsNullOrEmpty(dto.RowVersion))
                        _inventoryRepo.ExpectRowVersion(entity, Convert.FromBase64String(dto.RowVersion));
                    if (entity.Barcode != dto.Barcode && _inventoryRepo.GetProductByBarcode(dto.Barcode) != null)
                        throw new InvalidOperationException($"Barcode '{dto.Barcode}' is already assigned.");

                    var newCost = Pricing.Money.R2(dto.CostPrice);
                    var newPrice = Pricing.Money.R2(dto.SellingPrice);
                    var newTax = Pricing.Money.R2(dto.TaxRate);
                    if (entity.CostPrice != newCost || entity.SellingPrice != newPrice || entity.TaxRate != newTax)
                        _unit.Audit(actor, "PriceChange", "Product", entity.Barcode, details:
                            $"Cost {entity.CostPrice:0.00}->{newCost:0.00}; price {entity.SellingPrice:0.00}->{newPrice:0.00}; VAT {entity.TaxRate:0.00}->{newTax:0.00}");

                    entity.Barcode = dto.Barcode; entity.ProductName = dto.ProductName; entity.CategoryId = categoryId;
                    entity.VendorId = dto.VendorId; entity.CostPrice = newCost; entity.SellingPrice = newPrice; entity.TaxRate = newTax;
                    entity.UnitOfMeasure = dto.UnitOfMeasure; entity.ReorderLevel = dto.ReorderLevel;
                    entity.Description = dto.Description; entity.IsActive = dto.IsActive;
                    if (!string.IsNullOrWhiteSpace(dto.ImagePath)) entity.ImagePath = dto.ImagePath;
                    _inventoryRepo.UpdateProduct(entity);
                });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("This product was changed by someone else (for example a sale or restock). Reopen it and try again.");
            }
        }

        /// <summary>
        /// PR-Q23/24/82: Manager/Admin stock correction to a counted quantity, with a required reason.
        /// Decrease → "Damaged" (Damaged/Expired) or "Inventory Adjustment" expense at current average cost;
        /// increase → "Inventory Adjustment" gain at current average cost. Non-cash entries (CQ-07).
        /// </summary>
        public void AdjustStock(string barcode, int countedQuantity, string reason, string? note, string actor)
        {
            if (countedQuantity < 0) throw new ArgumentException("Counted quantity cannot be negative.");
            if (!StockAdjustmentReasons.Contains(reason)) throw new ArgumentException("Choose a stock correction reason.");
            if (reason == "Other" && string.IsNullOrWhiteSpace(note)) throw new ArgumentException("Add a note for 'Other'.");
            if (note?.Length > 150) throw new ArgumentException("Keep the note under 150 characters.");
            _unit.Run(() =>
            {
                var product = _inventoryRepo.GetProductByBarcode(barcode.Trim()) ?? throw new InvalidOperationException("Unrecognized barcode. Product not found.");
                if (countedQuantity < product.ReservedQuantity)
                    throw new InvalidOperationException($"{product.ReservedQuantity} unit(s) are reserved by held orders. Cancel or complete them first, or count them too.");
                var delta = countedQuantity - product.StockQuantity;
                if (delta == 0) throw new InvalidOperationException("The counted quantity equals the current stock. Nothing to correct.");
                var value = Pricing.Money.R2(Math.Abs(delta) * product.CostPrice);
                product.StockQuantity = countedQuantity;
                _inventoryRepo.UpdateProduct(product);
                if (value > 0)
                {
                    var category = delta < 0 && (reason == "Damaged" || reason == "Expired") ? "Damaged" : "Inventory Adjustment";
                    _financeService.RecordSystemEntry(category, delta < 0 ? "Outflow" : "Inflow", value,
                        $"Stock correction ({reason}) {product.Barcode}: {delta:+#;-#} units", actor);
                }
                _unit.Audit(actor, "StockAdjustment", "Product", product.Barcode, reason,
                    $"{product.StockQuantity - delta} -> {countedQuantity} ({delta:+#;-#}); value {value:0.00}" + (string.IsNullOrWhiteSpace(note) ? "" : $"; note: {note.Trim()}"));
            });
        }

        /// <summary>Moving weighted-average restock (PR-Q19/21). Zero-cost (free/bonus) stock needs a reason (PR-Q20).</summary>
        public void RestockProduct(string barcode, int addedQty, decimal totalBatchCost, decimal? newRetailPrice, string actor = "SYSTEM", string? zeroCostReason = null)
        {
            if (addedQty <= 0) throw new ArgumentException("Restock quantity must be greater than zero.");
            if (totalBatchCost < 0) throw new ArgumentException("Batch cost cannot be negative.");
            if (Pricing.Money.R2(totalBatchCost) != totalBatchCost) throw new ArgumentException("Batch cost can have at most 2 decimals.");
            if (totalBatchCost == 0 && string.IsNullOrWhiteSpace(zeroCostReason))
                throw new ArgumentException("A zero-cost restock is only for free/bonus stock: enter the reason.");
            _unit.Run(() =>
            {
                var product = _inventoryRepo.GetProductByBarcode(barcode.Trim()) ?? throw new InvalidOperationException("Unrecognized barcode. Product not found.");
                decimal currentTotalValue = Math.Max(0, product.StockQuantity) * product.CostPrice;
                int resultingQty = Math.Max(0, product.StockQuantity) + addedQty;
                product.CostPrice = Pricing.Money.R2((currentTotalValue + totalBatchCost) / resultingQty);
                product.StockQuantity += addedQty;

                if (newRetailPrice.HasValue && newRetailPrice.Value > 0)
                {
                    if (newRetailPrice.Value < product.CostPrice)
                        throw new InvalidOperationException("Loss Prevention Alert: Target retail price is below the new blended wholesale cost.");
                    var price = Pricing.Money.R2(newRetailPrice.Value);
                    if (price != product.SellingPrice)
                        _unit.Audit(actor, "PriceChange", "Product", product.Barcode, details: $"Price {product.SellingPrice:0.00}->{price:0.00} (restock)");
                    product.SellingPrice = price;
                }
                _inventoryRepo.UpdateProduct(product);
                if (totalBatchCost > 0) _financeService.RecordSystemInventoryCost(totalBatchCost, $"Restock: {barcode}", actor);
                _unit.Audit(actor, "Restock", "Product", product.Barcode, totalBatchCost == 0 ? zeroCostReason!.Trim() : null,
                    $"+{addedQty} units; batch cost {totalBatchCost:0.00}");
            });
        }

        /// <summary>Return to vendor. No cash received → stock and value reduced, "vendor credit pending" noted (PR-Q25).</summary>
        public void ReturnToVendor(string barcode, int returnQty, decimal refundAmount, string actor = "SYSTEM")
        {
            if (returnQty <= 0) throw new ArgumentException("Return quantity must be greater than zero.");
            if (refundAmount < 0 || Pricing.Money.R2(refundAmount) != refundAmount) throw new ArgumentException("Enter a valid refund amount.");
            _unit.Run(() =>
            {
                var product = _inventoryRepo.GetProductByBarcode(barcode.Trim()) ?? throw new InvalidOperationException("Unrecognized barcode. Product not found.");
                var available = product.StockQuantity - product.ReservedQuantity;
                if (available < returnQty) throw new InvalidOperationException($"Insufficient stock. {available} unit(s) available ({product.ReservedQuantity} reserved by held orders).");
                product.StockQuantity -= returnQty;
                _inventoryRepo.UpdateProduct(product);
                if (refundAmount > 0) _financeService.RecordSystemVendorRefund(refundAmount, $"RTV: {barcode}", actor);
                _unit.Audit(actor, "ReturnToVendor", "Product", product.Barcode, refundAmount > 0 ? null : "Vendor credit pending",
                    $"-{returnQty} units; refund received {refundAmount:0.00}");
            });
        }

        private static void ValidateMoney(decimal cost, decimal price, decimal tax)
        {
            if (cost < 0 || price < 0 || Pricing.Money.R2(cost) != cost || Pricing.Money.R2(price) != price)
                throw new ArgumentException("Prices must be zero or more, with at most 2 decimals.");
            if (tax < 0 || tax > 100 || Pricing.Money.R2(tax) != tax)
                throw new ArgumentException("VAT rate must be 0–100 with at most 2 decimals.");
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