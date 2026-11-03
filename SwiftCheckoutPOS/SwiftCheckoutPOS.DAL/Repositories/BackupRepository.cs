// Project: SwiftCheckoutPOS.DAL
// File: Repositories/BackupRepository.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.DAL.Context;

namespace SwiftCheckoutPOS.DAL.Repositories
{
    /// <summary>
    /// IMM-10/BP-05: native SQL Server backup, verification and restore. Every database name and file path
    /// travels as a SQL parameter (BACKUP/RESTORE accept variables); ALTER/DROP DATABASE use QUOTENAME.
    /// No user text is ever concatenated into SQL.
    /// </summary>
    public class BackupRepository
    {
        private readonly string _connectionString;

        public BackupRepository(ApplicationDbContext context)
            : this(context.Database.GetConnectionString()
                   ?? throw new InvalidOperationException("The database connection is not configured.")) { }

        public BackupRepository(string connectionString) => _connectionString = connectionString;

        public string DatabaseName => new SqlConnectionStringBuilder(_connectionString).InitialCatalog;

        private string MasterConnection()
        {
            var b = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master", Pooling = false };
            return b.ConnectionString;
        }

        private static void Exec(string connectionString, string sql, params (string Name, object Value)[] parameters)
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var command = new SqlCommand(sql, connection) { CommandTimeout = 0 };
            foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
            command.ExecuteNonQuery();
        }

        /// <summary>Full copy-only-free backup with page checksums. The SQL Server service writes the file.</summary>
        public void BackupDatabase(string fullPath, string backupName) =>
            Exec(MasterConnection(),
                "BACKUP DATABASE @db TO DISK = @path WITH INIT, FORMAT, CHECKSUM, NAME = @name, MEDIANAME = N'SwiftCheckoutPOS';",
                ("@db", DatabaseName), ("@path", fullPath), ("@name", backupName));

        /// <summary>RESTORE VERIFYONLY WITH CHECKSUM: the file is readable, complete and its page checksums are valid.</summary>
        public void VerifyBackup(string fullPath) =>
            Exec(MasterConnection(), "RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;", ("@path", fullPath));

        public IReadOnlyList<(string Logical, string Type)> FileList(string fullPath)
        {
            var list = new List<(string, string)>();
            using var connection = new SqlConnection(MasterConnection());
            connection.Open();
            using var command = new SqlCommand("RESTORE FILELISTONLY FROM DISK = @path;", connection) { CommandTimeout = 0 };
            command.Parameters.AddWithValue("@path", fullPath);
            using var reader = command.ExecuteReader();
            while (reader.Read()) list.Add((reader.GetString(reader.GetOrdinal("LogicalName")), reader.GetString(reader.GetOrdinal("Type"))));
            return list;
        }

        /// <summary>Current physical files of a database: (type "D"/"L", path), in file_id order. Empty when it does not exist.</summary>
        private IReadOnlyList<(string Type, string Physical)> CurrentFiles(string databaseName)
        {
            var list = new List<(string, string)>();
            using var connection = new SqlConnection(MasterConnection());
            connection.Open();
            using var command = new SqlCommand(
                "SELECT CASE type WHEN 1 THEN 'L' ELSE 'D' END, physical_name FROM sys.master_files WHERE database_id = DB_ID(@db) ORDER BY file_id;", connection);
            command.Parameters.AddWithValue("@db", databaseName);
            using var reader = command.ExecuteReader();
            while (reader.Read()) list.Add((reader.GetString(0), reader.GetString(1)));
            return list;
        }

