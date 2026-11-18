using HCMS4.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HCMS4.Services
{
    public class DailyReportBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailyReportBackgroundService> _logger;

        public DailyReportBackgroundService(IServiceProvider serviceProvider, ILogger<DailyReportBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait until we reach midnight (or shortly after) on first run
            await WaitForMidnight(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunDailyTasksAsync();

                // Wait 24 hours before next execution
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);

                // Wait until midnight again
                await WaitForMidnight(stoppingToken);
            }
        }

        /// <summary>The nightly work: close past appointments and store yesterday's pharmacy report.</summary>
        public async Task RunDailyTasksAsync()
        {
            using var scope = _serviceProvider.CreateScope();

            try
            {
                // Past appointments no longer wait for an admin to open the appointments page (issue #15).
                var appointmentService = scope.ServiceProvider.GetRequiredService<IAppointmentService>();
                await appointmentService.UpdatePastScheduledToCompletedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing past appointments");
            }

            try
            {
                var reportService = scope.ServiceProvider.GetRequiredService<IDailyReportService>();

                var yesterday = DateTime.Today.AddDays(-1);
                var result = await reportService.GenerateDailyReportAsync(yesterday);

                if (result)
                {
                    _logger.LogInformation("Auto-generated daily report for {Date}", yesterday);
                }
                else
                {
                    _logger.LogWarning("Failed to generate daily report for {Date}", yesterday);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error auto-generating daily report");
            }
        }

        private async Task WaitForMidnight(CancellationToken stoppingToken)
        {
            var now = DateTime.Now;
            var nextMidnight = now.Date.AddDays(1); // Next midnight
            var delay = nextMidnight - now;

            // If we're within 5 minutes of midnight, just proceed
            if (delay.TotalMinutes < 5)
            {
                _logger.LogInformation("Close to midnight, proceeding with report generation");
                return;
            }

            // If the app just started and it's not near midnight, don't wait - 
            // instead generate the previous day's report immediately if missing
            _logger.LogInformation("Next scheduled run at {NextMidnight}. Waiting...", nextMidnight);
            
            // Wait until midnight, but check cancellation token periodically
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Service is shutting down
                _logger.LogInformation("Background service cancellation requested during wait");
            }
        }
    }
}
