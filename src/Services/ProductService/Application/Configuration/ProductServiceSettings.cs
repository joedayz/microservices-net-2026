using System.ComponentModel.DataAnnotations;

namespace ProductService.Application.Configuration;

public class ProductServiceSettings
{
    public const string SectionName = "ProductService";

    [Required]
    public string ServiceName { get; set; } = "ProductService";

    public string ServiceVersion { get; set; } = "1.0.0";

    public int MaxPageSize { get; set; } = 50;

    public int DefaultPageSize { get; set; } = 10;
}
