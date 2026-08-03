using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Health Checks (Módulo 11) — URLs configurables (localhost en local, DNS de K8s en AKS)
var productHealthUrl = builder.Configuration["HealthChecks:ProductServiceUrl"]
    ?? "http://localhost:5001/health";
var orderHealthUrl = builder.Configuration["HealthChecks:OrderServiceUrl"]
    ?? "http://localhost:5003/health";

builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri(productHealthUrl),
        name: "product-service",
        tags: new[] { "dependency" })
    .AddUrlGroup(
        new Uri(orderHealthUrl),
        name: "order-service",
        tags: new[] { "dependency" });

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5010, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});

var app = builder.Build();

app.MapReverseProxy();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthJson
});
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthJson
});

app.Run();

static Task WriteHealthJson(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";
    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        entries = report.Entries.ToDictionary(
            e => e.Key,
            e => new
            {
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
                exception = e.Value.Exception?.Message
            })
    };
    return context.Response.WriteAsJsonAsync(payload);
}
