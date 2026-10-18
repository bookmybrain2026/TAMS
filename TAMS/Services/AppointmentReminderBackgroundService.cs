
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TAMS.Services;

namespace TAMS.Services
{
    public class AppointmentReminderBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AppointmentReminderBackgroundService> _logger;

        public AppointmentReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<AppointmentReminderBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Appointment reminder background service started.");

            // Check immediately when the service starts.
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var reminderService = scope.ServiceProvider.GetRequiredService<AppointmentReminderService>();

                    //await reminderService.ProcessDueRemindersAsync(
                    //    stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while processing appointment reminders.");
                }

                try
                {
                    // Check again in one minute
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
            _logger.LogInformation("Appointment reminder background service stopped.");
        }
    }
}
