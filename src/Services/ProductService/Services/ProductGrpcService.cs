using Grpc.Core;
using ProductService.Domain;

namespace ProductService.Grpc;

public class ProductGrpcService : ProductGrpc.ProductGrpcBase
{
    private readonly IProductRepository _repository;

    public ProductGrpcService(IProductRepository repository)
    {
        _repository = repository;
    }

    public override async Task<ProductReply> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid product id"));

        var product = await _repository.GetByIdAsync(id, context.CancellationToken);
        if (product == null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Product {request.Id} not found"));

        return ToReply(product);
    }

    public override async Task<ProductListReply> GetAllProducts(GetAllProductsRequest request, ServerCallContext context)
    {
        var products = await _repository.GetAllAsync(context.CancellationToken);
        var reply = new ProductListReply();
        reply.Products.AddRange(products.Select(ToReply));
        return reply;
    }

    private static ProductReply ToReply(Product product) => new()
    {
        Id = product.Id.ToString(),
        Name = product.Name,
        Description = product.Description,
        Price = (double)product.Price,
        Stock = product.Stock,
        CreatedAt = product.CreatedAt.ToString("O")
    };
}