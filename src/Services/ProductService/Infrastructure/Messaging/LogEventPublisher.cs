using System.Text.Json;
using Microsoft.Extensions.Logging;
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
        var json = JsonSerializer.Serialize(domainEvent);
        _logger.LogInformation(
            "[LogEventPublisher] Event {EventType} ({EventId}): {Payload}",
            domainEvent.EventType,
            domainEvent.EventId,
            json);
        return Task.CompletedTask;
    }
}
