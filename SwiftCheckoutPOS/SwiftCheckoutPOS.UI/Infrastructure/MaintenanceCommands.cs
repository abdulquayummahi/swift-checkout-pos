using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.UI.Infrastructure;

/// <summary>
/// PR-Q68 owner-only maintenance, run on the shop PC in a console:
///   SwiftCheckoutPOS.UI.exe --reset-admin      (asks for username and new password)
///   SwiftCheckoutPOS.UI.exe --migrate           (applies database migrations; back up first, see DATA_RECOVERY_PLAN.md)
///   SwiftCheckoutPOS.UI.exe --backup            (verified PreUpgrade backup, PR-Q53)
///   SwiftCheckoutPOS.UI.exe --restore-drill [name]  (PR-Q84 test restore into a separate database; live data unchanged)
///   SwiftCheckoutPOS.UI.exe --restore name      (replaces live data; safety backup first; stop the service first)
/// </summary>
public static class MaintenanceCommands
{
    private const string Owner = "OWNER (maintenance console)";

    public static bool TryRun(string[] args, string connectionString, SwiftCheckoutPOS.BLL.Configuration.DataPaths paths, string legacyImageFolder)
    {
        BackupService Backups(ApplicationDbContext context) =>
            new(new BackupRepository(connectionString), paths, new AtomicUnit(context), null, [legacyImageFolder]);
        ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()).Options);
        string? After(string flag) { var i = Array.IndexOf(args, flag); return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : null; }

        if (args.Contains("--backup") || args.Contains("--restore-drill") || args.Contains("--restore"))
        {
            using var context = Context();
            var backups = Backups(context);
            try
            {
                if (args.Contains("--backup"))
                {
                    var set = backups.RunBackup("PreUpgrade", Owner);
                    Console.WriteLine($"Backup {set.SetName} made and verified ({set.SizeMB} MB) in {backups.EffectiveTarget(backups.GetConfiguration())}.");
                }
                else if (args.Contains("--restore-drill"))
                {
                    Console.WriteLine("Restore drill: restoring into a separate test database. Live data is not changed...");
                    Console.WriteLine(backups.RestoreDrill(After("--restore-drill"), Owner));
                }
                else
                {
                    var name = After("--restore");
                    if (name is null) { Console.WriteLine("Give the backup name, for example: --restore SwiftCheckoutPOS_20261104_230000_Daily"); return true; }
                    var database = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString).InitialCatalog;
                    Console.WriteLine($"This REPLACES all data in database '{database}' with backup {name}.");
                    Console.WriteLine("Stop the SwiftCheckoutPOS service first. A safety backup of the current data is made automatically.");
                    Console.Write($"Type the database name ({database}) to continue: ");
                    if (Console.ReadLine()?.Trim() != database) { Console.WriteLine("Not confirmed. Nothing changed."); return true; }
                    Console.WriteLine(backups.RestoreLive(name, Owner));
                    Console.WriteLine("Next: run --migrate (if this backup is from an older version), start the service, sign in and check today's last receipt.");
                }
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
                Environment.ExitCode = 1;
            }
            return true;
        }
        if (args.Contains("--migrate"))
        {
            using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()).Options);
            var pending = context.Database.GetPendingMigrations().ToList();
            Console.WriteLine(pending.Count == 0 ? "Database is up to date." : $"Applying {pending.Count} migration(s): {string.Join(", ", pending)}");
            context.Database.Migrate();
            Console.WriteLine("Done.");
            return true;
        }
        if (!args.Contains("--reset-admin")) return false;
        Console.Write("Admin username: ");
        var username = Console.ReadLine() ?? "";
        Console.Write("New password (12+ characters): ");
        var password = ReadHidden();
        Console.Write("Repeat password: ");
        if (ReadHidden() != password) { Console.WriteLine("Passwords differ. Nothing changed."); return true; }
        using (var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                   .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()).Options))
        {
            new AuthService(new UserRepository(context)).EmergencyResetAdmin(username, password);
        }
        Console.WriteLine($"Admin '{username.Trim()}' is active with the new password. All of that user's existing logins were ended.");
        return true;
    }

    private static string ReadHidden()
    {
        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return new string(chars.ToArray()); }
            if (key.Key == ConsoleKey.Backspace) { if (chars.Count > 0) chars.RemoveAt(chars.Count - 1); continue; }
            chars.Add(key.KeyChar);
        }
    }
}
