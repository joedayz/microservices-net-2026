using Microsoft.EntityFrameworkCore;
using OrderService.Domain;

namespace OrderService.Infrastructure;

public class EfOrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;
    private readonly ILogger<EfOrderRepository> _logger;

    public EfOrderRepository(OrderDbContext context, ILogger<EfOrderRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Order>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created order with ID: {OrderId}", order.Id);
        return order;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders.FindAsync(new object[] { id }, cancellationToken);
        if (order == null)
        {
            return false;
        }

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted order with ID: {OrderId}", id);
        return true;
    }
}