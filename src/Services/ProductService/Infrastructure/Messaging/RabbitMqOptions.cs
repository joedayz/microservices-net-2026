namespace ProductService.Infrastructure.Messaging;

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";
    public string ConnectionString { get; set; } = "amqp://guest:guest@localhost:5672";
    public string Exchange { get; set; } = "product-events";
    public string ExchangeType { get; set; } = "topic";
}

