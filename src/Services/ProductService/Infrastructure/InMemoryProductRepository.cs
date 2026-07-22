using ProductService.Domain;

namespace ProductService.Infrastructure;


public class InMemoryProductRepository : IProductRepository
{
    private readonly Dictionary<Guid, Product> _products = new();

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _products.TryGetValue(id, out var product);
        return Task.FromResult(product);
    }

    public Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IEnumerable<Product>>(_products.Values);
    }

    public Task<Product> CreateAsync(Product product, CancellationToken cancellationToken = default)
    {
        _products[product.Id] = product;
        return Task.FromResult(product);
    }

    public Task<bool> UpdateAsync(Product product, CancellationToken cancellationToken)
    {
        if (!_products.ContainsKey(product.Id))
            return Task.FromResult(false);
        _products[product.Id] = product;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_products.Remove(id));
    }
}
