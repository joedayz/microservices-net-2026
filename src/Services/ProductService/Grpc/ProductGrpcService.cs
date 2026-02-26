using Grpc.Core;
using ProductService.Application.Services;

namespace ProductService.Grpc;

public class ProductGrpcService : ProductGrpc.ProductGrpcBase
{
    private readonly IProductService _productService;

    public ProductGrpcService(IProductService productService)
    {
        _productService = productService;
    }

    public override async Task<ProductReply> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid product ID"));

        var product = await _productService.GetByIdAsync(id, context.CancellationToken);
        if (product == null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Product {id} not found"));

        return new ProductReply
        {
            Id = product.Id.ToString(),
            Name = product.Name,
            Description = product.Description ?? string.Empty,
            Price = (double)product.Price,
            Stock = product.Stock,
            CreatedAt = product.CreatedAt.ToString("O")
        };
    }

    public override async Task<ProductListReply> GetAllProducts(GetAllProductsRequest request, ServerCallContext context)
    {
        var products = await _productService.GetAllAsync(context.CancellationToken);
        var reply = new ProductListReply();
        foreach (var p in products)
        {
            reply.Products.Add(new ProductReply
            {
                Id = p.Id.ToString(),
                Name = p.Name,
                Description = p.Description ?? string.Empty,
                Price = (double)p.Price,
                Stock = p.Stock,
                CreatedAt = p.CreatedAt.ToString("O")
            });
        }
        return reply;
    }
}
