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
app.UseAuthorization();
app.MapControllers();

app.Run();
