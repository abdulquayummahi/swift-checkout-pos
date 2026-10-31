// Project: SwiftCheckoutPOS.UI
// File: Controllers/ReportAdminController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.Interfaces;
using SwiftCheckoutPOS.BLL.Services;
using System;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class ReportAdminController : Controller
    {
        private readonly ReportService _reportService;
        private readonly IReceiptPrinter _hardwarePrinter;

        public ReportAdminController(ReportService reportService, IReceiptPrinter hardwarePrinter)
        {
            _reportService = reportService;
            _hardwarePrinter = hardwarePrinter;
        }

        [HttpGet]
        public IActionResult Index(DateTime? startDate, DateTime? endDate, string searchQuery)
        {
            ViewBag.SearchQuery = searchQuery;
            var model = _reportService.GetAdvancedReport(startDate, endDate, searchQuery);
            return View(model);
        }

        // ARCHITECT FIX: Endpoint now accepts and processes the full date range
        [HttpGet]
        public IActionResult GetZReportPreview(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                DateTime start = startDate ?? DateTime.Today;
                DateTime end = endDate ?? DateTime.Today;

                string previewText = _reportService.GenerateZReportText(start, end);
                return Json(new { success = true, preview = previewText });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"SOFTWARE EXCEPTION: {ex.Message}" });
            }
        }

        // ARCHITECT FIX: Endpoint now accepts and processes the full date range
        [HttpPost]
        public IActionResult PrintZReport(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                if (!_hardwarePrinter.IsPrinterOnline())
                {
                    return Json(new { success = false, message = "HARDWARE FAULT: Printer offline or COM port inaccessible." });
                }

                DateTime start = startDate ?? DateTime.Today;
                DateTime end = endDate ?? DateTime.Today;

                var zReportDto = _reportService.GenerateZReport(start, end);
                _hardwarePrinter.PrintZReport(zReportDto);

                return Json(new { success = true, message = $"Report spanning {start:MMM dd} to {end:MMM dd} dispatched to printer." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"CRITICAL HARDWARE EXCEPTION: {ex.Message}" });
            }
        }
    }
}