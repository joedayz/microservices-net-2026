using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using ProductService;
using ProductService.Application.Configuration;
using ProductService.Application.Services;
using ProductService.Domain;
using ProductService.Domain.Events;
using ProductService.Grpc;
using ProductService.Infrastructure;
using ProductService.Infrastructure.Cache;
using ProductService.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configuración centralizada (Options Pattern)
builder.Services.Configure<ProductServiceSettings>(
    builder.Configuration.GetSection(ProductServiceSettings.SectionName));

builder.Services.Configure<CacheSettings>(
    builder.Configuration.GetSection(CacheSettings.SectionName));

builder.Services.Configure<FeatureFlagSettings>(
    builder.Configuration.GetSection(FeatureFlagSettings.SectionName));


// Register Entity Framework
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ProductDbContext>(options =>
    options.UseNpgsql(connectionString));


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

// RabbitMQ options (ConnectionString from ConnectionStrings:RabbitMq)
builder.Services.AddOptions<RabbitMqOptions>()
    .Configure<IConfiguration>((opts, config) =>
    {
        config.GetSection(RabbitMqOptions.SectionName).Bind(opts);
        opts.ConnectionString = config.GetConnectionString("RabbitMq") ?? opts.ConnectionString;
    });

var messagingProvider = builder.Configuration.GetValue<string>("Messaging:Provider") ?? "log";
if (messagingProvider.Equals("rabbitmq", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
    builder.Services.AddHostedService<ProductEventConsumer>();
}
else
{
    builder.Services.AddSingleton<IEventPublisher, LogEventPublisher>();
}

// gRPC
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();

// Kestrel: REST en 5001, gRPC en 5002
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5001, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
    options.ListenLocalhost(5002, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2);
});

// DI
builder.Services.AddScoped<IProductRepository, EfProductRepository>();
builder.Services.AddScoped<IProductService, ProductService.Application.Services.ProductService>();

// Agregar Azure App Configuration (ANTES de builder.Build())
// Usar AppConfig:Enabled=false en Development para evitar bloqueos si Azure es lento
var appConfigEnabled = builder.Configuration.GetValue<bool>("AppConfig:Enabled", true);
if (appConfigEnabled && !string.IsNullOrEmpty(builder.Configuration["AppConfig:Endpoint"]))
{
    builder.Services.AddAzureAppConfiguration();
    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        var endpoint = builder.Configuration["AppConfig:Endpoint"]!;
        var credential = new DefaultAzureCredential();

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
    }, optional: true);  // Si Azure falla (403, timeout), la app arranca con config local
}

var app = builder.Build();

// Middleware para refresh automático de App Configuration
if (appConfigEnabled && !string.IsNullOrEmpty(builder.Configuration["AppConfig:Endpoint"]))
{
    app.UseAzureAppConfiguration();
}

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
app.MapGrpcService<ProductGrpcService>();
app.MapGrpcReflectionService();

// =========================
// Seed inicial
// =========================

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();

    // Aplicar migraciones automáticamente
    await dbContext.Database.MigrateAsync();

    // Seed initial data if database is empty
    if (!dbContext.Products.Any())
    {
        var repository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
        await SeedDataAsync(repository);
    }
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
