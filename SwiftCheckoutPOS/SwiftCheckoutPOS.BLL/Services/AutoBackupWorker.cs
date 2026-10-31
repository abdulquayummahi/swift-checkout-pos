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
    public class AutoBackupWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AutoBackupWorker> _logger;
        private DateTime _lastBackupDate = DateTime.MinValue;

        public AutoBackupWorker(IServiceScopeFactory scopeFactory, ILogger<AutoBackupWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var backupService = scope.ServiceProvider.GetRequiredService<BackupService>();
                        var config = backupService.GetConfiguration();

                        if (config.IsAutoBackupEnabled)
                        {
                            var now = DateTime.Now;

                            // Check if current time matches the target time (Hours and Minutes) 
                            // and ensure we only backup once per calendar day.
                            if (now.Hour == config.AutoBackupTime.Hours &&
                                now.Minute == config.AutoBackupTime.Minutes &&
                                _lastBackupDate.Date != now.Date)
                            {
                                _logger.LogInformation("Autonomous scheduled backup initiated.");
                                backupService.ExecuteManualBackup();
                                _lastBackupDate = now;
                                _logger.LogInformation("Autonomous scheduled backup completed successfully.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Autonomous Backup Worker Fault: {ex.Message}");
                }

                // Sleep the thread for 30 seconds to optimize CPU cycles before checking the clock again
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}