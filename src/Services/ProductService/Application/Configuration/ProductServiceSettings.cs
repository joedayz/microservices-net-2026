using System.ComponentModel.DataAnnotations;

namespace ProductService.Application.Configuration;

/// <summary>
/// Configuración general del servicio.
/// Se mapea desde la sección "ProductService" de appsettings.json.
/// </summary>
public class ProductServiceSettings
{
    public const string SectionName = "ProductService";

    [Required]
    public string ServiceName { get; set; } = "ProductService";

    public string ServiceVersion { get; set; } = "1.0.0";

    public int MaxPageSize { get; set; } = 50;

    public int DefaultPageSize { get; set; } = 10;
}
