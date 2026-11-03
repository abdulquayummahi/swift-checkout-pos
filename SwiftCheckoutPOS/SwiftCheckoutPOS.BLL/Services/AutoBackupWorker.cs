// Project: SwiftCheckoutPOS.BLL
// File: Services/AutoBackupWorker.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SwiftCheckoutPOS.BLL.Services
{
    /// <summary>
    /// PR-Q53/Q89: daily automatic backup at the configured time (23:00) with catch-up. Every minute it asks
    /// BackupService whether a verified backup exists since the last scheduled time; if not, it makes one.
    /// After a failure it waits 15 minutes before trying again, so a full disk does not loop.
    /// </summary>
    public class AutoBackupWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AutoBackupWorker> _logger;

        public AutoBackupWorker(IServiceScopeFactory scopeFactory, ILogger<AutoBackupWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); } // let SQL Server finish starting
            catch (OperationCanceledException) { return; }
            var retryAfter = DateTime.MinValue;
            while (!stoppingToken.IsCancellationRequested)
            {
                if (DateTime.Now >= retryAfter)
                {
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var backups = scope.ServiceProvider.GetRequiredService<BackupService>();
                        if (backups.IsScheduledBackupDue())
                        {
                            _logger.LogInformation("Scheduled backup started.");
                            var set = backups.RunBackup("Daily", "SYSTEM (scheduled)");
                            _logger.LogInformation("Scheduled backup {Set} verified ({Size} MB).", set.SetName, set.SizeMB);
                        }
                    }
                    catch (Exception ex)
                    {
                        retryAfter = DateTime.Now.AddMinutes(15);
                        _logger.LogError("Scheduled backup failed; retrying after {Retry:HH:mm}. {Message}", retryAfter, ex.Message);
                    }
                }
                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
