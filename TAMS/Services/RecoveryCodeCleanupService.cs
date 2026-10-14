using MongoDB.Driver;
using TAMS.Data;
using TAMS.Models;

public class RecoveryCodeCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecoveryCodeCleanupService> _logger;

    public RecoveryCodeCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<RecoveryCodeCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();

                var db = scope.ServiceProvider
                    .GetRequiredService<MongoDBContext>();

                var filter = Builders<PasswordRecovery>.Filter
                    .Eq(recovery => recovery.IsUsed, true);

                var result = await db.PasswordRecoveries.DeleteManyAsync(
                    filter,
                    stoppingToken);

                _logger.LogInformation(
                    "Recovery code cleanup deleted {Count} used codes.",
                    result.DeletedCount);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Recovery code cleanup failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}