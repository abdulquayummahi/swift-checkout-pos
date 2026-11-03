using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Host;

/// <summary>
/// BP-06/BP-07: starts the real Program in memory in Production mode against the guarded test database and a temporary
/// data root. Checks DI for every controller, anonymous/redirect behaviour, the safe error page, host filtering,
/// security headers and media serving. No Kestrel port, browser or device is used.
/// </summary>
[Collection("SqlServer")]
public sealed class HostSmokeTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "scpos-host-" + Guid.NewGuid().ToString("N")[..10]);
    private readonly WebApplicationFactory<Program> factory;
    private readonly Dictionary<string, string?> saved = new();

    public HostSmokeTests(SqlServerFixture fixture)
    {
        fixture.Reset();
        Set("ConnectionStrings__DbConn", fixture.Guard.ForTestDatabase());
        Set("Paths__DataRoot", root);
        Set("ASPNETCORE_ENVIRONMENT", "Production");
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Production"));
    }

    private void Set(string name, string value)
    {
        saved[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    private HttpClient Client(string host = "http://localhost") =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri(host) });

    [Fact]
    [Trait("Fixes", "BP-07")]
    public async Task Approved_HostStartsWithSafePagesHeadersAndLoopbackHostsOnly_PRQ50_PRQ64()
    {
        var client = Client();

        var first = await client.GetAsync("/Account/Login");           // empty database: one-time first-Admin setup (BP-13)
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Contains("/Account/Setup", first.Headers.Location!.ToString());
        var login = await client.GetAsync("/Account/Setup");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("nosniff", login.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", login.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", login.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Contains("no-store", login.Headers.CacheControl!.ToString());
        Assert.False(login.Headers.Contains("Server"));

        var till = await client.GetAsync("/Checkout");
        Assert.Equal(HttpStatusCode.Redirect, till.StatusCode);         // fallback policy: sign in first
        Assert.Contains("/Account/Login", till.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/BackupAdmin")).StatusCode);

        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/no/such/page")).StatusCode); // unknown pages reveal nothing before sign-in
        var missing = await client.GetAsync("/Home/Error?code=404");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var body = await missing.Content.ReadAsStringAsync();
        Assert.Contains("Page not found", body);
        Assert.DoesNotContain("Exception", body);
        var failure = await (await client.GetAsync("/Home/Error")).Content.ReadAsStringAsync();
        Assert.Contains("Something went wrong", failure);
        Assert.DoesNotContain("Development", failure);

        var evil = await Client("http://pos.example.com").GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.BadRequest, evil.StatusCode);       // AllowedHosts: localhost/127.0.0.1/[::1] only

        File.WriteAllBytes(Path.Combine(root, "Media", "products", "p-x-000000000000.png"), [0x89, 0x50, 0x4E, 0x47]);
        var image = await client.GetAsync("/images/products/p-x-000000000000.png");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);              // uploaded images come from the data root

        Assert.True(Directory.Exists(Path.Combine(root, "Keys")));
        Assert.True(Directory.Exists(Path.Combine(root, "Logs")));
    }

    [Fact]
    [Trait("Fixes", "BP-06")]
    public void Approved_EveryControllerResolvesFromTheRealContainer()
    {
        _ = Client(); // builds the host
        using var scope = factory.Services.CreateScope();
        var controllers = typeof(Program).Assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract).ToList();
        Assert.True(controllers.Count >= 15);
        foreach (var type in controllers)
            Assert.NotNull(ActivatorUtilities.CreateInstance(scope.ServiceProvider, type));
    }

    private static string Token(string html)
    {
        var m = System.Text.RegularExpressions.Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        Assert.True(m.Success, "antiforgery token missing from form");
        return m.Groups[1].Value;
    }

    [Fact]
    [Trait("Fixes", "BP-07")]
    public async Task Approved_FirstAdminThenEveryPageRendersInProduction_PRQ52()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true, BaseAddress = new Uri("http://localhost") });
        var setup = await client.GetStringAsync("/Account/Setup");
        var created = await client.PostAsync("/Account/Setup", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(setup), ["username"] = "owner", ["fullName"] = "Shop Owner",
            ["password"] = "correct horse 2026", ["confirmPassword"] = "correct horse 2026"
        }));
        var loginPage = await created.Content.ReadAsStringAsync();
        Assert.Contains("Admin created", loginPage);
        Assert.Equal(HttpStatusCode.Redirect, (await Client().GetAsync("/Account/Setup")).StatusCode); // one-time only

        var signedIn = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Token(loginPage), ["Username"] = "owner", ["Password"] = "correct horse 2026"
        }));
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.DoesNotContain("Invalid username", await signedIn.Content.ReadAsStringAsync());

        foreach (var page in new[] { "/Checkout", "/Shift", "/InventoryAdmin", "/InventoryAdmin/Create", "/Customer", "/FinanceAdmin",
                     "/ReportAdmin", "/SettingsAdmin", "/UserAdmin", "/UserAdmin/Create", "/BackupAdmin", "/AuditLog", "/AdminDashboard",
                     "/ManagerDashboard", "/Refund", "/SalesAdmin", "/Reconciliation", "/Report/ZReport", "/Home", "/" })
        {
            var response = await client.GetAsync(page.Length > 1 && page.Count(c => c == '/') == 1 ? page + "/Index" : page); // default action is Account/Login
            var html = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{page} returned {(int)response.StatusCode} at {response.RequestMessage?.RequestUri}");
            Assert.DoesNotContain("Something went wrong", html);
            Assert.DoesNotContain("Exception", html);
            // BP-09: every script, style, image and font is local; nothing is fetched from the internet.
            Assert.DoesNotMatch("(src|href)=\"(https?:)?//", html);
        }

        // A real POST through the UI: a manual backup into the temporary data root, then it is listed.
        var backupPage = await client.GetStringAsync("/BackupAdmin/Index");
        var backup = await client.PostAsync("/BackupAdmin/TriggerManualBackup",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Token(backupPage) }));
        var after = await backup.Content.ReadAsStringAsync();
        Assert.Contains("made and verified", after);
        Assert.Contains("Verified", after);
    }

    public void Dispose()
    {
        factory.Dispose();
        foreach (var (name, value) in saved) Environment.SetEnvironmentVariable(name, value);
        Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
        try { Directory.Delete(root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
