using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProductService.Application.Configuration;

namespace ProductService.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ConfigController: ControllerBase
{
    private readonly IOptionsSnapshot<ProductServiceSettings> _serviceSettings;
    private readonly IOptionsSnapshot<CacheSettings> _cacheSettings;
    private readonly IOptionsSnapshot<FeatureFlagSettings> _featureFlags;
    private readonly IWebHostEnvironment _environment;

    public ConfigController(
        IOptionsSnapshot<ProductServiceSettings> serviceSettings,
        IOptionsSnapshot<CacheSettings> cacheSettings,
        IOptionsSnapshot<FeatureFlagSettings> featureFlags,
        IWebHostEnvironment environment)
    {
        _serviceSettings = serviceSettings;
        _cacheSettings = cacheSettings;
        _featureFlags = featureFlags;
        _environment = environment;
    }

    /// <summary>
    /// Obtener configuración activa (solo Development).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetConfig()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        return Ok(new
        {
            Environment = _environment.EnvironmentName,
            Service = _serviceSettings.Value,
            Cache = _cacheSettings.Value,
            FeatureFlags = _featureFlags.Value
        });
    }

    /// <summary>
    /// Obtener feature flags activos.
    /// </summary>
    [HttpGet("features")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetFeatureFlags()
    {
        return Ok(_featureFlags.Value);
    }
}
