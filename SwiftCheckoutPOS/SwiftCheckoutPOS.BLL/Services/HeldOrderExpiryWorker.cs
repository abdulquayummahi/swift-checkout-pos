using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SwiftCheckoutPOS.BLL.Services;

/// <summary>BP-03/PR-Q18: every 10 minutes (and at start-up) cancel held orders older than the configured hours.</summary>
public sealed class HeldOrderExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<HeldOrderExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var hours = scope.ServiceProvider.GetRequiredService<SettingsService>().GetHeldOrderRetentionHours();
                var expired = scope.ServiceProvider.GetRequiredService<CheckoutService>().ExpireHeldOrders(hours, DateTime.Now);
                if (expired > 0) logger.LogInformation("Expired {Count} held order(s) older than {Hours} hours.", expired, hours);
            }
            catch (Exception ex)
            {
                // Never log database text or order contents.
                logger.LogWarning("Held-order expiry check failed ({ErrorType}); will retry.", ex.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
