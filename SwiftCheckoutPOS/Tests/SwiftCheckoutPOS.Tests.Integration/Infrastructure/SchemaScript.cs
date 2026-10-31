using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SwiftCheckoutPOS.DAL.Context;

namespace SwiftCheckoutPOS.Tests.Integration.Infrastructure;

public static class SchemaScript
{
    public static DbContextOptions<ApplicationDbContext> Options(TestConnectionGuard guard,
        params IInterceptor[] interceptors) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(guard.ForTestDatabase(), sql => sql.EnableRetryOnFailure())
            .AddInterceptors(interceptors)
            .Options;

    public static string Generate(TestConnectionGuard guard)
    {
        using var context = new ApplicationDbContext(Options(guard));
        return "-- BP-10 TEST ONLY: current model, not an upgrade migration.\n"
            + "-- Apply only through the guarded SwiftCheckoutPOS_Test fixture.\n"
            + Normalize(context.Database.GenerateCreateScript());
    }

    public static string Normalize(string value) => value.Replace("\r\n", "\n").TrimEnd() + "\n";
    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(value))));
    public static string DeployedPath => Path.Combine(AppContext.BaseDirectory, "Schema", "CurrentModelBaseline.sql");

    public static string SourcePath([CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "Schema", "CurrentModelBaseline.sql"));

    public static string ReadReviewed(TestConnectionGuard guard)
    {
        if (!File.Exists(DeployedPath))
            throw new InvalidOperationException("Reviewed test schema is missing. Generate, review, then rebuild.");
        var script = Normalize(File.ReadAllText(DeployedPath));
        if (script != Generate(guard))
            throw new InvalidOperationException("Reviewed test schema differs from the current model. Stop and review; no SQL was applied.");
        return script;
    }
}
