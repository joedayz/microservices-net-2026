using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.EntityFrameworkCore;
using ProductService;
using ProductService.Application.Services;
using ProductService.Domain;
using ProductService.Infrastructure;
using ProductService.Infrastructure.Cache;
using ProductService.Application.Configuration;
using Azure.Identity;
using ProductService.Domain.Events;
using ProductService.Infrastructure.Messaging;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using ProductService.Grpc;

var builder = WebApplication.CreateBuilder(args);

if (!string.IsNullOrEmpty(builder.Configuration["AppConfig:Endpoint"]))
{
    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        var endpoint = builder.Configuration["AppConfig:Endpoint"];
        // En local, DefaultAzureCredential puede colgarse intentando Managed Identity (IMDS).
        // Preferir Azure CLI tras `az login`.
        var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeManagedIdentityCredential = true,
            ExcludeVisualStudioCredential = true,
            ExcludeAzurePowerShellCredential = true,
            ExcludeInteractiveBrowserCredential = true
        });

        options.Connect(new Uri(endpoint), credential)
            .Select("ProductService:*")         // Configuración del servicio
            .Select("Cache:*")                  // Configuración de cache
            .Select("FeatureFlags:*")           // Feature flags
            .ConfigureKeyVault(kv =>
            {
                kv.SetCredential(credential);   // Para resolver referencias a Key Vault
            })
            .ConfigureRefresh(refresh =>
            {
                refresh.Register("ProductService:Sentinel", refreshAll: true)
                    .SetRefreshInterval(TimeSpan.FromSeconds(30));
            });
    });
    builder.Services.AddAzureAppConfiguration();
}

// Add services to the container.
builder.Services.AddControllers();

// Configuración centralizada (Options Pattern)
builder.Services.Configure<ProductServiceSettings>(
    builder.Configuration.GetSection(ProductServiceSettings.SectionName));

builder.Services.Configure<CacheSettings>(
    builder.Configuration.GetSection(CacheSettings.SectionName));

builder.Services.Configure<FeatureFlagSettings>(
    builder.Configuration.GetSection(FeatureFlagSettings.SectionName));


// API Versioning (del Módulo 3)
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

// Swagger / OpenAPI (del Módulo 3)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

// Entity Framework Core + PostgreSQL (NUEVO en Módulo 4)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseNpgsql(connectionString));

// DI - Cambiar InMemoryProductRepository por EfProductRepository
builder.Services.AddScoped<IProductRepository, EfProductRepository>();
// builder.Services.AddSingleton<IProductRepository, InMemoryProductRepository>(); // Ya no se usa

// Register application services (Scoped porque depende del DbContext que es Scoped)
builder.Services.AddScoped<IProductService, ProductService.Application.Services.ProductService>();



// Register Redis Cache
var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
    });
    builder.Services.AddScoped<IProductCache, RedisProductCache>();
}
else
{
    // Fallback a cache en memoria si Redis no está disponible
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<IProductCache, InMemoryProductCache>();
}

// Domain Events: RabbitMQ o fallback a logging, según configuración (Módulo 7)
var messagingProvider = builder.Configuration["Messaging:Provider"] ?? "rabbitmq";
if (messagingProvider == "rabbitmq")
{
    builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
}
else
{
    builder.Services.AddSingleton<IEventPublisher, LogEventPublisher>();
}
builder.Services.AddHostedService<ProductEventConsumer>();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5001, o => o.Protocols = HttpProtocols.Http1);       // REST
    options.ListenLocalhost(5002, o => o.Protocols = HttpProtocols.Http2);       // gRPC
});

// gRPC (Módulo 7)
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

var app = builder.Build();

if (!string.IsNullOrEmpty(builder.Configuration["AppConfig:Endpoint"]))
{
    app.UseAzureAppConfiguration();  // Middleware para refresh automático
}


// HTTP pipeline
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
app.MapGrpcService<ProductGrpcService>();
app.MapGrpcReflectionService();

// Ensure database is created and migrate
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();

    // Aplicar migraciones automáticamente
    await dbContext.Database.MigrateAsync();

    // Seed initial data if database is empty
    if (!dbContext.Products.Any())
    {
        var repository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Seeding initial data...");
        await SeedDataAsync(repository);
        logger.LogInformation("Seed data completed successfully");
    }
}

app.Run();

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
