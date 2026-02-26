using System.Net.Http.Json;

namespace OrderService.Clients;

public class HttpProductServiceClient : IProductServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpProductServiceClient> _logger;

    public HttpProductServiceClient(HttpClient httpClient, ILogger<HttpProductServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ProductInfo?> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/Products/{id}", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            var dto = await response.Content.ReadFromJsonAsync<ProductDto>(cancellationToken);
            return dto == null ? null : MapToProductInfo(dto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get product {ProductId} via HTTP", id);
            return null;
        }
    }

    public async Task<IEnumerable<ProductInfo>> GetAvailableProductsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/v1/Products", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Array.Empty<ProductInfo>();
            var dtos = await response.Content.ReadFromJsonAsync<ProductDto[]>(cancellationToken);
            return dtos?.Select(MapToProductInfo) ?? Array.Empty<ProductInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get products via HTTP");
            return Array.Empty<ProductInfo>();
        }
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
