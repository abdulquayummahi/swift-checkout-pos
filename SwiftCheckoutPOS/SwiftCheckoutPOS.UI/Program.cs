using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
// Project: SwiftCheckoutPOS.UI
// File: Program.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SwiftCheckoutPOS.BLL.Interfaces;
using SwiftCheckoutPOS.BLL.Services;
using SwiftCheckoutPOS.DAL.Context;
using SwiftCheckoutPOS.DAL.Repositories;
// PR-Q01: always BDT and fixed number formatting, never the Windows regional settings.
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = SwiftCheckoutPOS.BLL.Pricing.Money.Culture;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = SwiftCheckoutPOS.BLL.Pricing.Money.Culture;
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService(options => options.ServiceName = "SwiftCheckoutPOS"); // BP-06: no effect when run from a console

// BP-06/T10: machine-local data root (outside the install folder) and machine-local settings file.
var dataPaths = new SwiftCheckoutPOS.BLL.Configuration.DataPaths(builder.Configuration["Paths:DataRoot"]);
dataPaths.EnsureCreated();
builder.Configuration.AddJsonFile(dataPaths.LocalSettingsFile, optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables().AddCommandLine(args); // environment/command line still win over the local file
builder.Services.AddSingleton(dataPaths);

var connectionString = builder.Configuration.GetConnectionString("DbConn");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine($"SwiftCheckoutPOS cannot start: no database connection is configured.\n" +
        $"Create {dataPaths.LocalSettingsFile} as described in DEPLOYMENT_GUIDE.md (ConnectionStrings:DbConn).");
    Environment.ExitCode = 2;
    return;
}
var legacyImageFolder = Path.Combine(builder.Environment.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot"), "images", "products");
if (SwiftCheckoutPOS.UI.Infrastructure.MaintenanceCommands.TryRun(args, connectionString, dataPaths, legacyImageFolder)) return; // PR-Q68 owner maintenance

// BP-06: local rolling log files; console logging stays for development.
builder.Logging.AddProvider(new SwiftCheckoutPOS.UI.Infrastructure.FileLoggerProvider(dataPaths.Logs));

// BP-06/T10: persisted Data Protection keys (cookies/antiforgery survive restarts), DPAPI-protected on Windows.
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("SwiftCheckoutPOS")
    .PersistKeysToFileSystem(new DirectoryInfo(dataPaths.Keys));
if (OperatingSystem.IsWindows()) dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);

// BP-06/T10: outside Development the till listens on this PC only (loopback), never on the network.
var httpsCertificate = builder.Configuration["Hosting:CertificatePath"];
var useHttps = !string.IsNullOrWhiteSpace(httpsCertificate);
if (!builder.Environment.IsDevelopment())
{
    var port = int.TryParse(builder.Configuration["Hosting:Port"], out var p) ? p : 5080;
    builder.WebHost.ConfigureKestrel(kestrel =>
    {
        kestrel.ListenLocalhost(port, listen =>
        {
            if (useHttps) listen.UseHttps(httpsCertificate!, builder.Configuration["Hosting:CertificatePassword"]);
        });
    });
}

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(12); // the 15-minute screen lock is separate (BP-21)
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAuthentication("POSAuthCookie")
    .AddCookie("POSAuthCookie", options =>
    {
        options.Cookie.Name = "SwiftCheckout.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(12); // PR-Q64: absolute 12-hour maximum
        options.SlidingExpiration = false;
        // IMM-09/PC-01: a disabled, demoted or reset account loses access on its next request.
        options.Events.OnValidatePrincipal = async context =>
        {
            var auth = context.HttpContext.RequestServices.GetRequiredService<AuthService>();
            var idText = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var state = int.TryParse(idText, out var id) ? auth.SecurityState(id) : null;
            if (state is not { Active: true } ||
                state.Value.Role != context.Principal!.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ||
                state.Value.Stamp != context.Principal.FindFirst("stamp")?.Value)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync("POSAuthCookie");
            }
        };
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, SwiftCheckoutPOS.UI.Infrastructure.FinancialViewHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(SwiftCheckoutPOS.UI.Infrastructure.FinancialViewRequirement.Policy,
        policy => policy.RequireAuthenticatedUser().AddRequirements(new SwiftCheckoutPOS.UI.Infrastructure.FinancialViewRequirement()));
    options.FallbackPolicy = new AuthorizationPolicyBuilder("POSAuthCookie")
        .RequireAuthenticatedUser()
        .Build();
});

    // 2. Data Access Layer Registration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions => sqlOptions.EnableRetryOnFailure()));

