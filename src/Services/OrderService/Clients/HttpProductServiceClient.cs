using OrderService.Application.DTOs;

namespace OrderService.Clients;

public class HttpProductServiceClient : IProductServiceClient
{
    private readonly HttpClient _httpClient;

    public HttpProductServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<ProductInfoDto>> GetAvailableProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = await _httpClient.GetFromJsonAsync<List<ProductInfoDto>>(
            "/api/v1/Products", cancellationToken);
        return products ?? [];
    }

    public async Task<ProductInfoDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<ProductInfoDto>(
            $"/api/v1/Products/{id}", cancellationToken);
    }
}