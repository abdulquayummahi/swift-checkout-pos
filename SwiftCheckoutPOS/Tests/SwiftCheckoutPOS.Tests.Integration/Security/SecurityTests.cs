using SwiftCheckoutPOS.BLL.DTOs;
using SwiftCheckoutPOS.BLL.Security;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Repositories;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Security;

public sealed class LoginThrottleTests
{
    [Fact]
    [Trait("Fixes", "DR-23")]
    public void Approved_DelaysAfterFifthFailureDoubleToFiveMinutes_CQ09()
    {
        var t = new LoginThrottle(); var now = new DateTime(2026, 11, 4, 10, 0, 0);
        for (var i = 1; i <= 4; i++) Assert.Equal(TimeSpan.Zero, t.RegisterFailure("cashier", now));
        Assert.Equal(TimeSpan.FromSeconds(30), t.RegisterFailure("cashier", now));
        Assert.True(t.WaitRemaining("CASHIER", now.AddSeconds(10)) > TimeSpan.Zero); // per username, case-insensitive
        Assert.Equal(TimeSpan.FromSeconds(60), t.RegisterFailure("cashier", now.AddSeconds(31)));
        Assert.Equal(TimeSpan.FromSeconds(300), LoginThrottle.Delay(9));             // capped
        Assert.Equal(TimeSpan.FromSeconds(300), LoginThrottle.Delay(20));
        Assert.Equal(TimeSpan.Zero, t.WaitRemaining("cashier", now.AddMinutes(32))); // 30 min without failure resets
        t.RegisterFailure("x", now); t.Reset("x");
        Assert.Equal(TimeSpan.Zero, t.WaitRemaining("x", now));
    }
}

[Collection("SqlServer")]
public sealed class AccountSecurityTests
{
    private readonly SqlServerFixture fixture;
    public AccountSecurityTests(SqlServerFixture fixture) { this.fixture = fixture; fixture.Reset(); }

    [Fact]
    [Trait("Fixes", "PC-01")]
    public void Approved_RoleStatusOrPasswordChangeEndsExistingLogins_IMM09()
    {
        using var c = fixture.Context();
        var users = new UserAdminService(new UserRepository(c));
        users.CreateUser(new UserActionDTO { Username = "mgr", FullName = "Manager One", Role = "Manager", PlainTextPassword = "Passw0rd!x-long", IsActive = true });
        var auth = new AuthService(new UserRepository(c));
        Assert.True(auth.ValidateCredentials("mgr", "Passw0rd!x-long", out var id, out _, out _, out var stamp));
        var dto = users.GetUserForEdit(id); dto.Role = "Cashier";
        users.UpdateUser(dto);
        var state = auth.SecurityState(id)!.Value;
        Assert.Equal("Cashier", state.Role);
        Assert.NotEqual(stamp, state.Stamp); // the old cookie's stamp no longer matches
    }

    [Fact]
    [Trait("Fixes", "PC-09")]
    public void Approved_FirstAdminOnlyWhileNoUserExistsAndNoDefaultPassword_PRQ52()
    {
        using var c = fixture.Context();
        var auth = new AuthService(new UserRepository(c));
        Assert.True(auth.NeedsFirstAdmin());
        Assert.Throws<ArgumentException>(() => auth.CreateFirstAdmin("owner", "Owner", "short"));
        auth.CreateFirstAdmin("owner", "Shop Owner", "LongEnough1-ok");
        Assert.False(auth.NeedsFirstAdmin());
        Assert.Throws<InvalidOperationException>(() => auth.CreateFirstAdmin("second", "X", "LongEnough1-ok"));
        Assert.True(auth.ValidateCredentials("owner", "LongEnough1-ok", out _, out var role, out _));
        Assert.Equal("Admin", role);
        auth.EmergencyResetAdmin("owner", "NewSecret99-ok"); // PR-Q68 local maintenance
        Assert.False(auth.ValidateCredentials("owner", "LongEnough1-ok", out _, out _, out _));
        Assert.True(auth.ValidateCredentials("owner", "NewSecret99-ok", out _, out _, out _));
    }
}
