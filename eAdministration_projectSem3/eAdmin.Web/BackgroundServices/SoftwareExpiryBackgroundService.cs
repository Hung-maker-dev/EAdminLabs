using System;
using System.Threading;
using System.Threading.Tasks;
using eAdmin.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace eAdmin.Web.BackgroundServices
{
    /// <summary>
    /// Background Service chạy mỗi ngày lúc 08:00 sáng.
    /// Kiểm tra phần mềm sắp hết hạn và gửi cảnh báo tự động.
    /// </summary>
    public class SoftwareExpiryBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SoftwareExpiryBackgroundService> _logger;

        public SoftwareExpiryBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<SoftwareExpiryBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger       = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SoftwareExpiryBackgroundService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                // Tính thời gian chờ đến 08:00 sáng hôm sau
                var now    = DateTime.Now;
                var nextRun = now.Date.AddDays(1).AddHours(8);
                var delay  = nextRun - now;

                _logger.LogInformation("Next software expiry check at: {NextRun}", nextRun);

                await Task.Delay(delay, stoppingToken);

                try
                {
                    // Tạo scope mới vì INotificationService là Scoped
                    using var scope = _scopeFactory.CreateScope();
                    var notifyService = scope.ServiceProvider
                        .GetRequiredService<INotificationService>();

                    await notifyService.CheckSoftwareExpiryAndNotifyAsync(daysBeforeExpiry: 30);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during software expiry check.");
                }
            }
        }
    }
}
