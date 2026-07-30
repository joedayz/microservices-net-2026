using ProductService.Domain.Events;

namespace ProductService.Infrastructure.Messaging;

public class LogEventPublisher : IEventPublisher
{
    private readonly ILogger<LogEventPublisher> _logger;

    public LogEventPublisher(ILogger<LogEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default)
        where T : DomainEvent
    {
        _logger.LogInformation(
            "[Fallback] Event {EventType} ({EventId}) not sent — RabbitMQ unavailable",
            domainEvent.EventType, domainEvent.EventId);
        return Task.CompletedTask;
    }
}