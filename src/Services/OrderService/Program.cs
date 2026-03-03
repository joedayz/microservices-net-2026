using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OrderService.Clients;
using OrderService.Domain;
using OrderService.Infrastructure;

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
    });
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

app.Run();
