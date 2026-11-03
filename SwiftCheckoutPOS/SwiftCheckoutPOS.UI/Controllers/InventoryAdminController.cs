// Project: SwiftCheckoutPOS.UI
// File: Controllers/InventoryAdminController.cs
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.UI.Infrastructure;
using SwiftCheckoutPOS.UI.Models;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class InventoryAdminController : Controller
    {
        private readonly InventoryService _inventoryService;
        private readonly SwiftCheckoutPOS.BLL.Media.ProductImageStore _images;

        private readonly SettingsService _settings;

        public InventoryAdminController(InventoryService inventoryService, SwiftCheckoutPOS.BLL.Media.ProductImageStore images, SettingsService settings)
        {
            _settings = settings;
            _inventoryService = inventoryService;
            _images = images;
        }

        [HttpGet]
        public IActionResult Index(string searchQuery, string sortOrder)
        {
            ViewBag.SearchQuery = searchQuery;
            var canFin = User.CanViewFinancials(_settings);
            ViewBag.CanViewFinancials = canFin;
            // PR-Q92: cost/margin values and sort keys never reach a Manager without financial view.
            if (!canFin && sortOrder is not null && (sortOrder.StartsWith("cost") || sortOrder.StartsWith("margin"))) sortOrder = null;
            ViewBag.SortOrder = sortOrder;
            var products = _inventoryService.GetAllSystemProducts(searchQuery, sortOrder).ToList();
            if (!canFin) foreach (var p in products) { p.CostPrice = 0; p.MarginPercentage = 0; }
            return View(products);
        }

        // IMM-10: validated store with generated names (no barcode paths or wildcards). Returns null when no file was sent.
        private string? ProcessUploadedImage(IFormFile? imageFile, string barcode)
        {
            if (imageFile == null || imageFile.Length == 0) return null;
            if (imageFile.Length > SwiftCheckoutPOS.BLL.Media.ProductImageStore.MaxBytes) throw new InvalidOperationException("The image is larger than 2 MB.");
            using var stream = imageFile.OpenReadStream();
            return _images.Save(stream, imageFile.FileName, barcode);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model = new CreateProductViewModel { AvailableCategories = GetCategorySelectList(), AvailableVendors = GetVendorSelectList() };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CreateProductViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableCategories = GetCategorySelectList();
                model.AvailableVendors = GetVendorSelectList();
                return View(model);
            }

            string? imagePath = null;
            try
            {
                imagePath = ProcessUploadedImage(model.ImageFile, model.Barcode);

                var dto = new ProductDTO
                {
                    Barcode = model.Barcode,
                    ProductName = model.ProductName,
                    VendorId = model.VendorId,
                    CostPrice = model.CostPrice,
                    SellingPrice = model.SellingPrice,
                    StockQuantity = model.StockQuantity,
                    TaxRate = model.TaxRate,
                    UnitOfMeasure = model.UnitOfMeasure,
                    ReorderLevel = model.ReorderLevel,
                    Description = model.Description,
                    ImagePath = imagePath
                };

                _inventoryService.AddNewProduct(dto, model.CategoryId, User.Actor());
                TempData["Success"] = "Product successfully added to master inventory.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _images.Remove(imagePath); // the product was not saved: do not leave an orphan file
                ModelState.AddModelError(string.Empty, ex.Message);
                model.AvailableCategories = GetCategorySelectList();
                model.AvailableVendors = GetVendorSelectList();
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            try
            {
                var dto = _inventoryService.GetProductForEdit(id);
                var model = new EditProductViewModel
                {
                    ProductId = dto.ProductId,
                    Barcode = dto.Barcode,
                    ProductName = dto.ProductName,
                    CategoryId = dto.CategoryId,
                    VendorId = dto.VendorId,
                    CostPrice = User.CanViewFinancials(_settings) ? dto.CostPrice : null, // CQ-03: never shown to Manager without view
                    SellingPrice = dto.SellingPrice,
                    StockQuantity = dto.StockQuantity,
                    RowVersion = dto.RowVersion,
                    TaxRate = dto.TaxRate,
                    UnitOfMeasure = dto.UnitOfMeasure,
                    ReorderLevel = dto.ReorderLevel,
                    Description = dto.Description,
                    ExistingImagePath = dto.ImagePath,
                    IsActive = dto.IsActive,
                    AvailableCategories = GetCategorySelectList(),
                    AvailableVendors = GetVendorSelectList()
                };
                return View(model);
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(EditProductViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableCategories = GetCategorySelectList();
                model.AvailableVendors = GetVendorSelectList();
                return View(model);
            }

            string? newImagePath = null;
            try
            {
                // The old file name comes from the database, never from the posted hidden field.
                var previousImage = _inventoryService.GetProductForEdit(model.ProductId).ImagePath;
                newImagePath = ProcessUploadedImage(model.ImageFile, model.Barcode);

                var dto = new ProductDTO
                {
                    ProductId = model.ProductId,
                    Barcode = model.Barcode,
                    ProductName = model.ProductName,
                    VendorId = model.VendorId,
                    CostPrice = model.CostPrice ?? 0,
                    KeepCost = model.CostPrice is null, // write-only cost: empty keeps the current cost
                    SellingPrice = model.SellingPrice,
                    RowVersion = model.RowVersion,
                    TaxRate = model.TaxRate,
                    UnitOfMeasure = model.UnitOfMeasure,
                    ReorderLevel = model.ReorderLevel,
                    Description = model.Description,
                    ImagePath = newImagePath,
                    IsActive = model.IsActive
                };

                _inventoryService.UpdateProduct(dto, model.CategoryId, User.Actor());
                if (newImagePath is not null && newImagePath != previousImage) _images.Remove(previousImage);
                TempData["Success"] = "Product details successfully updated.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _images.Remove(newImagePath); // save failed: keep the old picture, drop the new upload
                ModelState.AddModelError(string.Empty, ex.Message);
                model.AvailableCategories = GetCategorySelectList();
                model.AvailableVendors = GetVendorSelectList();
                return View(model);
            }
        }

        // --- ARCHITECT NEW: High-Speed Operations ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProcessRestock(string barcode, int addedQty, decimal totalBatchCost, decimal? newRetailPrice, string? zeroCostReason)
        {
            try
            {
                _inventoryService.RestockProduct(barcode, addedQty, totalBatchCost, newRetailPrice, User.Actor(), zeroCostReason);
                TempData["Success"] = $"Restock successful. Moving Average Cost calculated and Ledger updated.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Restock Failed: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProcessReturnToVendor(string barcode, int returnQty, decimal refundAmount)
        {
            try
            {
                _inventoryService.ReturnToVendor(barcode, returnQty, refundAmount, User.Actor());
                TempData["Success"] = $"RTV successful. Inventory deducted and Ledger credited.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Return Failed: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult QuickAddCategory([FromForm] string categoryName)
        {
            try { _inventoryService.AddCategory(categoryName); return Json(new { success = true }); }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        [HttpPost]
        public IActionResult QuickAddVendor([FromForm] string vendorName)
        {
            try { _inventoryService.AddVendor(vendorName); return Json(new { success = true }); }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        /// <summary>PR-Q23/82: stock correction to a counted quantity, with reason (Manager/Admin via class role gate).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AdjustStock(string barcode, int countedQuantity, string reason, string? note)
        {
            try
            {
                _inventoryService.AdjustStock(barcode ?? "", countedQuantity, reason ?? "", note, User.Actor());
                TempData["Success"] = "Stock corrected and recorded.";
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        private SelectList GetCategorySelectList() => new SelectList(_inventoryService.GetAllCategories(), "CategoryId", "CategoryName");
        private SelectList GetVendorSelectList() => new SelectList(_inventoryService.GetAllVendors(), "VendorId", "VendorName");
    }
}