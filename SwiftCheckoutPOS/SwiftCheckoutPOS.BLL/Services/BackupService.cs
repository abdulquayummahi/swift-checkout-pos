// Project: SwiftCheckoutPOS.BLL
// File: Services/BackupService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using SwiftCheckoutPOS.BLL.Configuration;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services
{
    /// <summary>
    /// IMM-10 / BP-05 / BP-12 (T08, T09): verified backup sets, PR-Q55 retention, PR-Q53/Q89 23:00 schedule with
    /// catch-up, PR-Q56 USB copy, PR-Q84 restore drill and the owner-only in-place restore with a safety backup.
    /// The record of what exists lives in manifest files next to the backups, not in the database being protected.
    /// </summary>
    public class BackupService
    {
        public const string Prefix = "SwiftCheckoutPOS_";
        public static readonly string[] Kinds = ["Daily", "Manual", "PreUpgrade", "PreRestore"];
        private static readonly Regex SetPattern = new(@"^SwiftCheckoutPOS_\d{8}_\d{6}_(Daily|Manual|PreUpgrade|PreRestore)$", RegexOptions.CultureInvariant);
        private static readonly Regex LegacyPattern = new(@"^SwiftCheckoutPOS_Backup_\d{8}_\d{6}\.bak$", RegexOptions.CultureInvariant);
        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

        private readonly BackupRepository _repo;
        private readonly DataPaths _paths;
        private readonly AtomicUnit? _audit;
        private readonly Func<DateTime> _clock;
        private readonly IReadOnlyList<string> _extraMedia;

        public BackupService(BackupRepository repo, DataPaths paths, AtomicUnit? audit = null,
            Func<DateTime>? clock = null, IEnumerable<string>? extraMediaFolders = null)
        {
            _repo = repo;
            _paths = paths;
            _audit = audit;
            _clock = clock ?? (() => DateTime.Now);
            _extraMedia = (extraMediaFolders ?? Array.Empty<string>()).ToList();
        }

        private string ConfigFile => Path.Combine(_paths.Config, "backup_config.json");
        private string StatusFile => Path.Combine(_paths.Config, "backup_status.json");
        private string LegacyConfigFile => Path.Combine(_paths.InstallDirectory, "backup_config.json");

        // ---------------- settings (BP-12) ----------------

        public BackupConfigDTO GetConfiguration()
        {
            if (TryRead<BackupConfigDTO>(ConfigFile) is { } saved) return saved;
            var config = new BackupConfigDTO();
            // Preserve a pre-upgrade backup_config.json (left untouched in the install folder).
            if (TryRead<BackupConfigDTO>(LegacyConfigFile) is { } legacy)
            {
                config.IsAutoBackupEnabled = legacy.IsAutoBackupEnabled;
                config.AutoBackupTime = legacy.AutoBackupTime;
                if (!string.IsNullOrWhiteSpace(legacy.TargetDirectory))
                {
                    if (Validate(new BackupConfigDTO { TargetDirectory = legacy.TargetDirectory, AutoBackupTime = legacy.AutoBackupTime }).Count == 0)
                        config.TargetDirectory = legacy.TargetDirectory;
                    else
                        config.LegacyTargetDirectory = legacy.TargetDirectory;
                }
            }
            return config;
        }

        public string EffectiveTarget(BackupConfigDTO config) =>
            string.IsNullOrWhiteSpace(config.TargetDirectory) ? _paths.Backups : Path.GetFullPath(config.TargetDirectory);

        public IReadOnlyList<string> Validate(BackupConfigDTO config)
        {
            var errors = new List<string>();
            if (config.AutoBackupTime < TimeSpan.Zero || config.AutoBackupTime >= TimeSpan.FromDays(1))
                errors.Add("Backup time must be between 00:00 and 23:59.");
            if (!string.IsNullOrWhiteSpace(config.TargetDirectory))
            {
                if (!IsFullLocalPath(config.TargetDirectory)) errors.Add("Backup folder must be a full path such as D:\\POSBackups.");
                else if (DataPaths.IsInside(config.TargetDirectory, _paths.InstallDirectory))
                    errors.Add("Backup folder must not be inside the program folder (an upgrade would remove it).");
            }
            if (!string.IsNullOrWhiteSpace(config.UsbCopyDirectory))
            {
                if (!IsFullLocalPath(config.UsbCopyDirectory)) errors.Add("USB folder must be a full path such as E:\\.");
                else if (DataPaths.IsInside(config.UsbCopyDirectory, EffectiveTarget(config)) || DataPaths.IsInside(EffectiveTarget(config), config.UsbCopyDirectory!))
                    errors.Add("USB folder must be different from the backup folder.");
            }
            return errors;
        }

        private static bool IsFullLocalPath(string path)
        {
            if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || path.Contains('*') || path.Contains('?')) return false;
            if (path.StartsWith(@"\\", StringComparison.Ordinal)) return false; // no network shares: offline, one PC
            return Path.IsPathFullyQualified(path);
        }

        public void SaveConfiguration(BackupConfigDTO config, string actor = "SYSTEM")
        {
            config.TargetDirectory = (config.TargetDirectory ?? "").Trim();
            config.UsbCopyDirectory = string.IsNullOrWhiteSpace(config.UsbCopyDirectory) ? null : config.UsbCopyDirectory.Trim();
            var errors = Validate(config);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
            var target = EffectiveTarget(config);
            Directory.CreateDirectory(target);
            // Permission check: this account must be able to read/write the folder (the SQL Server service writes the .bak).
            var probe = Path.Combine(target, $".write-test-{Guid.NewGuid():N}");
            try { File.WriteAllText(probe, "ok"); File.Delete(probe); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { throw new InvalidOperationException("The backup folder cannot be written. Check its Windows permissions."); }
            config.LegacyTargetDirectory = GetConfiguration().LegacyTargetDirectory;
            Write(ConfigFile, config);
            Audit(actor, "BackupSettingsChanged", "Settings", "Backup",
                $"Folder={target}; Auto={config.IsAutoBackupEnabled}; Time={config.AutoBackupTime:hh\\:mm}; USB={config.UsbCopyDirectory ?? "-"}");
        }

        // ---------------- backup ----------------

        /// <summary>Creates, verifies and records one backup set, then applies retention and the optional USB copy.</summary>
        public BackupManifestDTO RunBackup(string kind, string actor, bool applyRetention = true)
        {
            if (!Kinds.Contains(kind)) throw new ArgumentException("Unknown backup kind.", nameof(kind));
            var config = GetConfiguration();
            var errors = Validate(config);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
            var target = EffectiveTarget(config);
            Directory.CreateDirectory(target);

            var at = _clock();
            string set;
            do { set = $"{Prefix}{at:yyyyMMdd_HHmmss}_{kind}"; at = at.AddSeconds(1); }
            while (File.Exists(Path.Combine(target, set + ".json")) || File.Exists(Path.Combine(target, set + ".bak")));

            var manifest = new BackupManifestDTO
            {
                SetName = set, Kind = kind, Database = _repo.DatabaseName, CreatedAt = _clock(), CreatedBy = actor,
                BackupFile = set + ".bak"
            };
            var bak = Path.Combine(target, manifest.BackupFile);
            var zip = Path.Combine(target, set + ".files.zip");
            try
            {
                _repo.BackupDatabase(bak, set);
                _repo.VerifyBackup(bak);
                manifest.SizeBytes = new FileInfo(bak).Length;
                manifest.Sha256 = Hash(bak);
                if (CreateFilesArchive(zip))
                {
                    manifest.FilesArchive = Path.GetFileName(zip);
                    manifest.FilesSha256 = Hash(zip);
                }
                manifest.Verified = true;
            }
            catch (Exception ex)
            {
                TryDelete(bak);
                TryDelete(zip);
                manifest.Error = Short(ex);
                Write(Path.Combine(target, set + ".json"), manifest);
                Audit(actor, "BackupFailed", "Backup", set, manifest.Error);
                throw new InvalidOperationException("Backup failed: " + manifest.Error);
            }
            Write(Path.Combine(target, set + ".json"), manifest);
            Audit(actor, "Backup", "Backup", set, $"{kind}; {manifest.SizeMB} MB; verified");
            if (applyRetention) ApplyRetention(target);
            if (!string.IsNullOrWhiteSpace(config.UsbCopyDirectory) && Directory.Exists(Path.GetPathRoot(config.UsbCopyDirectory)))
            {
                try { CopyLatestToUsb(config.UsbCopyDirectory, actor); }
                catch (InvalidOperationException) { /* recorded in status; the backup itself succeeded */ }
            }
            return manifest;
        }

        /// <summary>Kept for the old Admin button name.</summary>
        public string ExecuteManualBackup(string actor = "SYSTEM") => RunBackup("Manual", actor).SetName;

        private bool CreateFilesArchive(string zipPath)
        {
            var sources = new List<(string Folder, string Prefix)>();
            if (Directory.Exists(_paths.Media)) sources.Add((_paths.Media, "Media"));
            if (Directory.Exists(_paths.Config)) sources.Add((_paths.Config, "Config"));
            for (var i = 0; i < _extraMedia.Count; i++)
                if (Directory.Exists(_extraMedia[i])) sources.Add((_extraMedia[i], $"LegacyImages{(i == 0 ? "" : i.ToString())}"));
            var files = sources.SelectMany(s => Directory.EnumerateFiles(s.Folder, "*", SearchOption.AllDirectories)
                .Select(f => (File: f, Entry: s.Prefix + "/" + Path.GetRelativePath(s.Folder, f).Replace('\\', '/')))).ToList();
            if (files.Count == 0) return false;
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            foreach (var (file, entry) in files)
            {
                try
                {
                    using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var destination = archive.CreateEntry(entry, CompressionLevel.Optimal).Open();
                    source.CopyTo(destination);
                }
                catch (IOException) { /* a file in use (for example a status file being written) is skipped */ }
            }
            return true;
        }

        // ---------------- history and retention (PR-Q55) ----------------

        public IReadOnlyList<BackupManifestDTO> GetHistory() => ReadSets(EffectiveTarget(GetConfiguration()));

        private static IReadOnlyList<BackupManifestDTO> ReadSets(string folder)
        {
            if (!Directory.Exists(folder)) return Array.Empty<BackupManifestDTO>();
            var sets = new List<BackupManifestDTO>();
            foreach (var json in Directory.EnumerateFiles(folder, Prefix + "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(json);
                if (!SetPattern.IsMatch(name)) continue;
                if (TryRead<BackupManifestDTO>(json) is { } m && m.SetName == name) sets.Add(m);
            }
            foreach (var bak in Directory.EnumerateFiles(folder, "*.bak"))
            {
                var file = Path.GetFileName(bak);
                if (!LegacyPattern.IsMatch(file)) continue;
                var info = new FileInfo(bak);
                sets.Add(new BackupManifestDTO { SetName = Path.GetFileNameWithoutExtension(file), Kind = "Old", BackupFile = file,
                    SizeBytes = info.Length, CreatedAt = info.LastWriteTime, Legacy = true, Error = "Older backup without verification record" });
            }
            return sets.OrderByDescending(s => s.CreatedAt).ToList();
        }

        /// <summary>
        /// PR-Q55: keep every verified set from the 30 most recent backup days, the first verified set of each of the
        /// 12 most recent months, and always the newest verified set. Old failure records go after 30 days.
        /// Older un-verified .bak files from before this version are never deleted automatically.
        /// </summary>
        public static IReadOnlyList<BackupManifestDTO> SelectForDeletion(IEnumerable<BackupManifestDTO> sets)
        {
            var all = sets.Where(s => !s.Legacy).ToList();
            var verified = all.Where(s => s.Verified).OrderByDescending(s => s.CreatedAt).ToList();
            if (verified.Count == 0) return Array.Empty<BackupManifestDTO>();
            var keep = new HashSet<string> { verified[0].SetName };
            var days = verified.Select(s => s.CreatedAt.Date).Distinct().OrderByDescending(d => d).Take(30).ToHashSet();
            foreach (var s in verified.Where(s => days.Contains(s.CreatedAt.Date))) keep.Add(s.SetName);
            foreach (var first in verified.GroupBy(s => (s.CreatedAt.Year, s.CreatedAt.Month)).OrderByDescending(g => g.Key).Take(12)
                         .Select(g => g.OrderBy(s => s.CreatedAt).First()))
                keep.Add(first.SetName);
            var failureCutoff = verified[0].CreatedAt.AddDays(-30);
            return all.Where(s => s.Verified ? !keep.Contains(s.SetName) : s.CreatedAt < failureCutoff).ToList();
        }

        private void ApplyRetention(string folder)
        {
            foreach (var set in SelectForDeletion(ReadSets(folder)))
            {
                if (!SetPattern.IsMatch(set.SetName)) continue;
                foreach (var suffix in new[] { ".bak", ".files.zip", ".json" })
                    TryDelete(Path.Combine(folder, set.SetName + suffix));
            }
        }

        // ---------------- schedule (PR-Q53/Q89) ----------------

        public static DateTime LastSlot(DateTime now, TimeSpan time)
        {
            var today = now.Date + time;
            return now >= today ? today : today.AddDays(-1);
        }

        /// <summary>
        /// Due when automatic backup is on and no verified backup exists since the most recent 23:00. If the PC was off
        /// at 23:00 the backup runs as soon as the program starts again (catch-up).
        /// </summary>
        public bool IsScheduledBackupDue()
        {
            var config = GetConfiguration();
            if (!config.IsAutoBackupEnabled) return false;
            var slot = LastSlot(_clock(), config.AutoBackupTime);
            return !GetHistory().Any(s => s.Verified && s.CreatedAt >= slot);
        }

        public BackupPageDTO GetPage()
        {
            var config = GetConfiguration();
            var history = GetHistory();
            var status = GetStatus();
            var warnings = new List<string>();
            var newest = history.FirstOrDefault(s => s.Verified);
            var now = _clock();
            if (newest is null) warnings.Add("There is no verified backup yet. Press \"Back up now\".");
            else if (now - newest.CreatedAt > TimeSpan.FromHours(26)) warnings.Add($"The newest verified backup is from {newest.CreatedAt:dd MMM yyyy HH:mm}. The limit is one business day (PR-Q69).");
            if (!config.IsAutoBackupEnabled) warnings.Add("Automatic daily backup is OFF.");
            if (status.LastUsbCopyAt is null || now - status.LastUsbCopyAt > TimeSpan.FromHours(30))
                warnings.Add("Copy the newest backup to the USB drive today and keep the drive away from this PC (PR-Q56).");
            if (status.LastDrillAt is null || now - status.LastDrillAt > TimeSpan.FromDays(31))
                warnings.Add("A monthly restore drill is due (PR-Q84). See DATA_RECOVERY_PLAN.md.");
            else if (!status.LastDrillPassed) warnings.Add("The last restore drill FAILED: " + status.LastDrillResult);
            if (config.LegacyTargetDirectory is not null)
                warnings.Add($"Old backups may still be in {config.LegacyTargetDirectory}. Copy them to the backup folder or USB before upgrading.");
            if (history.FirstOrDefault() is { Verified: false, Legacy: false } failed) warnings.Add("The last backup attempt failed: " + failed.Error);
            return new BackupPageDTO { Config = config, EffectiveTarget = EffectiveTarget(config), Status = status, History = history, Warnings = warnings };
        }

        public BackupStatusDTO GetStatus() => TryRead<BackupStatusDTO>(StatusFile) ?? new BackupStatusDTO();

        // ---------------- USB copy (PR-Q56) ----------------

        public string CopyLatestToUsb(string? directory, string actor)
        {
            var config = GetConfiguration();
            var dir = string.IsNullOrWhiteSpace(directory) ? config.UsbCopyDirectory : directory.Trim();
            var status = GetStatus();
            try
            {
                if (string.IsNullOrWhiteSpace(dir) || !IsFullLocalPath(dir)) throw new InvalidOperationException("Enter the USB drive folder, for example E:\\.");
                var target = EffectiveTarget(config);
                if (DataPaths.IsInside(dir, target) || DataPaths.IsInside(dir, _paths.Root))
                    throw new InvalidOperationException("The USB folder must be on the removable drive, not in the backup or data folder.");
                if (!Directory.Exists(Path.GetPathRoot(dir))) throw new InvalidOperationException($"Drive {Path.GetPathRoot(dir)} is not plugged in.");
                var latest = ReadSets(target).FirstOrDefault(s => s.Verified) ?? throw new InvalidOperationException("There is no verified backup to copy.");
                var destination = Path.Combine(dir, "SwiftCheckoutPOS-Backups");
                Directory.CreateDirectory(destination);
                foreach (var file in new[] { latest.BackupFile, latest.FilesArchive, latest.SetName + ".json" }.Where(f => f is not null))
                    File.Copy(Path.Combine(target, file!), Path.Combine(destination, file!), overwrite: true);
                if (Hash(Path.Combine(destination, latest.BackupFile)) != latest.Sha256)
                    throw new InvalidOperationException("The copy on the USB drive does not match the original. Try another drive.");
                foreach (var old in SelectForDeletion(ReadSets(destination)))
                    foreach (var suffix in new[] { ".bak", ".files.zip", ".json" }) TryDelete(Path.Combine(destination, old.SetName + suffix));
                status.LastUsbCopyAt = _clock(); status.LastUsbCopySet = latest.SetName; status.LastUsbCopyDirectory = destination; status.LastUsbError = null;
                Write(StatusFile, status);
                Audit(actor, "BackupCopiedToUsb", "Backup", latest.SetName, destination);
                return $"{latest.SetName} copied and checked on {destination}.";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                status.LastUsbError = ex is InvalidOperationException ? ex.Message : "Copy failed: " + Short(ex);
                Write(StatusFile, status);
                throw new InvalidOperationException(status.LastUsbError);
            }
        }

        // ---------------- download (IMM-10) ----------------

        /// <summary>Returns the full path of a backup file in the backup folder, or null. Only exact listed names are accepted.</summary>
        public string? OpenBackupFile(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName != Path.GetFileName(fileName)) return null;
            var setName = fileName.EndsWith(".files.zip", StringComparison.Ordinal) ? fileName[..^".files.zip".Length]
                : fileName.EndsWith(".bak", StringComparison.Ordinal) ? fileName[..^4] : null;
            if (setName is null || !(SetPattern.IsMatch(setName) || LegacyPattern.IsMatch(fileName))) return null;
            var folder = EffectiveTarget(GetConfiguration());
            var full = Path.GetFullPath(Path.Combine(folder, fileName));
            return DataPaths.IsInside(full, folder) && File.Exists(full) ? full : null;
        }

        public void RecordDownload(string fileName, string actor) => Audit(actor, "BackupDownloaded", "Backup", fileName);

        // ---------------- restore (T09, owner maintenance only) ----------------

        private BackupManifestDTO FindVerified(string? setName)
        {
            var sets = GetHistory();
            var set = string.IsNullOrWhiteSpace(setName) ? sets.FirstOrDefault(s => s.Verified)
                : sets.FirstOrDefault(s => s.SetName == setName.Trim() || s.BackupFile == setName.Trim());
            if (set is null) throw new InvalidOperationException("Backup set not found in the backup folder.");
            if (!set.Verified) throw new InvalidOperationException("That backup was never verified and will not be restored.");
            var bak = Path.Combine(EffectiveTarget(GetConfiguration()), set.BackupFile);
            if (!File.Exists(bak)) throw new InvalidOperationException("The .bak file is missing.");
            if (Hash(bak) != set.Sha256) throw new InvalidOperationException("The .bak file has changed since it was made (checksum mismatch).");
            _repo.VerifyBackup(bak);
            return set;
        }

        /// <summary>PR-Q84: restore into a separate drill database, run DBCC CHECKDB, count rows, drop it.</summary>
        public string RestoreDrill(string? setName, string actor)
        {
            var status = GetStatus();
            var drillDb = _repo.DatabaseName + "_RestoreDrill";
            try
            {
                var set = FindVerified(setName);
                status.LastDrillSet = set.SetName;
                _repo.DropDatabase(drillDb);
                _repo.RestoreAsCopy(Path.Combine(EffectiveTarget(GetConfiguration()), set.BackupFile), drillDb);
                _repo.CheckDatabase(drillDb);
                var (products, sales, users) = _repo.CountRows(drillDb);
                status.LastDrillPassed = true;
                status.LastDrillResult = $"Passed: {set.SetName} restored and checked ({products} products, {sales} sales, {users} users).";
            }
            catch (Exception ex)
            {
                status.LastDrillPassed = false;
                status.LastDrillResult = ex is InvalidOperationException ? ex.Message : Short(ex);
            }
            finally
            {
                try { _repo.DropDatabase(drillDb); } catch (Exception) { /* left for the next drill to replace */ }
            }
            status.LastDrillAt = _clock();
            Write(StatusFile, status);
            Audit(actor, status.LastDrillPassed ? "RestoreDrillPassed" : "RestoreDrillFailed", "Backup", status.LastDrillSet ?? "-", status.LastDrillResult);
            if (!status.LastDrillPassed) throw new InvalidOperationException(status.LastDrillResult);
            return status.LastDrillResult!;
        }

        /// <summary>
        /// Replaces the live database with a verified backup. A PreRestore safety backup is made first; if the restore
        /// fails, the safety backup is put back automatically.
        /// </summary>
        public string RestoreLive(string setName, string actor)
        {
            var set = FindVerified(setName);
            var folder = EffectiveTarget(GetConfiguration());
            var safety = RunBackup("PreRestore", actor, applyRetention: false);
            try
            {
                _repo.RestoreInPlace(Path.Combine(folder, set.BackupFile));
            }
            catch (Exception ex)
            {
                try { _repo.RestoreInPlace(Path.Combine(folder, safety.BackupFile)); }
                catch (Exception rollback)
                {
                    throw new InvalidOperationException($"Restore failed ({Short(ex)}) and putting back the safety backup also failed ({Short(rollback)}). " +
                        $"Run --restore {safety.SetName} after fixing the cause. Nothing has been deleted.");
                }
                throw new InvalidOperationException($"Restore failed ({Short(ex)}). The database was put back exactly as it was (safety backup {safety.SetName}).");
            }
            Audit(actor, "DatabaseRestored", "Backup", set.SetName, $"Safety backup before restore: {safety.SetName}");
            return $"Restored {set.SetName}. Safety backup of the replaced data: {safety.SetName}.";
        }

        // ---------------- helpers ----------------

        private void Audit(string actor, string action, string entityType, string key, string? details = null)
        {
            if (_audit is null) return;
            try { _audit.Run(() => _audit.Audit(actor, action, entityType, key, null, details)); }
            catch (Exception) { /* the backup record files remain the source of truth */ }
        }

        private static string Hash(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static T? TryRead<T>(string path) where T : class
        {
            try { return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : null; }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return null; }
        }

        private static void Write<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(value, Json));
            File.Move(temp, path, overwrite: true);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }

        private static string Short(Exception ex)
        {
            var message = ex.GetBaseException().Message.ReplaceLineEndings(" ");
            return message.Length <= 300 ? message : message[..300];
        }
    }
}
