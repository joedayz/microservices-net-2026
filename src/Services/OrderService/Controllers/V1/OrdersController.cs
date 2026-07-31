using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Application.DTOs;
using OrderService.Clients;
using OrderService.Domain;

namespace OrderService.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]                                          // ← Protege TODO el controller
public class OrdersController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductServiceClient _productServiceClient;

    public OrdersController(IOrderRepository orderRepository, IProductServiceClient productServiceClient)
    {
        _orderRepository = orderRepository;
        _productServiceClient = productServiceClient;
    }

    [HttpGet]
    [AllowAnonymous]                                 // ← Listar órdenes es público
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAll(CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.GetAllAsync(cancellationToken);
        return Ok(orders.Select(ToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken);
        if (order == null) return NotFound($"Order with ID {id} not found");
        return Ok(ToDto(order));
    }

    // IMPORTANTE: esta ruta debe declararse antes que "{id}" para que no colisione
    [HttpGet("available-products")]
    [AllowAnonymous]  
    public async Task<ActionResult<IEnumerable<ProductInfoDto>>> GetAvailableProducts(CancellationToken cancellationToken)
    {
        var products = await _productServiceClient.GetAvailableProductsAsync(cancellationToken);
        return Ok(products);
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")] 
    public async Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var items = new List<OrderItem>();
        foreach (var item in dto.Items)
        {
            var product = await _productServiceClient.GetProductByIdAsync(item.ProductId, cancellationToken);
            if (product == null)
                return BadRequest($"Product {item.ProductId} not found");

            items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = item.Quantity
            });
        }

        var order = new Order(dto.CustomerName, items);
        await _orderRepository.CreateAsync(order, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = order.Id, version = "1.0" }, ToDto(order));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")] 
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _orderRepository.DeleteAsync(id, cancellationToken);
        if (!deleted) return NotFound($"Order with ID {id} not found");
        return NoContent();
    }

    private static OrderDto ToDto(Order order) => new()
    {
        Id = order.Id,
        CustomerName = order.CustomerName,
        Total = order.Total,
        CreatedAt = order.CreatedAt,
        Items = order.Items.Select(i => new OrderItemDto
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            UnitPrice = i.UnitPrice,
            Quantity = i.Quantity
        }).ToList()
    };
}