using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SwiftCheckoutPOS.DAL.Context;

namespace SwiftCheckoutPOS.Tests.Integration.Infrastructure;

/// <summary>IMM-03: the test schema is built from the real EF migrations, not a frozen SQL file.</summary>
public static class SchemaScript
{
    public static DbContextOptions<ApplicationDbContext> Options(TestConnectionGuard guard,
        params IInterceptor[] interceptors) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(guard.ForTestDatabase(), sql => sql.EnableRetryOnFailure())
            .AddInterceptors(interceptors)
            .Options;

    /// <summary>Identity of the migration set; a new migration changes it and triggers a test-DB rebuild.</summary>
    public static string MigrationsHash(TestConnectionGuard guard)
    {
        using var context = new ApplicationDbContext(Options(guard)); // metadata only; no connection
        var ids = string.Join("\n", context.Database.GetMigrations());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ids)));
    }
}
