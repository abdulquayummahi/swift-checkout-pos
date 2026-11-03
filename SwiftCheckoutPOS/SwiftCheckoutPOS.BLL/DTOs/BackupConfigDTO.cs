// Project: SwiftCheckoutPOS.BLL
// File: DTOs/BackupConfigDTO.cs
using System;
using System.Collections.Generic;

namespace SwiftCheckoutPOS.BLL.DTOs
{
    /// <summary>BP-05/BP-12 Admin-only backup settings, stored in DataRoot\Config\backup_config.json.</summary>
    public class BackupConfigDTO
    {
        /// <summary>Folder for .bak sets. Empty means the default DataRoot\Backups. Must be outside the install folder.</summary>
        public string TargetDirectory { get; set; } = "";
        /// <summary>PR-Q53: automatic daily backup is on unless the Admin turns it off.</summary>
        public bool IsAutoBackupEnabled { get; set; } = true;
        /// <summary>PR-Q89: 23:00 shop time.</summary>
        public TimeSpan AutoBackupTime { get; set; } = new TimeSpan(23, 0, 0);
        /// <summary>PR-Q56: optional USB folder (for example E:\). When the drive is plugged in, each new backup is copied there.</summary>
        public string? UsbCopyDirectory { get; set; }
        /// <summary>Old folder found in a pre-upgrade backup_config.json that is no longer allowed (inside the install folder).</summary>
        public string? LegacyTargetDirectory { get; set; }
    }

    /// <summary>One backup set: Name.bak + Name.files.zip (images/settings, PR-Q57) + Name.json (this manifest).</summary>
    public class BackupManifestDTO
    {
        public string SetName { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Database { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = "";
        public string BackupFile { get; set; } = "";
        public long SizeBytes { get; set; }
        public string Sha256 { get; set; } = "";
        public string? FilesArchive { get; set; }
        public string? FilesSha256 { get; set; }
        public bool Verified { get; set; }
        public string? Error { get; set; }
        /// <summary>Not stored: true for an old .bak found without a manifest.</summary>
        public bool Legacy { get; set; }
        public double SizeMB => Math.Round(SizeBytes / 1048576.0, 2);
    }

    public class BackupStatusDTO
    {
        public DateTime? LastUsbCopyAt { get; set; }
        public string? LastUsbCopySet { get; set; }
        public string? LastUsbCopyDirectory { get; set; }
        public string? LastUsbError { get; set; }
        public DateTime? LastDrillAt { get; set; }
        public string? LastDrillSet { get; set; }
        public string? LastDrillResult { get; set; }
        public bool LastDrillPassed { get; set; }
    }

    public class BackupPageDTO
    {
        public BackupConfigDTO Config { get; set; } = new();
        public string EffectiveTarget { get; set; } = "";
        public BackupStatusDTO Status { get; set; } = new();
        public IReadOnlyList<BackupManifestDTO> History { get; set; } = Array.Empty<BackupManifestDTO>();
        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    }

    // Kept for older views/tests.
    public class BackupFileDTO
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public double FileSizeMB { get; set; }
        public DateTime CreationTime { get; set; }
    }
}
