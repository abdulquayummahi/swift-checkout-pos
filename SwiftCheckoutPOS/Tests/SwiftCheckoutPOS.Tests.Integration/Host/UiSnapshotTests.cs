using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SwiftCheckoutPOS.DAL.Entities;
using SwiftCheckoutPOS.Tests.Integration.Infrastructure;

namespace SwiftCheckoutPOS.Tests.Integration.Host;

/// <summary>
/// UI review aid and render check: seeds a realistic small shop, signs in as Admin and saves the HTML of every main
/// screen to bin/.../ui-snapshots (used for screenshots during design work). Every page must render with status 200.
/// </summary>
[Collection("SqlServer")]
public sealed class UiSnapshotTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "scpos-snap-" + Guid.NewGuid().ToString("N")[..10]);
    private readonly string output = Path.Combine(AppContext.BaseDirectory, "ui-snapshots");
    private readonly WebApplicationFactory<Program> factory;
    private readonly SqlServerFixture fixture;
    private readonly Dictionary<string, string?> saved = new();

    public UiSnapshotTests(SqlServerFixture fixture)
    {
        this.fixture = fixture;
        fixture.Reset();
        Set("ConnectionStrings__DbConn", fixture.Guard.ForTestDatabase());
        Set("Paths__DataRoot", root);
        Set("ASPNETCORE_ENVIRONMENT", "Production");
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Production"));
        Directory.CreateDirectory(output);
    }

    private void Set(string name, string value) { saved[name] = Environment.GetEnvironmentVariable(name); Environment.SetEnvironmentVariable(name, value); }

    private static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    private async Task<string> Save(HttpClient client, string url, string name)
    {
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK || (url.Contains("code=404") && response.StatusCode == HttpStatusCode.NotFound), $"{url} returned {(int)response.StatusCode}");
        File.WriteAllText(Path.Combine(output, name + ".html"), html);
        return html;
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, string url, string token, Dictionary<string, string> form)
    {
        form["__RequestVerificationToken"] = token;
        return client.PostAsync(url, new FormUrlEncodedContent(form));
    }

    [Fact]
    [Trait("Fixes", "UI")]
    public async Task EveryMainScreenRendersWithShopData()
    {
        foreach (var old in Directory.GetFiles(output, "*.html")) File.Delete(old);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true, BaseAddress = new Uri("http://localhost") });

        var setup = await Save(client, "/Account/Setup", "setup");
        Assert.Contains("Set up the shop", setup);
        // A realistic shop made by the same code as --seed-demo (it also creates the owner account).
        new SwiftCheckoutPOS.UI.Infrastructure.DemoDataSeeder(fixture.Guard.ForTestDatabase()).Run(days: 12);
        var login = await Save(client, "/Account/Login", "login");
        var owner = SwiftCheckoutPOS.UI.Infrastructure.DemoDataSeeder.Owner;
        await Post(client, "/Account/Login", Token(login), new() { ["Username"] = owner.Username, ["Password"] = owner.Password });

        int[] ids;
        using (var c = fixture.Context())
            ids = new[] { "8941000000017", "8941000000055", "8941000000109", "8941000000154" }
                .Select(b => c.Products.Single(p => p.Barcode == b).ProductId).ToArray();
        foreach (var id in new[] { ids[0], ids[1], ids[1], ids[2], ids[2], ids[2], ids[3] })
        {
            var till = await client.GetStringAsync("/Checkout/Index");
            await Post(client, "/Checkout/AddById", Token(till), new() { ["productId"] = id.ToString() });
        }

        var pages = new (string Url, string Name)[]
        {
            ("/Checkout/Index", "till"), ("/Shift/Index", "shift"), ("/Refund/Index", "refund"),
            ("/AdminDashboard/Index", "dashboard-admin"), ("/ManagerDashboard/Index", "dashboard-manager"),
            ("/InventoryAdmin/Index", "inventory"), ("/InventoryAdmin/Create", "inventory-create"), ($"/InventoryAdmin/Edit/{ids[0]}", "inventory-edit"),
            ("/Customer/Index", "customers"), ("/FinanceAdmin/Index", "finance"), ("/ReportAdmin/Index", "reports"),
            ("/UserAdmin/Index", "users"), ("/UserAdmin/Create", "users-create"), ("/SettingsAdmin/Index", "settings"),
            ("/BackupAdmin/Index", "backup"), ("/AuditLog/Index", "audit"), ("/Account/Locked", "locked"),
            ("/Home/Error?code=404", "error-404"), ("/Account/AccessDenied", "access-denied")
        };
        foreach (var (url, name) in pages) await Save(client, url, name);
        using (var c = fixture.Context())
        {
            var customerId = c.Customers.OrderByDescending(x => x.Sales.Count).First().CustomerId;
            await Save(client, $"/Customer/Details/{customerId}", "customer-details");
        }
        Assert.Contains("Aromatic rice", File.ReadAllText(Path.Combine(output, "till.html")));
        Assert.Contains("SCP-", File.ReadAllText(Path.Combine(output, "dashboard-admin.html")));
    }

    public void Dispose()
    {
        factory.Dispose();
        foreach (var (name, value) in saved) Environment.SetEnvironmentVariable(name, value);
        Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
        try { Directory.Delete(root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
