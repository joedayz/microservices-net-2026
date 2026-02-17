namespace ProductService.Application.Configuration;

/// <summary>
/// Feature flags del servicio.
/// Se mapea desde la sección "FeatureFlags" de appsettings.json.
/// </summary>
public class FeatureFlagSettings
{
    public const string SectionName = "FeatureFlags";

    /// <summary>Habilitar endpoint de búsqueda por nombre</summary>
    public bool EnableSearchByName { get; set; } = false;

    /// <summary>Habilitar respuestas con metadata extendida</summary>
    public bool EnableExtendedMetadata { get; set; } = false;

    /// <summary>Habilitar logging detallado de cache hits/misses</summary>
    public bool EnableCacheLogging { get; set; } = true;

}
