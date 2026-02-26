using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductService.Domain.Events;
using RabbitMQ.Client;

namespace ProductService.Infrastructure.Messaging;

public class RabbitMqEventPublisher : IEventPublisher
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqEventPublisher> _logger;

    public RabbitMqEventPublisher(
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString)
        };
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(_options.Exchange, _options.ExchangeType, durable: true, autoDelete: false);
    }

    public Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default)
        where T : DomainEvent
    {
        var json = JsonSerializer.Serialize(domainEvent);
        var body = Encoding.UTF8.GetBytes(json);
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Type = domainEvent.EventType;

        _channel.BasicPublish(
            exchange: _options.Exchange,
            routingKey: domainEvent.EventType,
            mandatory: false,
            basicProperties: properties,
            body: body);

        _logger.LogInformation(
            "Published event {EventType} ({EventId}) to RabbitMQ",
            domainEvent.EventType,
            domainEvent.EventId);

        return Task.CompletedTask;
    }
}
