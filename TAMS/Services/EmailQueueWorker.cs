using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace TAMS.Services;

public sealed class EmailQueueWorker : BackgroundService
{
    private readonly EmailOutbox _outbox;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<EmailQueueWorker> _logger;
    private const int WorkerCount = 3;
    private const int MaxAttempts = 6;

    public EmailQueueWorker(EmailOutbox outbox, IServiceScopeFactory scopes,
        ILogger<EmailQueueWorker> logger)
    { _outbox = outbox; _scopes = scopes; _logger = logger; }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(0, WorkerCount).Select(_ => RunAsync(stoppingToken)));

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var job = await _outbox.ClaimAsync(ct);
                if (job is null) { await Task.Delay(TimeSpan.FromSeconds(2), ct); continue; }
                await ProcessAsync(job, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email worker polling failed");
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            }
        }
    }

    private async Task ProcessAsync(EmailJob job, CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<SmtpEmailService>();
            var method = typeof(SmtpEmailService).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SingleOrDefault(m => m.Name == job.Method && m.ReturnType == typeof(Task));
            if (method is null) throw new InvalidOperationException("Unknown email method: " + job.Method);
            var parameters = method.GetParameters();
            using var doc = JsonDocument.Parse(job.ArgumentsJson);
            var items = doc.RootElement.EnumerateArray().ToArray();
            if (items.Length != parameters.Length) throw new InvalidOperationException("Invalid email arguments");
            var args = parameters.Select((p, i) =>
                JsonSerializer.Deserialize(items[i].GetRawText(), p.ParameterType)).ToArray();
            _logger.LogInformation("Sending email job {Id}, queued {QueuedUtc}, attempt {Attempt}",
                job.Id, job.CreatedUtc, job.Attempts);
            await (Task)method.Invoke(sender, args)!;
            await UpdateOwnedAsync(job, Builders<EmailJob>.Update
                .Set(j => j.Status, "AcceptedByBrevo")
                .Set(j => j.AcceptedUtc, DateTime.UtcNow)
                .Unset(j => j.LeaseUntilUtc), ct);
            _logger.LogInformation("Brevo accepted email job {Id}; queue wait {Seconds}s",
                job.Id, (DateTime.UtcNow - job.CreatedUtc).TotalSeconds);
        }
        catch (Exception error) when (!(error is OperationCanceledException && ct.IsCancellationRequested))
        {
            var ex = error is TargetInvocationException tie && tie.InnerException is not null
                ? tie.InnerException : error;
            bool transient = ex is HttpRequestException http &&
                (http.StatusCode is null || http.StatusCode == HttpStatusCode.TooManyRequests ||
                 (int)http.StatusCode >= 500);
            transient |= ex is TaskCanceledException || ex is TimeoutException;
            bool retry = transient && job.Attempts < MaxAttempts;
            var backoff = TimeSpan.FromSeconds(Math.Min(900, 15 * Math.Pow(2, job.Attempts - 1)));
            await UpdateOwnedAsync(job, Builders<EmailJob>.Update
                .Set(j => j.Status, retry ? "Pending" : "Failed")
                .Set(j => j.LastError, ex.GetType().Name + ": " + ex.Message)
                .Set(j => j.NextAttemptUtc, DateTime.UtcNow.Add(backoff))
                .Unset(j => j.LeaseUntilUtc), CancellationToken.None);
            _logger.LogError(ex, "Email job {Id} failed. Retry={Retry}, attempt={Attempt}",
                job.Id, retry, job.Attempts);
        }
    }

    private Task UpdateOwnedAsync(EmailJob job, UpdateDefinition<EmailJob> update, CancellationToken ct)
    {
        var filter = Builders<EmailJob>.Filter.Eq(j => j.Id, job.Id) &
                     Builders<EmailJob>.Filter.Eq(j => j.LeaseId, job.LeaseId) &
                     Builders<EmailJob>.Filter.Eq(j => j.Status, "Processing");
        return _outbox.Jobs.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
