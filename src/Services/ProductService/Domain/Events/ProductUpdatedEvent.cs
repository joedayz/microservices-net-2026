namespace ProductService.Domain.Events;

public class ProductUpdatedEvent : DomainEvent
{
    public override string EventType => "product.updated";
    public Guid ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Stock { get; init; }
}
