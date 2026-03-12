using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using OrderService.Clients;
using OrderService.Domain;
using OrderService.Infrastructure;
using Polly;
using Polly.CircuitBreaker;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var productConfig = builder.Configuration.GetSection("ProductService");
var communicationMode = productConfig.GetValue<string>("CommunicationMode") ?? "http";
var httpUrl = productConfig.GetValue<string>("HttpUrl") ?? "http://localhost:5001";
var grpcUrl = productConfig.GetValue<string>("GrpcUrl") ?? "http://localhost:5002";

if (communicationMode.Equals("grpc", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IProductServiceClient>(sp =>
        new GrpcProductServiceClient(grpcUrl, sp.GetRequiredService<ILogger<GrpcProductServiceClient>>()));
}
else
{
    builder.Services.AddHttpClient<IProductServiceClient, HttpProductServiceClient>(client =>
    {
        client.BaseAddress = new Uri(httpUrl);
    })
    .AddPolicyHandler(ResiliencePolicies.GetRetryPolicy())
    .AddPolicyHandler(ResiliencePolicies.GetCircuitBreakerPolicy())
    .AddPolicyHandler(ResiliencePolicies.GetTimeoutPolicy());
}

builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

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

builder.Services.AddOpenApi();

// ============================
// Health Checks
// ============================
builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri($"{httpUrl}/api/v1/Products"),
        name: "product-service",
        tags: new[] { "dependency" });

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5003, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ============================
// Health Check Endpoints
// ============================
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds + "ms"
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds + "ms"
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // Liveness: solo verifica que el proceso responde
});

app.Run();
