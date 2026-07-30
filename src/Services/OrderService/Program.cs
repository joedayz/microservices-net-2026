using Asp.Versioning;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using OrderService.Clients;
using OrderService.Domain;
using OrderService.Infrastructure;
using ProductService.Grpc;

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
builder.Services.AddSwaggerGen();

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

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// Aplicar migraciones automáticamente al iniciar (igual que ProductService)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();