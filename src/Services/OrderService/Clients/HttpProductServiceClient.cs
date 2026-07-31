using System.Collections.Concurrent;
using System.Net;
using OrderService.Application.DTOs;

namespace OrderService.Clients;

public class HttpProductServiceClient : IProductServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpProductServiceClient> _logger;

    // Cache estático para fallback (última respuesta exitosa por producto)
    private static readonly ConcurrentDictionary<string, ProductInfoDto> _productCache = new();

    public HttpProductServiceClient(
        HttpClient httpClient,
        ILogger<HttpProductServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<ProductInfoDto>> GetAvailableProductsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var products = await _httpClient.GetFromJsonAsync<List<ProductInfoDto>>(
                "/api/v1/Products", cancellationToken);

            if (products is not null)
            {
                foreach (var product in products)
                {
                    _productCache[$"product:{product.Id}"] = product;
                }
            }

            return products ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetAvailableProducts failed — returning cached products if any");
            return _productCache.Values.ToList();
        }
    }

    public async Task<ProductInfoDto?> GetProductByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"product:{id}";

        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/Products/{id}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            var product = await response.Content.ReadFromJsonAsync<ProductInfoDto>(cancellationToken);
            if (product is not null)
            {
                _productCache[cacheKey] = product;
            }

            return product;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetProductById {ProductId} failed — trying fallback cache", id);
            return GetFallbackProduct(cacheKey);
        }
    }

    private ProductInfoDto? GetFallbackProduct(string cacheKey)
    {
        if (_productCache.TryGetValue(cacheKey, out var cached))
        {
            _logger.LogInformation("Fallback cache HIT for {CacheKey}", cacheKey);
            return cached;
        }

        _logger.LogWarning("Fallback cache MISS for {CacheKey}", cacheKey);
        return null;
    }
}
