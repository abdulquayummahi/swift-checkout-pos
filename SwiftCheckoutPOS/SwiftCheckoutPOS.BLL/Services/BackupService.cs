// Project: SwiftCheckoutPOS.BLL
// File: Services/BackupService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.BLL.Services
{
    public class BackupService
    {
        private readonly BackupRepository _backupRepo;
        // Map the configuration file to the executable's root directory
        private readonly string _configDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private readonly string _configFile;

        public BackupService(BackupRepository backupRepo)
        {
            _backupRepo = backupRepo;
            _configFile = Path.Combine(_configDirectory, "backup_config.json");
        }

        public BackupConfigDTO GetConfiguration()
        {
            if (!File.Exists(_configFile)) return new BackupConfigDTO();

            try
            {
                string json = File.ReadAllText(_configFile);
                return JsonSerializer.Deserialize<BackupConfigDTO>(json) ?? new BackupConfigDTO();
            }
            catch
            {
                return new BackupConfigDTO();
            }
        }

        public void SaveConfiguration(BackupConfigDTO config)
        {
            if (!Directory.Exists(config.TargetDirectory))
            {
                Directory.CreateDirectory(config.TargetDirectory);
            }

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configFile, json);
        }

        public string ExecuteManualBackup()
        {
            var config = GetConfiguration();
            return _backupRepo.CreateDatabaseBackup(config.TargetDirectory);
        }

        public IEnumerable<BackupFileDTO> GetBackupHistory()
        {
            var config = GetConfiguration();
            if (!Directory.Exists(config.TargetDirectory)) return new List<BackupFileDTO>();

            var directoryInfo = new DirectoryInfo(config.TargetDirectory);
            var files = directoryInfo.GetFiles("*.bak").OrderByDescending(f => f.CreationTime);

            return files.Select(f => new BackupFileDTO
            {
                FileName = f.Name,
                FilePath = f.FullName,
                FileSizeMB = Math.Round(f.Length / 1048576.0, 2),
                CreationTime = f.CreationTime
            }).ToList();
        }
    }
}