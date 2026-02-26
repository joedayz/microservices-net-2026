namespace OrderService.Clients;

public record ProductInfo(Guid Id, string Name, string Description, decimal Price, int Stock, DateTime CreatedAt);
