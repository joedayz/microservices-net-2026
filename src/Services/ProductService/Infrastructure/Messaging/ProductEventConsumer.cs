using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ProductService.Infrastructure.Messaging;

public class ProductEventConsumer : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProductEventConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public ProductEventConsumer(IConfiguration configuration, ILogger<ProductEventConsumer> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var exchange = _configuration["RabbitMq:Exchange"] ?? "product-events";
        var exchangeType = _configuration["RabbitMq:ExchangeType"] ?? "topic";
        var connectionString = _configuration.GetConnectionString("RabbitMq")
            ?? "amqp://guest:guest@localhost:5672";

        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await _channel.ExchangeDeclareAsync(exchange, exchangeType, durable: true, cancellationToken: stoppingToken);

        // Cola exclusiva y auto-eliminable: solo existe mientras corre este servicio
        var queue = await _channel.QueueDeclareAsync(cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queue.QueueName, exchange, routingKey: "product.*", cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            _logger.LogInformation("Event received [{RoutingKey}]: {Body}", ea.RoutingKey, body);
            await Task.CompletedTask;
        };
        await _channel.BasicConsumeAsync(queue.QueueName, autoAck: true, consumer, stoppingToken);

        // Mantener el BackgroundService vivo hasta que se cancele
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown normal
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken);
        if (_connection is not null)
            await _connection.CloseAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }
}
