using OrderService.Domain;

namespace OrderService.Infrastructure;

public class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<Guid, Order> _orders = new();

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_orders.TryGetValue(id, out var order) ? order : null);

    public Task<IEnumerable<Order>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<Order>>(_orders.Values.OrderBy(o => o.CreatedAt).ToList());

    public Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default)
    {
        _orders[order.Id] = order;
        return Task.FromResult(order);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_orders.Remove(id));
    }
}
