using System.ComponentModel.DataAnnotations;

namespace OrderService.Application.DTOs;

public class ProductInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

public class CreateOrderItemDto
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public class CreateOrderDto
{
    [Required]
    public string CustomerName { get; set; } = string.Empty;

    [MinLength(1)]
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class OrderDto
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<OrderItemDto> Items { get; set; } = new();
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
}