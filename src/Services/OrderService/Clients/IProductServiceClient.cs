namespace OrderService.Clients;

public interface IProductServiceClient
{
    Task<ProductInfo?> GetProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductInfo>> GetAvailableProductsAsync(CancellationToken cancellationToken = default);
}
