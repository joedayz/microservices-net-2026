using Grpc.Net.Client;
using ProductService.Grpc;

namespace OrderService.Clients;

public class GrpcProductServiceClient : IProductServiceClient
{
    private readonly string _grpcUrl;
    private readonly ILogger<GrpcProductServiceClient> _logger;

    public GrpcProductServiceClient(string grpcUrl, ILogger<GrpcProductServiceClient> logger)
    {
        _grpcUrl = grpcUrl;
        _logger = logger;
    }

    public async Task<ProductInfo?> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var channel = GrpcChannel.ForAddress(_grpcUrl);
            var client = new ProductGrpc.ProductGrpcClient(channel);
            var reply = await client.GetProductAsync(new GetProductRequest { Id = id.ToString() }, cancellationToken: cancellationToken);
            return new ProductInfo(
                Guid.Parse(reply.Id),
                reply.Name,
                reply.Description,
                (decimal)reply.Price,
                reply.Stock,
                DateTime.Parse(reply.CreatedAt));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get product {ProductId} via gRPC", id);
            return null;
        }
    }

    public async Task<IEnumerable<ProductInfo>> GetAvailableProductsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var channel = GrpcChannel.ForAddress(_grpcUrl);
            var client = new ProductGrpc.ProductGrpcClient(channel);
            var reply = await client.GetAllProductsAsync(new GetAllProductsRequest(), cancellationToken: cancellationToken);
            return reply.Products.Select(p => new ProductInfo(
                Guid.Parse(p.Id),
                p.Name,
                p.Description,
                (decimal)p.Price,
                p.Stock,
                DateTime.Parse(p.CreatedAt)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get products via gRPC");
            return Array.Empty<ProductInfo>();
        }
    }
}
