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
///   SwiftCheckoutPOS.UI.exe --recreate-database (deletes and recreates an old/test database; copy saved first)
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
            var (state, pending) = Inspect(connectionString);
            if (state == SchemaState.Legacy) { Console.WriteLine(LegacyMessage(connectionString)); Environment.ExitCode = 3; return true; }
            using var context = Context();
            Console.WriteLine(pending.Count == 0 ? "Database is up to date." : $"Applying {pending.Count} migration(s): {string.Join(", ", pending)}");
            context.Database.Migrate();
            Console.WriteLine("Done.");
            return true;
        }
        if (args.Contains("--recreate-database"))
        {
            var database = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString).InitialCatalog;
            Console.WriteLine($"This DELETES database '{database}' and all its data, then creates it empty with the current schema.");
            Console.WriteLine("Use it only for a development/test database or one made by an older development version.");
            Console.Write($"Type the database name ({database}) to continue: ");
            if (Console.ReadLine()?.Trim() != database) { Console.WriteLine("Not confirmed. Nothing changed."); return true; }
            var repo = new BackupRepository(connectionString);
            try
            {
                var backups = new BackupService(repo, paths, null, null, [legacyImageFolder]);
                var set = backups.RunBackup("PreUpgrade", Owner, applyRetention: false);
                Console.WriteLine($"Copy of the old database saved first: {set.SetName} in {backups.EffectiveTarget(backups.GetConfiguration())}.");
            }
            catch (Exception ex) when (ex is InvalidOperationException or Microsoft.Data.SqlClient.SqlException)
            {
                Console.WriteLine($"A copy of the old database could not be made ({ex.Message}).");
                Console.Write("Type YES to delete it anyway without a copy: ");
                if (Console.ReadLine()?.Trim() != "YES") { Console.WriteLine("Nothing changed."); return true; }
            }
            repo.DropThisDatabase();
            using (var context = Context()) context.Database.Migrate();
            Console.WriteLine($"Database '{database}' created with the current schema. Start the program: the first page asks for the first Admin.");
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

    public enum SchemaState { Current, Pending, Missing, Legacy, Unreachable }
    private const string BaselineMigration = "20261103183746_InitialBaseline";

    /// <summary>
    /// Reads (never changes) the database state: missing, made by a pre-baseline development version (tables but no
    /// InitialBaseline in the history), migrations pending, or current.
    /// </summary>
    public static (SchemaState State, IReadOnlyList<string> Pending) Inspect(string connectionString)
    {
        try
        {
            using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connectionString).Options);
            if (!context.Database.CanConnect()) return (SchemaState.Missing, context.Database.GetMigrations().ToList());
            var applied = context.Database.GetAppliedMigrations().ToList();
            if (!applied.Contains(BaselineMigration))
            {
                var tables = context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS [Value] FROM sys.tables WHERE name <> N'__EFMigrationsHistory'").Single();
                if (tables > 0) return (SchemaState.Legacy, Array.Empty<string>());
            }
            var pending = context.Database.GetPendingMigrations().ToList();
            return (pending.Count == 0 ? SchemaState.Current : SchemaState.Pending, pending);
        }
        catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
        {
            return (SchemaState.Unreachable, Array.Empty<string>());
        }
    }

    public static string LegacyMessage(string connectionString)
    {
        var database = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString).InitialCatalog;
        return $"Database '{database}' was made by an older development version and cannot be upgraded (see UPGRADE_GUIDE.md).\n" +
               "If its data is not needed, run the program once with --recreate-database (it saves a copy first, then creates it empty).";
    }

    /// <summary>Startup gate: the till never runs on a database whose tables do not match this version.</summary>
    public static bool SchemaReady(string connectionString)
    {
        var (state, pending) = Inspect(connectionString);
        var message = state switch
        {
            SchemaState.Legacy => LegacyMessage(connectionString),
            SchemaState.Missing => "The database does not exist yet. Run the program once with --migrate to create it.",
            SchemaState.Pending => $"The database needs {pending.Count} update(s) ({string.Join(", ", pending)}). Make a backup, then run the program once with --migrate.",
            _ => null
        };
        if (message is null) return true; // Current, or SQL Server not reachable yet (the service restarts and retries)
        Console.Error.WriteLine("SwiftCheckoutPOS cannot start: " + message);
        Environment.ExitCode = 3;
        return false;
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
