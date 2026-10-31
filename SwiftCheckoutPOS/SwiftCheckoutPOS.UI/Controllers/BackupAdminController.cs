// Project: SwiftCheckoutPOS.UI
// File: Controllers/BackupAdminController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Services;
using System;
using System.IO;

namespace SwiftCheckoutPOS.UI.Controllers
{
    [Authorize(Roles = "Admin,Manager")]
    public class BackupAdminController : Controller
    {
        private readonly BackupService _backupService;

        public BackupAdminController(BackupService backupService)
        {
            _backupService = backupService;
        }

        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.Config = _backupService.GetConfiguration();
            var history = _backupService.GetBackupHistory();
            return View(history);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveConfiguration(BackupConfigDTO config)
        {
            try
            {
                _backupService.SaveConfiguration(config);
                TempData["Success"] = "Backup configuration successfully updated.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Configuration Fault: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TriggerManualBackup()
        {
            try
            {
                _backupService.ExecuteManualBackup();
                TempData["Success"] = "Manual database backup executed successfully.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Backup Generation Fault: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult DownloadBackup(string fileName)
        {
            var config = _backupService.GetConfiguration();
            string fullPath = Path.Combine(config.TargetDirectory, fileName);

            if (!System.IO.File.Exists(fullPath))
            {
                TempData["Error"] = "File no longer exists on physical disk.";
                return RedirectToAction(nameof(Index));
            }

            var fileBytes = System.IO.File.ReadAllBytes(fullPath);
            return File(fileBytes, "application/octet-stream", fileName);
        }
    }
}