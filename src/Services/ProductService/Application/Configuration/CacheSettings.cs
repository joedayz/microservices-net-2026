namespace ProductService.Application.Configuration;

public class CacheSettings
{
    public const string SectionName = "Cache";

    /// <summary>Habilitar o deshabilitar cache</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Tiempo de expiración absoluta en minutos</summary>
    public int AbsoluteExpirationMinutes { get; set; } = 5;

    /// <summary>Tiempo de expiración deslizante en minutos</summary>
    public int SlidingExpirationMinutes { get; set; } = 1;

    /// <summary>Prefijo para las keys de Redis</summary>
    public string KeyPrefix { get; set; } = "products";

}
