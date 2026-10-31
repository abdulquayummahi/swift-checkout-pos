// Project: SwiftCheckoutPOS.BLL
// File: DTOs/BackupConfigDTO.cs
using System;
using System.IO;

namespace SwiftCheckoutPOS.BLL.DTOs
{
    public class BackupConfigDTO
    {
        // ARCHITECT FIX: Dynamically maps to a "Backups" folder relative to the running executable
        public string TargetDirectory { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
        public bool IsAutoBackupEnabled { get; set; } = false;
        public TimeSpan AutoBackupTime { get; set; } = new TimeSpan(23, 0, 0);
    }

    public class BackupFileDTO
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public double FileSizeMB { get; set; }
        public DateTime CreationTime { get; set; }
    }
}