using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Guards;

public sealed class MarkerGuardTests
{
    [Theory]
    [InlineData(false, null, null, false)]
    [InlineData(true, "different-owner", "hash", true)]
    [InlineData(true, SqlServerFixture.Owner, "different-hash", true)]
    [InlineData(true, SqlServerFixture.Owner, "hash", false)]
    public void RejectsUnmarkedOrMismatchedCatalogBeforeReset(bool exists, string? owner, string? hash, bool ready) =>
        Assert.Throws<InvalidOperationException>(() =>
            SqlServerFixture.RequireOwnership(exists, owner, hash, ready, "hash"));

    [Fact]
    public void AcceptsOnlyReadyMatchingFixtureMarker() =>
        SqlServerFixture.RequireOwnership(true, SqlServerFixture.Owner, "hash", true, "hash");
}
