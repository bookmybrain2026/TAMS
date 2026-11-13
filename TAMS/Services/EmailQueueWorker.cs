using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TAMS.Services;

// Background service that takes emails from queue and sends them without making users wait

public sealed class EmailQueueWorker : BackgroundService
{
    private readonly EmailQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailQueueWorker> _logger;

    private const int WorkerCount = 3; // Allow up to 3 email jobs to be processed at the same time.

    public EmailQueueWorker(EmailQueue queue, IServiceScopeFactory scopeFactory, ILogger<EmailQueueWorker> logger)
    {
        _queue = queue;                 // Used to retrieve email jobs waiting to be processed.
        _scopeFactory = scopeFactory;   // Creates a separate DI scope for each email job.
        _logger = logger;               // Records errors if an email job fails.
    }

    // Called automatically when the application starts this background service
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable.Range(0, WorkerCount).Select(_ => ProcessQueueAsync(stoppingToken));   // Start 3 workers so multiple emails can be processed at once.
        return Task.WhenAll(workers);                                                                   // Keep the background service running until all workers finish.
    }

    // Each worker waits for queued jobs and processes them one at a time.
    private async Task ProcessQueueAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();                              // Give this email job its own DI scope. Lets scoped services be created and disposed safely.
                var sender = scope.ServiceProvider.GetRequiredService<SmtpEmailService>();  // Get the real SMTP sender, not the service that only queues emails.
                await job(sender);                                                          // Run the queued job using the real email sender. This is where the actual email sending happens.
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break; // The application is shutting down, so stop this worker.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Queued email delivery failed.");
            }
        }
    }


}
