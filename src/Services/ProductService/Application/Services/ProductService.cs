using ProductService.Application.DTOs;
using ProductService.Domain;
using ProductService.Infrastructure.Cache;

namespace ProductService.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly IProductCache _cache;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IProductRepository repository,
        IProductCache cache,
        ILogger<ProductService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting product with ID: {ProductId}", id);

        // Intentar obtener del cache
        var cached = await _cache.GetAsync(id, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        // Si no está en cache, obtener de BD
        var product = await _repository.GetByIdAsync(id, cancellationToken);
        if (product == null)
        {
            return null;
        }

        // Guardar en cache
        var dto = MapToDto(product);

        // Guardar en cache
        await _cache.SetAsync(id, dto, cancellationToken);

        return dto;
    }

    public async Task<IEnumerable<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting all products");


        // Intentar obtener del cache
        var cached = await _cache.GetAllAsync(cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        // Si no está en cache, obtener de BD
        var products = await _repository.GetAllAsync(cancellationToken);
        var dtos = products.Select(MapToDto).ToList();

        // Guardar en cache
        await _cache.SetAllAsync(dtos, cancellationToken);

        return dtos;
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating product: {ProductName}", dto.Name);

        var product = new Product(dto.Name, dto.Description, dto.Price, dto.Stock);
        var createdProduct = await _repository.CreateAsync(product, cancellationToken);

        var result = MapToDto(createdProduct);

        // Guardar en cache e invalidar lista
        await _cache.SetAsync(createdProduct.Id, result, cancellationToken);

        return result;
    }

    public async Task<bool> UpdateAsync(Guid id, CreateProductDto dto, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating product with ID: {ProductId}", id);
        var product = await _repository.GetByIdAsync(id, cancellationToken);
        if (product == null)
        {
            return false;
        }

        product.Name = dto.Name;
        product.Description = dto.Description;
        product.Price = dto.Price;
        product.Stock = dto.Stock;
        product.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(product, cancellationToken);

        if (updated)
        {
            // Invalidar cache
            await _cache.RemoveAsync(id, cancellationToken);
        }

        return updated;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting product with ID: {ProductId}", id);
        var deleted = await _repository.DeleteAsync(id, cancellationToken);

        if (deleted)
        {
            // Invalidar cache
            await _cache.RemoveAsync(id, cancellationToken);
        }

        return deleted;
    }

    private static ProductDto MapToDto(Product product)
    {
        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            Stock = product.Stock,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }
}
