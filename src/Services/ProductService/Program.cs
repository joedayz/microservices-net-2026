using System.Text;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
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

// ============================
// JWT Authentication
// ============================
var authProvider = builder.Configuration.GetValue<string>("Auth:Provider") ?? "local";

if (authProvider.Equals("azuread", StringComparison.OrdinalIgnoreCase))
{
    // ── Azure AD / Microsoft Entra ID ──
    var azureAdConfig = builder.Configuration.GetSection("AzureAd");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // Usar Authority v1 para que la metadata acepte tokens v1 de az cli
            options.Authority = $"{azureAdConfig["Instance"]}{azureAdConfig["TenantId"]}";
            options.Audience = azureAdConfig["Audience"];
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuers = new[]
                {
                    $"{azureAdConfig["Instance"]}{azureAdConfig["TenantId"]}/v2.0",
                    $"https://sts.windows.net/{azureAdConfig["TenantId"]}/"
                },
                ValidAudiences = new[]
                {
                    azureAdConfig["Audience"],
                    azureAdConfig["ClientId"]
                },
                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };
            // Diagnóstico: ver por qué falla la autenticación
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtBearer");
                    logger.LogError(context.Exception, "JWT Authentication failed: {Message}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtBearer");
                    var claims = context.Principal?.Claims.Select(c => $"{c.Type}={c.Value}");
                    logger.LogInformation("JWT Token validated. Claims: {Claims}", string.Join(", ", claims ?? Array.Empty<string>()));
                    return Task.CompletedTask;
                },
                OnChallenge = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("JwtBearer");
                    logger.LogWarning("JWT Challenge: {Error} - {ErrorDescription}", context.Error, context.ErrorDescription);
                    return Task.CompletedTask;
                }
            };
        });
}
else
{
    // ── JWT Local (desarrollo/laboratorio) ──
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var secretKey = jwtSettings["Key"] ?? "S3cur3K3y_F0r_D3v3l0pm3nt_Purp0s3s_Only_2025!";

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"] ?? "microservices-net-2025",
            ValidAudience = jwtSettings["Audience"] ?? "microservices-api",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ReadOnly", policy => policy.RequireRole("Admin", "Reader"));
});

// Swagger / OpenAPI (versioned)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Configurar JWT en Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el token JWT. Ejemplo: eyJhbGciOi..."
    });

    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer"),
            new List<string>()
        }
    });
});
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
app.UseAuthentication();
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
