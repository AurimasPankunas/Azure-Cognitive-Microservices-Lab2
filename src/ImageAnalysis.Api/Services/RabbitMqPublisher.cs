using System.Text.Json;
using RabbitMQ.Client;

namespace ImageAnalysis.Api.Services;

/// <summary>
/// Sends analysis events to RabbitMQ.
/// The connection is opened lazily so the API can start even if RabbitMQ is still booting.
/// </summary>
public class RabbitMqPublisher : IAsyncDisposable
{
    private const string QueueName = "analysis-history";

    private readonly ConnectionFactory _factory;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;

    /// <summary>
    /// Reads RabbitMQ connection settings from configuration.
    /// </summary>
    public RabbitMqPublisher(IConfiguration config)
    {
        _factory = new ConnectionFactory
        {
            HostName = config["RabbitMq:Host"] ?? "localhost",
            UserName = config["RabbitMq:User"] ?? "guest",
            Password = config["RabbitMq:Password"] ?? "guest"
        };
    }

    /// <summary>
    /// Creates the RabbitMQ connection and channel on first use.
    /// </summary>
    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is not null)
            return _channel;

        // Only one request may initialize the shared connection at a time.
        await _initLock.WaitAsync(ct);

        try
        {
            if (_channel is null)
            {
                _connection = await _factory.CreateConnectionAsync(ct);
                _channel = await _connection.CreateChannelAsync(cancellationToken: ct);

                // Declare a durable queue so messages survive a RabbitMQ restart.
                await _channel.QueueDeclareAsync(
                    queue: QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    cancellationToken: ct);
            }

            return _channel;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Serializes the event to JSON and publishes it to the history queue.
    /// </summary>
    public async Task PublishAsync<T>(T message, CancellationToken ct)
    {
        var channel = await GetChannelAsync(ct);
        var body = JsonSerializer.SerializeToUtf8Bytes(message);

        // Empty exchange name means "publish directly to the named queue".
        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: QueueName,
            body: body,
            cancellationToken: ct);
    }

    /// <summary>
    /// Releases RabbitMQ resources when the application shuts down.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();

        if (_connection is not null)
            await _connection.DisposeAsync();

        _initLock.Dispose();
    }
}
