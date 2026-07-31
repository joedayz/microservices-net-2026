using Asp.Versioning;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using OrderService.Clients;
using OrderService.Domain;
using OrderService.Infrastructure;
using ProductService.Grpc;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// API Versioning (mismo patrón que ProductService, Módulo 3)
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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el token JWT. Ejemplo: eyJhbGciOi..."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// Entity Framework Core + PostgreSQL (mismo patrón que ProductService, Módulo 4)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();

// Cliente hacia ProductService: HTTP o gRPC según configuración
var communicationMode = builder.Configuration["ProductService:CommunicationMode"] ?? "http";
var httpUrl = builder.Configuration["ProductService:HttpUrl"] ?? "http://localhost:5001";
var grpcUrl = builder.Configuration["ProductService:GrpcUrl"] ?? "http://localhost:5002";

if (communicationMode == "grpc")
{
    builder.Services.AddGrpcClient<ProductGrpc.ProductGrpcClient>(options =>
    {
        options.Address = new Uri(grpcUrl);
    });
    builder.Services.AddScoped<IProductServiceClient, GrpcProductServiceClient>();
}
else
{
    builder.Services.AddHttpClient<IProductServiceClient, HttpProductServiceClient>(client =>
    {
        client.BaseAddress = new Uri(httpUrl);
    });
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5003, o => o.Protocols = HttpProtocols.Http1);
});

// ============================
// JWT Authentication
// ============================
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ReadOnly", policy => policy.RequireRole("Admin", "Reader"));
});

builder.Services.AddHttpClient<IProductServiceClient, HttpProductServiceClient>(client =>
{
    client.BaseAddress = new Uri(httpUrl);
})
.AddPolicyHandler(ResiliencePolicies.GetRetryPolicy())
.AddPolicyHandler(ResiliencePolicies.GetCircuitBreakerPolicy())
.AddPolicyHandler(ResiliencePolicies.GetTimeoutPolicy());

builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri($"{httpUrl}/api/v1/Products"),
        name: "product-service",
        tags: new[] { "dependency" });



var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Aplicar migraciones automáticamente al iniciar (igual que ProductService)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();
