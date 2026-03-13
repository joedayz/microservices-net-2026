using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ============================
// Health Checks — Gateway verifica dependencias downstream
// ============================
builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri("http://localhost:5001/health"),
        name: "product-service",
        tags: new[] { "dependency" })
    .AddUrlGroup(
        new Uri("http://localhost:5003/health"),
        name: "order-service",
        tags: new[] { "dependency" });

// ListenAnyIP para que funcione dentro de contenedores Docker
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5010, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});

var app = builder.Build();

app.MapReverseProxy();

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
    Predicate = _ => false
});

app.Run();
