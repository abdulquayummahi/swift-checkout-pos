// Project: SwiftCheckoutPOS.UI
// File: Controllers/BackupAdminController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.UI.Infrastructure;
using System;

namespace SwiftCheckoutPOS.UI.Controllers
{
    /// <summary>
    /// PR-Q50/Q92: backup, download and restore are Admin-only. Restore itself is an owner maintenance command run
    /// with the program stopped (DATA_RECOVERY_PLAN.md); this page shows status, makes and copies backups.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class BackupAdminController : Controller
    {
        private readonly BackupService _backupService;

        public BackupAdminController(BackupService backupService)
        {
            _backupService = backupService;
        }

        [HttpGet]
        public IActionResult Index() => View(_backupService.GetPage());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveConfiguration(string? targetDirectory, bool isAutoBackupEnabled, string? autoBackupTime, string? usbCopyDirectory)
        {
            try
            {
                if (!TimeSpan.TryParse(autoBackupTime, System.Globalization.CultureInfo.InvariantCulture, out var time))
                    throw new InvalidOperationException("Enter the backup time as HH:mm, for example 23:00.");
                _backupService.SaveConfiguration(new BackupConfigDTO
                {
                    TargetDirectory = targetDirectory ?? "", IsAutoBackupEnabled = isAutoBackupEnabled,
                    AutoBackupTime = time, UsbCopyDirectory = usbCopyDirectory
                }, User.Actor());
                TempData["Success"] = "Backup settings saved.";
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            catch (Exception) { TempData["Error"] = "The backup settings could not be saved. Check the folder and its permissions."; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TriggerManualBackup()
        {
            try
            {
                var set = _backupService.RunBackup("Manual", User.Actor());
                TempData["Success"] = $"Backup {set.SetName} made and verified ({set.SizeMB} MB).";
            }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CopyToUsb(string? usbDirectory)
        {
            try { TempData["Success"] = _backupService.CopyLatestToUsb(usbDirectory, User.Actor()); }
            catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult DownloadBackup(string fileName)
        {
            // IMM-10: only exact backup file names inside the backup folder; no paths, wildcards or other files.
            var fullPath = _backupService.OpenBackupFile(fileName);
            if (fullPath is null)
            {
                TempData["Error"] = "That backup file is not available.";
                return RedirectToAction(nameof(Index));
            }
            _backupService.RecordDownload(System.IO.Path.GetFileName(fullPath), User.Actor());
            return PhysicalFile(fullPath, "application/octet-stream", System.IO.Path.GetFileName(fullPath));
        }
    }
}
