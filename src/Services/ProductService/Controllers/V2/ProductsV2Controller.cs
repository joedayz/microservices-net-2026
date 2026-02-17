using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProductService.Application.Configuration;
using ProductService.Application.DTOs;
using ProductService.Application.Services;

namespace ProductService.Controllers.V2;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IOptionsSnapshot<FeatureFlagSettings> _featureFlags;
    private readonly IOptionsSnapshot<ProductServiceSettings> _serviceSettings;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IProductService productService,
        IOptionsSnapshot<FeatureFlagSettings> featureFlags,
        IOptionsSnapshot<ProductServiceSettings> serviceSettings,
        ILogger<ProductsController> logger)
    {
        _productService = productService;
        _featureFlags = featureFlags;
        _serviceSettings = serviceSettings;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        // Usar configuración tipada para el tamaño de página
        var effectivePageSize = Math.Min(
            pageSize ?? _serviceSettings.Value.DefaultPageSize,
            _serviceSettings.Value.MaxPageSize);


        var allProducts = await _productService.GetAllAsync(cancellationToken);
        var productsList = allProducts.ToList();

        var totalCount = productsList.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var pagedProducts = productsList
            .Skip((page - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToList();

        var result = new PagedResult<ProductDto>
        {
            Items = pagedProducts,
            Page = page,
            PageSize = effectivePageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        return Ok(result);
    }



    /// <summary>
    /// Buscar productos por nombre (controlado por Feature Flag).
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SearchByName(
        [FromQuery] string name,
        CancellationToken cancellationToken = default)
    {
        // Verificar feature flag
        if (!_featureFlags.Value.EnableSearchByName)
        {
            _logger.LogWarning("SearchByName feature is disabled");
            return NotFound("This feature is currently disabled");
        }

        _logger.LogInformation("Searching products by name: {Name}", name);

        var allProducts = await _productService.GetAllAsync(cancellationToken);
        var filtered = allProducts
            .Where(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Ok(filtered);
    }



    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productService.GetByIdAsync(id, cancellationToken);

        if (product == null)
        {
            return NotFound($"Product with ID {id} not found");
        }

        return Ok(product);
    }
}
