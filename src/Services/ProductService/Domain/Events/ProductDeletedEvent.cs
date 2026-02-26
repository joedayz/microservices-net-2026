namespace ProductService.Domain.Events;

public class ProductDeletedEvent : DomainEvent
{
    public override string EventType => "product.deleted";
    public Guid ProductId { get; init; }
}
