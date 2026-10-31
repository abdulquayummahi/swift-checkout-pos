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
using SwiftCheckoutPOS.UI.Models;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class InventoryAdminController : Controller
    {
        private readonly InventoryService _inventoryService;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public InventoryAdminController(InventoryService inventoryService, IWebHostEnvironment webHostEnvironment)
        {
            _inventoryService = inventoryService;
            _webHostEnvironment = webHostEnvironment;
        }

        [HttpGet]
        public IActionResult Index(string searchQuery, string sortOrder)
        {
            ViewBag.SearchQuery = searchQuery;
            ViewBag.SortOrder = sortOrder;

            var products = _inventoryService.GetAllSystemProducts(searchQuery, sortOrder);
            return View(products);
        }

        private async Task<string> ProcessUploadedImage(IFormFile imageFile, string barcode)
        {
            if (imageFile == null || imageFile.Length == 0) return null;

            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp") { ext = ".jpg"; }

            string standardizedFileName = $"{barcode}{ext}";
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");

            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var existingFiles = Directory.GetFiles(uploadsFolder, $"{barcode}.*");
            foreach (var file in existingFiles) System.IO.File.Delete(file);

            string filePath = Path.Combine(uploadsFolder, standardizedFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            return standardizedFileName;
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model = new CreateProductViewModel { AvailableCategories = GetCategorySelectList(), AvailableVendors = GetVendorSelectList() };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateProductViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableCategories = GetCategorySelectList();
                model.AvailableVendors = GetVendorSelectList();
                return View(model);
            }

            try
            {
                string? imagePath = await ProcessUploadedImage(model.ImageFile, model.Barcode);

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

                _inventoryService.AddNewProduct(dto, model.CategoryId);
                TempData["Success"] = "Product successfully added to master inventory.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
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
                    CostPrice = dto.CostPrice,
                    SellingPrice = dto.SellingPrice,
                    StockQuantity = dto.StockQuantity,
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
        public async Task<IActionResult> Edit(EditProductViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableCategories = GetCategorySelectList();
                model.AvailableVendors = GetVendorSelectList();
                return View(model);
            }

            try
            {
                string? newImagePath = await ProcessUploadedImage(model.ImageFile, model.Barcode);

                var dto = new ProductDTO
                {
                    ProductId = model.ProductId,
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
                    ImagePath = newImagePath,
                    IsActive = model.IsActive
                };

                _inventoryService.UpdateProduct(dto, model.CategoryId);
                TempData["Success"] = "Product details successfully updated.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                model.AvailableCategories = GetCategorySelectList();
                model.AvailableVendors = GetVendorSelectList();
                return View(model);
            }
        }

        // --- ARCHITECT NEW: High-Speed Operations ---
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ProcessRestock(string barcode, int addedQty, decimal totalBatchCost, decimal? newRetailPrice)
        {
            try
            {
                _inventoryService.RestockProduct(barcode, addedQty, totalBatchCost, newRetailPrice);
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
                _inventoryService.ReturnToVendor(barcode, returnQty, refundAmount);
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

        private SelectList GetCategorySelectList() => new SelectList(_inventoryService.GetAllCategories(), "CategoryId", "CategoryName");
        private SelectList GetVendorSelectList() => new SelectList(_inventoryService.GetAllVendors(), "VendorId", "VendorName");
    }
}