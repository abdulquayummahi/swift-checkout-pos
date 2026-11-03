using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Repositories;

namespace SwiftCheckoutPOS.UI.Infrastructure;

/// <summary>
/// PR-Q68 owner-only maintenance, run on the shop PC in a console:
///   SwiftCheckoutPOS.UI.exe --reset-admin      (asks for username and new password)
///   SwiftCheckoutPOS.UI.exe --migrate           (applies database migrations; back up first, see DATA_RECOVERY_PLAN.md)
/// </summary>
public static class MaintenanceCommands
{
    public static bool TryRun(string[] args, string connectionString)
    {
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
