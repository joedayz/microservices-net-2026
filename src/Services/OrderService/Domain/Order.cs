namespace OrderService.Domain;

public class Order
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    public Order(string customerName)
    {
        Id = Guid.NewGuid();
        CustomerName = customerName;
        CreatedAt = DateTime.UtcNow;
    }
}
