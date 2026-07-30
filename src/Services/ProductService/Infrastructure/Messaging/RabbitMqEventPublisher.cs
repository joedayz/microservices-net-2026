using System.Text;
using System.Text.Json;
using ProductService.Domain.Events;
using RabbitMQ.Client;

namespace ProductService.Infrastructure.Messaging;

public class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly string _exchange;
    private readonly string _exchangeType;
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly ILogger<RabbitMqEventPublisher> _logger;

    public RabbitMqEventPublisher(IConfiguration configuration, ILogger<RabbitMqEventPublisher> logger)
    {
        _logger = logger;
        _exchange = configuration["RabbitMq:Exchange"] ?? "product-events";
        _exchangeType = configuration["RabbitMq:ExchangeType"] ?? "topic";

        var connectionString = configuration.GetConnectionString("RabbitMq")
            ?? "amqp://guest:guest@localhost:5672";

        // RabbitMQ.Client 7.x es async; el ctor de DI no puede ser async,
        // así que inicializamos de forma síncrona al arrancar el singleton.
        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
        _channel.ExchangeDeclareAsync(_exchange, _exchangeType, durable: true).GetAwaiter().GetResult();
    }

    public async Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default)
        where T : DomainEvent
    {
        var json = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await _channel.BasicPublishAsync(
            exchange: _exchange,
            routingKey: domainEvent.EventType,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Published event {EventType} ({EventId})", domainEvent.EventType, domainEvent.EventId);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
    }
}
