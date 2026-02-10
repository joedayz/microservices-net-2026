using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.OpenApi;
using ProductService;
using ProductService.Application.Services;
using ProductService.Domain;
using ProductService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// API Versioning
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = ApiVersionReader.Combine(
            new UrlSegmentApiVersionReader(),
            new HeaderApiVersionReader("X-Version"),
            new QueryStringApiVersionReader("version")
        );
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

// Swagger / OpenAPI (versioned)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

// DI
builder.Services.AddSingleton<IProductRepository, InMemoryProductRepository>();
builder.Services.AddSingleton<IProductService, ProductService.Application.Services.ProductService>();

var app = builder.Build();

// =========================
// HTTP pipeline
// =========================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                description.GroupName.ToUpperInvariant()
            );
        }

        options.RoutePrefix = string.Empty; // Swagger en /
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// =========================
// Seed inicial
// =========================

using (var scope = app.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Seeding initial data...");
    await SeedDataAsync(repository);
    logger.LogInformation("Seed data completed successfully");
}

app.Run();

// =========================
// Seed method
// =========================

static async Task SeedDataAsync(IProductRepository repository)
{
    var products = new[]
    {
        new Product("Laptop", "High-performance laptop", 1299.99m, 10),
        new Product("Mouse", "Wireless mouse", 29.99m, 50),
        new Product("Keyboard", "Mechanical keyboard", 89.99m, 30)
    };

    foreach (var product in products)
    {
        await repository.CreateAsync(product);
        Console.WriteLine($"Seeded product: {product.Name} (ID: {product.Id})");
    }
}
