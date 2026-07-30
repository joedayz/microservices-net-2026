using OrderService.Application.DTOs;
using ProductService.Grpc;

namespace OrderService.Clients;

public class GrpcProductServiceClient : IProductServiceClient
{
    private readonly ProductGrpc.ProductGrpcClient _client;

    public GrpcProductServiceClient(ProductGrpc.ProductGrpcClient client)
    {
        _client = client;
    }

    public async Task<IEnumerable<ProductInfoDto>> GetAvailableProductsAsync(CancellationToken cancellationToken = default)
    {
        var reply = await _client.GetAllProductsAsync(new GetAllProductsRequest(), cancellationToken: cancellationToken);
        return reply.Products.Select(p => new ProductInfoDto
        {
            Id = Guid.Parse(p.Id),
            Name = p.Name,
            Price = (decimal)p.Price,
            Stock = p.Stock
        });
    }

    public async Task<ProductInfoDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var reply = await _client.GetProductAsync(new GetProductRequest { Id = id.ToString() }, cancellationToken: cancellationToken);
        return new ProductInfoDto
        {
            Id = Guid.Parse(reply.Id),
            Name = reply.Name,
            Price = (decimal)reply.Price,
            Stock = reply.Stock
        };
    }
}