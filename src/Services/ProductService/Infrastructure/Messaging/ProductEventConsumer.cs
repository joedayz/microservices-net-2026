using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProductService.Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ProductService.Infrastructure.Messaging;

public class ProductEventConsumer : BackgroundService
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<ProductEventConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public ProductEventConsumer(
        IOptions<RabbitMqOptions> options,
        ILogger<ProductEventConsumer> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var factory = new ConnectionFactory { Uri = new Uri(_options.ConnectionString) };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(_options.Exchange, _options.ExchangeType, durable: true, autoDelete: false);
            var queueName = _channel.QueueDeclare("product-events-productservice", durable: true, exclusive: false, autoDelete: false).QueueName;
            _channel.QueueBind(queueName, _options.Exchange, "product.*");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (_, ea) =>
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                _logger.LogInformation("[ProductEventConsumer] Received event {RoutingKey}: {Payload}", ea.RoutingKey, json);
                _channel!.BasicAck(ea.DeliveryTag, false);
            };
            _channel.BasicConsume(queueName, autoAck: false, consumer);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ProductEventConsumer could not connect to RabbitMQ. Events will not be consumed.");
        }
    }

    public override void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        base.Dispose();
    }
}
