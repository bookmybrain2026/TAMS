using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Microsoft.Extensions.Configuration;

namespace TAMS.Services;

public sealed class EmailJob
{
    [BsonId] public ObjectId Id { get; set; }
    public string Method { get; set; } = "";
    public string ArgumentsJson { get; set; } = "[]";
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime NextAttemptUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LeaseUntilUtc { get; set; }
    public string? LeaseId { get; set; }
    public DateTime? AcceptedUtc { get; set; }
    public string? LastError { get; set; }
}

public sealed class EmailOutbox
{
    public IMongoCollection<EmailJob> Jobs { get; }

    public EmailOutbox(IConfiguration configuration)
    {
        // Explicit settings avoid assuming the structure of your existing MongoDBContext.
        var connection = configuration["EmailOutbox:ConnectionString"];
        var database = configuration["EmailOutbox:DatabaseName"];
        if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(database))
            throw new InvalidOperationException("Set EmailOutbox:ConnectionString and EmailOutbox:DatabaseName.");
        var client = new MongoClient(connection);
        Jobs = client.GetDatabase(database).GetCollection<EmailJob>("EmailOutbox");
    }

    public async Task EnqueueAsync(string method, object?[] args)
    {
        await Jobs.InsertOneAsync(new EmailJob
        {
            Method = method,
            ArgumentsJson = JsonSerializer.Serialize(args)
        });
    }

    public async Task<EmailJob?> ClaimAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<EmailJob>.Filter.Or(
            Builders<EmailJob>.Filter.And(
                Builders<EmailJob>.Filter.Eq(j => j.Status, "Pending"),
                Builders<EmailJob>.Filter.Lte(j => j.NextAttemptUtc, now)),
            Builders<EmailJob>.Filter.And(
                Builders<EmailJob>.Filter.Eq(j => j.Status, "Processing"),
                Builders<EmailJob>.Filter.Lte(j => j.LeaseUntilUtc, now)));
        var update = Builders<EmailJob>.Update
            .Set(j => j.Status, "Processing")
            .Set(j => j.LeaseUntilUtc, now.AddMinutes(5))
            .Set(j => j.LeaseId, Guid.NewGuid().ToString("N"))
            .Inc(j => j.Attempts, 1);
        return await Jobs.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<EmailJob> {
                Sort = Builders<EmailJob>.Sort.Ascending(j => j.CreatedUtc),
                ReturnDocument = ReturnDocument.After
            }, ct);
    }
}
