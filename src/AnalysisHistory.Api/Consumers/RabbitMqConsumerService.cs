using System.Text.Json;
using System.Text.RegularExpressions;
using AnalysisHistory.Api.Data;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Contracts;

namespace AnalysisHistory.Api.Consumers;

/// <summary>
/// Background worker that consumes image analysis events from RabbitMQ,
/// calculates statistics and stores the result in SQLite.
/// </summary>
public class RabbitMqConsumerService : BackgroundService
{
    private const string QueueName = "analysis-history";

    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqConsumerService> _logger;

    /// <summary>
    /// Receives configuration and a scope factory for creating scoped DbContext instances.
    /// </summary>
    public RabbitMqConsumerService(
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMqConsumerService> logger)
    {
        _config = config;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Keeps a RabbitMQ connection alive and reconnects if the broker becomes unavailable.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _config["RabbitMq:Host"] ?? "localhost",
            UserName = _config["RabbitMq:User"] ?? "guest",
            Password = _config["RabbitMq:Password"] ?? "guest"
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(
                    cancellationToken: stoppingToken);

                // Durable queue means messages survive a RabbitMQ restart.
                await channel.QueueDeclareAsync(
                    queue: QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);

                // RabbitMQ calls this handler for every delivered message.
                consumer.ReceivedAsync += async (_, eventArgs) =>
                {
                    await HandleMessageAsync(channel, eventArgs, stoppingToken);
                };

                // autoAck: false means we acknowledge a message only after successful processing.
                await channel.BasicConsumeAsync(
                    queue: QueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("RabbitMQ consumer is listening on queue {Queue}", QueueName);

                // Keep the background service alive until the application stops.
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RabbitMQ consumer failed. Retrying in 5 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Deserializes one message, calculates text statistics and saves an AnalysisRecord.
    /// </summary>
    private async Task HandleMessageAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken ct)
    {
        try
        {
            var message = JsonSerializer.Deserialize<ImageAnalyzedEvent>(
                eventArgs.Body.Span);

            if (message is null)
            {
                _logger.LogWarning("Received an empty or invalid message.");
                await channel.BasicAckAsync(eventArgs.DeliveryTag, false, ct);
                return;
            }

            // A hosted service is a singleton, so DbContext must be resolved per message.
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnalysisDbContext>();

            // Idempotency: ignore duplicate deliveries of the same analysis id.
            var alreadyExists = await db.Analyses
                .AnyAsync(x => x.Id == message.Id, ct);

            if (alreadyExists)
            {
                _logger.LogInformation(
                    "Analysis {Id} already exists; skipping duplicate.",
                    message.Id);

                await channel.BasicAckAsync(eventArgs.DeliveryTag, false, ct);
                return;
            }

            // Split text into words; \p{L}+ matches Unicode letters (including Lithuanian).
            var words = Regex.Matches(message.Text.ToLowerInvariant(), @"\p{L}+")
                .Select(match => match.Value)
                .ToList();

            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ir", "kad", "yra", "the", "and", "of", "to", "in", "a", "is",
                "this", "that", "with", "for", "on", "by"
            };

            // Count word frequencies and keep the ten most common meaningful words.
            var topKeywords = words
                .Where(word => word.Length > 2 && !stopWords.Contains(word))
                .GroupBy(word => word)
                .OrderByDescending(group => group.Count())
                .Take(10)
                .Select(group => new
                {
                    word = group.Key,
                    count = group.Count()
                })
                .ToList();

            var record = new AnalysisRecord
            {
                Id = message.Id,
                FileName = message.FileName,
                Text = message.Text,
                WordCount = words.Count,
                UniqueWordCount = words.Distinct().Count(),
                TopKeywordsJson = JsonSerializer.Serialize(topKeywords),
                CreatedAt = message.AnalyzedAt
            };

            db.Analyses.Add(record);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Saved analysis {Id} for {FileName} with {WordCount} words.",
                record.Id, record.FileName, record.WordCount);

            // Acknowledge only after the database write succeeded.
            await channel.BasicAckAsync(eventArgs.DeliveryTag, false, ct);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON message. Dropping it.");

            // Malformed messages will never succeed, so do not requeue them.
            await channel.BasicNackAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message. Requeueing it.");

            // Transient errors (for example database lock) are retried by RabbitMQ.
            await channel.BasicNackAsync(
                eventArgs.DeliveryTag,
                multiple: false,
                requeue: true,
                cancellationToken: ct);
        }
    }
}
