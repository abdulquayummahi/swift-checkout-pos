using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace SwiftCheckoutPOS.Tests.Integration.Infrastructure;

/// <summary>No connection is constructed/opened until this value has passed validation.</summary>
public sealed class TestConnectionGuard
{
    public const string Catalog = "SwiftCheckoutPOS_Test";
    public const string Variable = "SWIFTCHECKOUTPOS_TEST_CONNECTION";
    private readonly string connectionString;

    private TestConnectionGuard(string value) => connectionString = value;

    public static TestConnectionGuard FromEnvironment()
    {
        // This developer-only fixture never loads configuration, User Secrets or the UI host.
        var value = Environment.GetEnvironmentVariable(Variable, EnvironmentVariableTarget.User);
        if (string.IsNullOrWhiteSpace(value)) value = Environment.GetEnvironmentVariable(Variable);
        return Parse(value);
    }

    public static TestConnectionGuard Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw Invalid();
        try
        {
            var builder = new SqlConnectionStringBuilder(value);
            var local = Regex.IsMatch(builder.DataSource,
                @"^\(localdb\)\\[A-Za-z0-9_-]+$", RegexOptions.IgnoreCase)
                || Regex.IsMatch(builder.DataSource,
                    @"^(\.|\(local\)|localhost|127\.0\.0\.1|\[::1\]|"
                    + Regex.Escape(Environment.MachineName) + @")(\\[A-Za-z0-9_-]+)?$",
                    RegexOptions.IgnoreCase);
            if (!local || !string.Equals(builder.InitialCatalog, Catalog, StringComparison.Ordinal)
                || !string.IsNullOrEmpty(builder.AttachDBFilename)
                || !string.IsNullOrEmpty(builder.FailoverPartner))
                throw Invalid();
            builder.ApplicationName = "SwiftCheckoutPOS.Tests.Integration.BP10";
            // No pooled session state survives a test reset or escapes this fixture.
            builder.Pooling = false;
            return new TestConnectionGuard(builder.ConnectionString);
        }
        catch (ArgumentException) { throw Invalid(); }
        catch (FormatException) { throw Invalid(); }
    }

    internal string ForTestDatabase() => connectionString;
    internal string ForProvisioning()
    {
        var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
        return builder.ConnectionString;
    }

    // Never include the rejected value or a parser's potentially sensitive message.
    private static InvalidOperationException Invalid() =>
        new("Test configuration rejected: require a local instance, exact SwiftCheckoutPOS_Test catalog, and no attach-file/failover.");
    public override string ToString() => "Guarded SwiftCheckoutPOS_Test connection [REDACTED]";
}
