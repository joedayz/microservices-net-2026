using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

// Worker 2.x: FunctionsApplication (no HostBuilder + ConfigureFunctionsWorkerDefaults)
var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Build().Run();