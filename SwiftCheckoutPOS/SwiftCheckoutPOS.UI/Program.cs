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
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DbConn")
    ?? throw new InvalidOperationException("The DbConn connection string must be supplied through environment-specific configuration or user secrets.");

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
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
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder("POSAuthCookie")
        .RequireAuthenticatedUser()
        .Build();
});

    // 2. Data Access Layer Registration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions => sqlOptions.EnableRetryOnFailure()));

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
builder.Services.AddScoped<UserAdminService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ReportService>();
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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();

app.Run();
