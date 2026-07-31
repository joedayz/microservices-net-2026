using System;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Functions;

public class ProcessProductEvent
{
    private readonly ILogger<ProcessProductEvent> _logger;

    public ProcessProductEvent(ILogger<ProcessProductEvent> logger)
    {
        _logger = logger;
    }

    [Function(nameof(ProcessProductEvent))]
    public async Task Run(
        [ServiceBusTrigger("product-events", "product-events-sub", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Message received: MessageId = {MessageId}", message.MessageId);

        try
        {
            var body = message.Body.ToString();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("EventType", out var eventType))
                _logger.LogInformation("EventType: {EventType}", eventType.GetString());
            if (root.TryGetProperty("ProductId", out var productId))
                _logger.LogInformation("ProductId: {ProductId}", productId.GetString());

            // Aquí podrías: actualizar caché, notificar, escribir en otro sistema, etc.
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            throw;
        }
    }
}