        /// <summary>
        /// RESTORE with MOVE for every file: onto the target database's existing files when it exists (so a backup made
        /// on another PC or folder still restores), otherwise into the instance default folders named after the target.
        /// </summary>
        private void Restore(string fullPath, string targetDb, bool replace, string? prefix = null)
        {
            var files = FileList(fullPath);
            var current = CurrentFiles(targetDb);
            var parameters = new List<(string, object)> { ("@db", targetDb), ("@path", fullPath) };
            var sql = "DECLARE @dataDir nvarchar(400) = CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400));" +
                      "DECLARE @logDir nvarchar(400) = CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(400));";
            var moves = new List<string>();
            for (var i = 0; i < files.Count; i++)
            {
                var type = files[i].Type == "L" ? "L" : "D";
                var ordinal = files.Take(i).Count(f => (f.Type == "L" ? "L" : "D") == type);
                var existing = current.Where(c => c.Type == type).Skip(ordinal).Select(c => c.Physical).FirstOrDefault();
                parameters.Add(($"@l{i}", files[i].Logical));
                if (existing is not null)
                {
                    parameters.Add(($"@f{i}", existing));
                }
                else
                {
                    parameters.Add(($"@x{i}", type == "L" ? $"_log{i}.ldf" : $"_data{i}.{(i == 0 ? "mdf" : "ndf")}"));
                    sql += $"DECLARE @f{i} nvarchar(600) = {(type == "L" ? "@logDir" : "@dataDir")} + @db + @x{i};";
                }
                moves.Add($"MOVE @l{i} TO @f{i}");
            }
            sql += (prefix ?? "") + $"RESTORE DATABASE @db FROM DISK = @path WITH {string.Join(", ", moves)}{(replace ? ", REPLACE" : "")}, CHECKSUM, RECOVERY;";
            Exec(MasterConnection(), sql, parameters.ToArray());
        }

        /// <summary>
        /// Replaces this database with the backup. Other connections are closed first (SINGLE_USER WITH ROLLBACK
        /// IMMEDIATE) and the database is always returned to MULTI_USER. Run only from the owner maintenance command.
        /// </summary>
        public void RestoreInPlace(string fullPath)
        {
            const string single = "IF DB_ID(@db) IS NOT NULL BEGIN DECLARE @s nvarchar(400) = N'ALTER DATABASE ' + QUOTENAME(@db) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE'; EXEC(@s); END;";
            const string multi = "IF DB_ID(@db) IS NOT NULL BEGIN DECLARE @s nvarchar(400) = N'ALTER DATABASE ' + QUOTENAME(@db) + N' SET MULTI_USER'; EXEC(@s); END";
            SqlConnection.ClearAllPools();
            try
            {
                Restore(fullPath, DatabaseName, replace: true, prefix: single);
            }
            finally
            {
                try { Exec(MasterConnection(), multi, ("@db", DatabaseName)); } catch (SqlException) { /* reported by caller's checks */ }
                SqlConnection.ClearAllPools();
            }
        }

        /// <summary>PR-Q84 restore drill: restores the backup as a separate database with new file names.</summary>
        public void RestoreAsCopy(string fullPath, string copyName)
        {
            if (string.Equals(copyName, DatabaseName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("A restore drill must use a different database name.");
            if (CurrentFiles(copyName).Count > 0) throw new InvalidOperationException($"Database {copyName} already exists.");
            Restore(fullPath, copyName, replace: false);
        }

        public (int Products, int Sales, int Users) CountRows(string databaseName)
        {
            var b = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = databaseName, Pooling = false };
            using var connection = new SqlConnection(b.ConnectionString);
            connection.Open();
            using var command = new SqlCommand("SELECT (SELECT COUNT(*) FROM dbo.Products), (SELECT COUNT(*) FROM dbo.Sales), (SELECT COUNT(*) FROM dbo.Users);", connection);
            using var reader = command.ExecuteReader();
            reader.Read();
            return (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2));
        }

        public void CheckDatabase(string databaseName) =>
            Exec(MasterConnection(), "DECLARE @s nvarchar(400) = N'DBCC CHECKDB(' + QUOTENAME(@db) + N') WITH NO_INFOMSGS'; EXEC(@s);", ("@db", databaseName));

        /// <summary>Owner maintenance only (--recreate-database, after typed confirmation): drops THIS database.</summary>
        public void DropThisDatabase()
        {
            SqlConnection.ClearAllPools();
            Exec(MasterConnection(),
                "IF DB_ID(@db) IS NOT NULL BEGIN DECLARE @s nvarchar(600) = N'ALTER DATABASE ' + QUOTENAME(@db) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@db); EXEC(@s); END",
                ("@db", DatabaseName));
            SqlConnection.ClearAllPools();
        }

        public void DropDatabase(string databaseName)
        {
            if (string.Equals(databaseName, DatabaseName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to drop the live database.");
            SqlConnection.ClearAllPools();
            Exec(MasterConnection(),
                "IF DB_ID(@db) IS NOT NULL BEGIN DECLARE @s nvarchar(600) = N'ALTER DATABASE ' + QUOTENAME(@db) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@db); EXEC(@s); END",
                ("@db", databaseName));
        }
    }
}
