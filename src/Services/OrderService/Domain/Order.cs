namespace OrderService.Domain;

public class Order
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal Total => Items.Sum(i => i.UnitPrice * i.Quantity);
    public DateTime CreatedAt { get; set; }

    // Requerido por EF Core: no puede bindear navegaciones (Items) a parámetros de ctor
    private Order() { }

    public Order(string customerName, List<OrderItem> items)
    {
        Id = Guid.NewGuid();
        CustomerName = customerName;
        Items = items;
        CreatedAt = DateTime.UtcNow;
    }
}

public class OrderItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}