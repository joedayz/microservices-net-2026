namespace ProductService.Domain.Events;

public class ProductCreatedEvent : DomainEvent
{
    public override string EventType => "product.created";
    public Guid ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Stock { get; init; }
}
