using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using ProductService.Application.Configuration;
using ProductService.Application.DTOs;

namespace ProductService.Infrastructure.Cache;

public class RedisProductCache: IProductCache
{

    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisProductCache> _logger;
    private readonly CacheSettings _cacheSettings;
    private readonly FeatureFlagSettings _featureFlags;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RedisProductCache(
        IDistributedCache cache,
        IOptionsSnapshot<CacheSettings> cacheSettings,
        IOptionsSnapshot<FeatureFlagSettings> featureFlags,
        ILogger<RedisProductCache> logger)
    {
        _cache = cache;
        _cacheSettings = cacheSettings.Value;
        _featureFlags = featureFlags.Value;
        _logger = logger;
    }


    private string AllProductsKey => $"{_cacheSettings.KeyPrefix}:all";
    private string ProductKey(Guid id) => $"{_cacheSettings.KeyPrefix}:{id}";

    private DistributedCacheEntryOptions GetCacheOptions() => new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.AbsoluteExpirationMinutes),
        SlidingExpiration = _cacheSettings.SlidingExpirationMinutes > 0
            ? TimeSpan.FromMinutes(_cacheSettings.SlidingExpirationMinutes)
            : null
    };



    public async Task<ProductDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_cacheSettings.Enabled) return null;

        var cached = await _cache.GetStringAsync(ProductKey(id), cancellationToken);

        if (string.IsNullOrEmpty(cached))
        {
            if (_featureFlags.EnableCacheLogging)
                _logger.LogDebug("Cache MISS for product {ProductId}", id);
            return null;
        }


        if (_featureFlags.EnableCacheLogging)
            _logger.LogDebug("Cache HIT for product {ProductId}", id);

        return JsonSerializer.Deserialize<ProductDto>(cached, JsonOptions);
    }

    public async Task<IEnumerable<ProductDto>?> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_cacheSettings.Enabled) return null;

        var cached = await _cache.GetStringAsync(AllProductsKey, cancellationToken);

        if (string.IsNullOrEmpty(cached))
        {
            if (_featureFlags.EnableCacheLogging)
                _logger.LogDebug("Cache MISS for all products");
            return null;
        }

        if (_featureFlags.EnableCacheLogging)
            _logger.LogDebug("Cache HIT for all products");


        return JsonSerializer.Deserialize<IEnumerable<ProductDto>>(cached, JsonOptions);
    }

    public async Task SetAsync(Guid id, ProductDto product, CancellationToken cancellationToken = default)
    {
        if (!_cacheSettings.Enabled) return;

        var json = JsonSerializer.Serialize(product, JsonOptions);
        await _cache.SetStringAsync(ProductKey(id), json, GetCacheOptions(), cancellationToken);
        await RemoveAllAsync(cancellationToken);
    }

    public async Task SetAllAsync(IEnumerable<ProductDto> products, CancellationToken cancellationToken = default)
    {
        if (!_cacheSettings.Enabled) return;

        var json = JsonSerializer.Serialize(products, JsonOptions);
        await _cache.SetStringAsync(AllProductsKey, json, GetCacheOptions(), cancellationToken);
    }

    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(ProductKey(id), cancellationToken);
        await RemoveAllAsync(cancellationToken);
    }

    public async Task RemoveAllAsync(CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(AllProductsKey, cancellationToken);
    }
}
