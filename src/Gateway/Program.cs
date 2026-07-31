using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Health Checks — verifica ProductService y OrderService
builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri("http://localhost:5001/health"),
        name: "product-service",
        tags: new[] { "dependency" })
    .AddUrlGroup(
        new Uri("http://localhost:5003/health"),
        name: "order-service",
        tags: new[] { "dependency" });

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5010, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});

var app = builder.Build();

app.MapReverseProxy();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.Run();