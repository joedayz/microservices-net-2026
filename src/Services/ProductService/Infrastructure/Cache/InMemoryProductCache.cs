using Microsoft.Extensions.Caching.Memory;
using ProductService.Application.DTOs;

namespace ProductService.Infrastructure.Cache;

public class InMemoryProductCache: IProductCache
{

    private readonly IMemoryCache _cache;
    private readonly ILogger<InMemoryProductCache> _logger;
    private const string AllProductsKey = "products:all";

    public InMemoryProductCache(
        IMemoryCache cache,
        ILogger<InMemoryProductCache> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<ProductDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = $"products:{id}";
        _cache.TryGetValue(key, out ProductDto? product);
        return Task.FromResult(product);
    }

    public Task<IEnumerable<ProductDto>?> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _cache.TryGetValue(AllProductsKey, out IEnumerable<ProductDto>? products);
        return Task.FromResult(products);
    }

    public Task SetAsync(Guid id, ProductDto product, CancellationToken cancellationToken = default)
    {
        var key = $"products:{id}";
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
            SlidingExpiration = TimeSpan.FromMinutes(1)
        };
        _cache.Set(key, product, options);
        _cache.Remove(AllProductsKey);
        return Task.CompletedTask;
    }

    public Task SetAllAsync(IEnumerable<ProductDto> products, CancellationToken cancellationToken = default)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
            SlidingExpiration = TimeSpan.FromMinutes(1)
        };
        _cache.Set(AllProductsKey, products, options);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = $"products:{id}";
        _cache.Remove(key);
        _cache.Remove(AllProductsKey);
        return Task.CompletedTask;
    }

    public Task RemoveAllAsync(CancellationToken cancellationToken = default)
    {
        _cache.Remove(AllProductsKey);
        return Task.CompletedTask;
    }
}
