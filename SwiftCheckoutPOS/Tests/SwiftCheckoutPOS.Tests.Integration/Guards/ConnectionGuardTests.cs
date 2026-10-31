using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Guards;

public sealed class ConnectionGuardTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a connection")]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Database=SwiftCheckoutPOSDb;Integrated Security=true")]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Database=SwiftCheckoutPOS_Test_Copy;Integrated Security=true")]
    [InlineData("Server=remote.example;Database=SwiftCheckoutPOS_Test;Integrated Security=true")]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=true")]
    [InlineData("Server=(localdb)\\MSSQLLocalDB;Database=SwiftCheckoutPOS_Test;AttachDbFilename=C:\\fixture.mdf;Integrated Security=true")]
    [InlineData("Server=localhost;Database=SwiftCheckoutPOS_Test;Failover Partner=remote.example;Integrated Security=true")]
    public void RejectsInvalidTargetBeforeAnyConnection(string? value) =>
        Assert.Throws<InvalidOperationException>(() => TestConnectionGuard.Parse(value));

    [Theory]
    [InlineData("(localdb)\\MSSQLLocalDB")]
    [InlineData(".\\SQLEXPRESS")]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    public void AcceptsExactTestCatalogOnLocalInstance(string server)
    {
        // Synthetic input, never opened.
        var value = $"Server={server};Database=SwiftCheckoutPOS_Test;Integrated Security=true";
        Assert.NotNull(TestConnectionGuard.Parse(value));
    }

    [Fact]
    public void InvalidConfigurationDoesNotEchoInput()
    {
        const string syntheticSentinel = "SYNTHETIC_NEVER_ECHO";
        var error = Assert.Throws<InvalidOperationException>(() =>
            TestConnectionGuard.Parse($"Server=remote.example;Database=wrong;Password={syntheticSentinel}"));
        Assert.True(!error.Message.Contains(syntheticSentinel), "Guard diagnostics must be redacted.");
    }
}
