using Microsoft.AspNetCore.Authentication;
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

var connectionString = builder.Configuration.GetConnectionString("DbConn")
    ?? throw new InvalidOperationException("The DbConn connection string must be supplied through environment-specific configuration or user secrets.");
if (SwiftCheckoutPOS.UI.Infrastructure.MaintenanceCommands.TryRun(args, connectionString)) return; // PR-Q68 owner maintenance

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
builder.Services.AddScoped<BackupService>();
builder.Services.AddScoped<TillReconciliationService>();
builder.Services.AddScoped<RefundService>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<SettingsService>();

// Virtual printing is the safe operational default. A physical driver must be explicitly
// enabled only after its port configuration and device health have been verified.
builder.Services.AddScoped<IReceiptPrinter, VirtualPrinterService>();
builder.Services.AddScoped<Func<IReceiptPrinter>>(services => () => services.GetRequiredService<IReceiptPrinter>());

builder.Services.AddHostedService<AutoBackupWorker>();
builder.Services.AddHostedService<HeldOrderExpiryWorker>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRequestLocalization(new Microsoft.AspNetCore.Builder.RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(SwiftCheckoutPOS.BLL.Pricing.Money.Culture),
    SupportedCultures = [SwiftCheckoutPOS.BLL.Pricing.Money.Culture],
    SupportedUICultures = [SwiftCheckoutPOS.BLL.Pricing.Money.Culture],
    RequestCultureProviders = []
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
