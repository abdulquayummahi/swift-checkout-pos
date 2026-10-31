// Project: SwiftCheckoutPOS.DAL
// File: Repositories/BackupRepository.cs
using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;

namespace SwiftCheckoutPOS.DAL.Repositories
{
    public class BackupRepository
    {
        private readonly ApplicationDbContext _context;

        public BackupRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Executes a raw T-SQL command to generate a full .bak file on the local hard drive.
        /// </summary>
        /// <param name="targetDirectory">The physical path selected by the user via the UI.</param>
        /// <returns>The absolute file path of the successfully generated backup file.</returns>
        public string CreateDatabaseBackup(string targetDirectory)
        {
            try
            {
                // Ensure the directory exists on the physical drive
                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                // Standardize the filename with a strict timestamp to prevent overwrites
                string fileName = $"SwiftCheckoutPOS_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                string fullPath = Path.Combine(targetDirectory, fileName);

                // Dynamically retrieve the database name from the active connection string
                string databaseName = _context.Database.GetDbConnection().Database;

                // ARCHITECT DIRECTIVE: Raw parameterized T-SQL for native SQL Server backup
                // Using FORMAT to overwrite existing media headers, INIT to create a new backup set
                string sqlCommand = $@"
                    BACKUP DATABASE [{databaseName}] 
                    TO DISK = '{fullPath}' 
                    WITH FORMAT, 
                    MEDIANAME = 'SwiftCheckoutPOS_LocalBackup', 
                    NAME = 'Full Backup of SwiftCheckoutPOS';";

                // Execute the command directly against the SQL Server engine
                _context.Database.ExecuteSqlRaw(sqlCommand);

                return fullPath;
            }
            catch (Exception ex)
            {
                // Pass the hardware/SQL exception up to the BLL for logging and UI notification
                throw new InvalidOperationException($"Hardware or SQL Fault during backup execution: {ex.Message}");
            }
        }
    }
}