builder.Services.AddScoped<AtomicUnit>();
builder.Services.AddScoped<MetricsRepository>();
builder.Services.AddScoped<MetricsService>();
builder.Services.AddScoped<ShiftRepository>();
builder.Services.AddScoped<ShiftService>();
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<InventoryRepository>();
builder.Services.AddScoped<SalesRepository>();
builder.Services.AddScoped<ICheckoutRepository, CheckoutRepository>();
builder.Services.AddScoped<CustomerRepository>();
builder.Services.AddScoped<BackupRepository>();
builder.Services.AddScoped<TillReconciliationRepository>();
builder.Services.AddScoped<FinanceRepository>();
builder.Services.AddScoped<SettingsRepository>();

builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<SwiftCheckoutPOS.BLL.Security.LoginThrottle>();
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<IApprover, ApprovalService>();
builder.Services.AddScoped<CheckoutService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReconciliationService>();
builder.Services.AddScoped(services => new BackupService(services.GetRequiredService<BackupRepository>(), dataPaths,
    services.GetRequiredService<AtomicUnit>(), null, [legacyImageFolder]));
builder.Services.AddSingleton(new SwiftCheckoutPOS.BLL.Media.ProductImageStore(dataPaths.ProductImages, [legacyImageFolder]));
builder.Services.AddScoped<TillReconciliationService>();
builder.Services.AddScoped<RefundService>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<SettingsService>();

// Virtual printing is the safe operational default. A physical driver must be explicitly
// enabled only after its port configuration and device health have been verified.
// BP-08: printer from machine configuration "Printer:Mode" = Virtual (default) | EscPosSerial | EscPosWindows.
builder.Services.AddScoped<ReceiptService>();
builder.Services.AddScoped<IReceiptPrinter>(services =>
{
    var config = services.GetRequiredService<IConfiguration>();
    var settings = services.GetRequiredService<SettingsService>();
    Func<SwiftCheckoutPOS.BLL.Hardware.ReceiptHeaderDTO> header = settings.GetReceiptHeader;
    return config["Printer:Mode"] switch
    {
        "EscPosSerial" => new SwiftCheckoutPOS.BLL.Hardware.EscPosPrinter(new SwiftCheckoutPOS.BLL.Hardware.SerialTransport(
            config["Printer:Port"] ?? "COM3", int.TryParse(config["Printer:BaudRate"], out var baud) ? baud : 9600), header),
        "EscPosWindows" => new SwiftCheckoutPOS.BLL.Hardware.EscPosPrinter(new SwiftCheckoutPOS.BLL.Hardware.WindowsPrinterTransport(
            config["Printer:Name"] ?? "POS-80"), header),
        _ => new VirtualPrinterService(header, config["Paths:VirtualReceipts"] ?? dataPaths.VirtualReceipts)
    };
});
builder.Services.AddScoped<Func<IReceiptPrinter>>(services => () => services.GetRequiredService<IReceiptPrinter>());

builder.Services.AddHostedService<AutoBackupWorker>();
builder.Services.AddHostedService<HeldOrderExpiryWorker>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    if (useHttps) app.UseHsts();
}

if (app.Environment.IsDevelopment() || useHttps) app.UseHttpsRedirection();
app.UseRequestLocalization(new Microsoft.AspNetCore.Builder.RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(SwiftCheckoutPOS.BLL.Pricing.Money.Culture),
    SupportedCultures = [SwiftCheckoutPOS.BLL.Pricing.Money.Culture],
    SupportedUICultures = [SwiftCheckoutPOS.BLL.Pricing.Money.Culture],
    RequestCultureProviders = []
});
// IMM-10/BP-06: uploaded product images live in DataRoot\Media\products; older images still come from wwwroot.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(dataPaths.ProductImages),
    RequestPath = "/images/products"
});
app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseMiddleware<SwiftCheckoutPOS.UI.Infrastructure.IdleLockMiddleware>(); // BP-21
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();
