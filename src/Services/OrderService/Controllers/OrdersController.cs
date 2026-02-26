using Microsoft.AspNetCore.Mvc;
using OrderService.Clients;
using OrderService.Domain;
using OrderService.DTOs;

namespace OrderService.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductServiceClient _productClient;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IOrderRepository orderRepository,
        IProductServiceClient productClient,
        ILogger<OrdersController> logger)
    {
        _orderRepository = orderRepository;
        _productClient = productClient;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAll(CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.GetAllAsync(cancellationToken);
        return Ok(orders.Select(MapToDto));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken);
        if (order == null)
            return NotFound();
        return Ok(MapToDto(order));
    }

    [HttpGet("available-products")]
    [ProducesResponseType(typeof(IEnumerable<ProductInfo>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductInfo>>> GetAvailableProducts(CancellationToken cancellationToken)
    {
        var products = await _productClient.GetAvailableProductsAsync(cancellationToken);
        return Ok(products);
    }

    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var order = new Order(dto.CustomerName);

        foreach (var item in dto.Items)
        {
            var product = await _productClient.GetProductAsync(item.ProductId, cancellationToken);
            if (product == null)
                return BadRequest($"Product {item.ProductId} not found");
            if (product.Stock < item.Quantity)
                return BadRequest($"Product {product.Name} has insufficient stock. Available: {product.Stock}");

            order.Items.Add(new OrderItem(
                order.Id, product.Id, product.Name, product.Price, item.Quantity));
        }

        var created = await _orderRepository.CreateAsync(order, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, MapToDto(created));
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _orderRepository.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private static OrderDto MapToDto(Order order) => new()
    {
        Id = order.Id,
        CustomerName = order.CustomerName,
        CreatedAt = order.CreatedAt,
        Items = order.Items.Select(i => new OrderItemDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            UnitPrice = i.UnitPrice,
            Quantity = i.Quantity
        }).ToList()
    };
}
