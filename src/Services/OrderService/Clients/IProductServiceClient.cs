using OrderService.Application.DTOs;

namespace OrderService.Clients;

public interface IProductServiceClient
{
    Task<IEnumerable<ProductInfoDto>> GetAvailableProductsAsync(CancellationToken cancellationToken = default);
    Task<ProductInfoDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);
}