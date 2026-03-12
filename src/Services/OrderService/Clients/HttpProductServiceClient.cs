using System.Collections.Concurrent;
using System.Net.Http.Json;

namespace OrderService.Clients;

public class HttpProductServiceClient : IProductServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpProductServiceClient> _logger;

    // Fallback cache: última respuesta exitosa por endpoint
    private static readonly ConcurrentDictionary<string, ProductInfo> _productCache = new();
    private static readonly ConcurrentDictionary<string, IEnumerable<ProductInfo>> _productsListCache = new();

    public HttpProductServiceClient(HttpClient httpClient, ILogger<HttpProductServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ProductInfo?> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = id.ToString();
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/Products/{id}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ProductService returned {StatusCode} for product {ProductId}", response.StatusCode, id);
                return GetFallbackProduct(cacheKey);
            }
            var dto = await response.Content.ReadFromJsonAsync<ProductDto>(cancellationToken);
            if (dto == null) return null;

            var product = MapToProductInfo(dto);
            _productCache[cacheKey] = product; // Actualizar cache
            return product;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get product {ProductId} via HTTP — using fallback", id);
            return GetFallbackProduct(cacheKey);
        }
    }

    public async Task<IEnumerable<ProductInfo>> GetAvailableProductsAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "all-products";
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/Products", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("ProductService returned {StatusCode} for products list", response.StatusCode);
                return GetFallbackProductsList(cacheKey);
            }
            var dtos = await response.Content.ReadFromJsonAsync<ProductDto[]>(cancellationToken);
            var products = dtos?.Select(MapToProductInfo).ToList() ?? new List<ProductInfo>();

            _productsListCache[cacheKey] = products; // Actualizar cache
            return products;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get products via HTTP — using fallback");
            return GetFallbackProductsList(cacheKey);
        }
    }

    private ProductInfo? GetFallbackProduct(string cacheKey)
    {
        if (_productCache.TryGetValue(cacheKey, out var cached))
        {
            _logger.LogInformation("[Fallback] Returning cached product {CacheKey}", cacheKey);
            return cached;
        }
        _logger.LogWarning("[Fallback] No cached data for product {CacheKey}", cacheKey);
        return null;
    }

    private IEnumerable<ProductInfo> GetFallbackProductsList(string cacheKey)
    {
        if (_productsListCache.TryGetValue(cacheKey, out var cached))
        {
            _logger.LogInformation("[Fallback] Returning {Count} cached products", cached.Count());
            return cached;
        }
        _logger.LogWarning("[Fallback] No cached products list available");
        return Array.Empty<ProductInfo>();
    }

    private static ProductInfo MapToProductInfo(ProductDto dto) =>
        new(dto.Id, dto.Name, dto.Description ?? string.Empty, dto.Price, dto.Stock, dto.CreatedAt);

    private sealed class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
