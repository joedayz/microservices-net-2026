# Módulo 11 – Alta disponibilidad y tolerancia a fallos

## 🧠 Teoría

### Circuit Breaker (Polly)

El Circuit Breaker previene cascadas de fallos cuando un servicio downstream no está disponible:

- **Closed** (cerrado): Funcionamiento normal, las peticiones pasan al servicio destino. Si se acumulan fallos, se abre el circuito.
- **Open** (abierto): El circuito está abierto. Las peticiones fallan inmediatamente sin intentar llegar al servicio. Esto protege al servicio de sobrecarga.
- **Half-Open** (semi-abierto): Después de un período de espera, se permite una petición de prueba. Si tiene éxito, el circuito se cierra. Si falla, vuelve a abrirse.

```
  ┌──────────┐    N fallos    ┌──────────┐
  │  Closed  │ ──────────────►│   Open   │
  │ (normal) │                │ (falla   │
  └──────────┘                │  rápido) │
       ▲                      └────┬─────┘
       │                           │ espera N seg
       │                      ┌────▼─────┐
       │    éxito             │ Half-Open│
       └──────────────────────│ (prueba) │
                              └──────────┘
```

### Retry con Exponential Backoff + Jitter

Reintentos inteligentes que evitan la "thundering herd":

- **Exponential Backoff**: Cada reintento espera más tiempo (2^attempt segundos).
- **Jitter**: Se agrega variación aleatoria (0-1000ms) para que múltiples clientes no reintenten al mismo tiempo.
- **Max Attempts**: Límite de 3 reintentos antes de fallar.

```
Intento 1: falla → espera ~2s + jitter
Intento 2: falla → espera ~4s + jitter
Intento 3: falla → espera ~8s + jitter
Intento 4: falla definitiva
```

### Timeout

Límite de tiempo por petición HTTP individual (10 segundos). Evita que una petición lenta bloquee recursos indefinidamente.

### Fallback

Respuesta alternativa cuando falla la llamada al servicio:
- **Cache local**: Se almacena la última respuesta exitosa y se retorna cuando el servicio no responde.
- **Degradación elegante**: El sistema sigue funcionando con datos potencialmente desactualizados en vez de fallar por completo.

### Health Checks

Endpoints de monitoreo que permiten saber si un servicio está saludable:
- `/health` — Estado completo con todas las dependencias (PostgreSQL, Redis, servicios downstream).
- `/health/live` — Liveness probe: solo verifica que el proceso responde (para Kubernetes).

---

## 🧪 Laboratorio 11

### Objetivo
Implementar patrones de resiliencia completos en la arquitectura de microservicios:
1. Retry policy con exponential backoff + jitter
2. Circuit breaker
3. Timeout policy
4. Fallback con cache local
5. Health checks en los 3 servicios

### Resumen de cambios realizados

| Archivo | Cambio |
|---------|--------|
| `OrderService.csproj` | Agregados paquetes `Microsoft.Extensions.Http.Polly`, `AspNetCore.HealthChecks.Uris` |
| `OrderService/Infrastructure/ResiliencePolicies.cs` | **NUEVO** — Clase estática con las 3 políticas de Polly |
| `OrderService/Program.cs` | Wired up Polly policies en `AddHttpClient` + Health Checks |
| `OrderService/Clients/HttpProductServiceClient.cs` | Fallback cache con `ConcurrentDictionary` |
| `OrderService/appsettings.json` | Sección `Resilience` con configuración |
| `ProductService/Program.cs` | Health Checks con PostgreSQL + Redis |
| `ProductService.csproj` | Agregados paquetes `AspNetCore.HealthChecks.NpgSql`, `AspNetCore.HealthChecks.Redis` |
| `Gateway/Program.cs` | Health Checks que verifican servicios downstream |
| `Gateway/Gateway.csproj` | Agregados paquetes `HealthChecks.Uris` |

---

### Paso 1 — Paquetes NuGet

**OrderService:**
```bash
cd src/Services/OrderService
dotnet add package Microsoft.Extensions.Http.Polly
dotnet add package AspNetCore.HealthChecks.Uris --version 9.0.0
```

**ProductService:**
```bash
cd src/Services/ProductService
dotnet add package AspNetCore.HealthChecks.NpgSql --version 9.0.0
dotnet add package AspNetCore.HealthChecks.Redis --version 9.0.0
```

**Gateway:**
```bash
cd src/Gateway
dotnet add package AspNetCore.HealthChecks.Uris --version 9.0.0
```

---

### Paso 2 — Políticas de resiliencia (ResiliencePolicies.cs)

**Archivo: `src/Services/OrderService/Infrastructure/ResiliencePolicies.cs`** (completo):

```csharp
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace OrderService.Infrastructure;

/// <summary>
/// Políticas de resiliencia Polly para el HttpClient hacia ProductService.
/// Orden al encadenar en Program.cs: Retry → Circuit Breaker → Timeout.
/// </summary>
public static class ResiliencePolicies
{
    /// <summary>
    /// Retry: hasta 3 intentos con backoff exponencial + jitter.
    /// Cubre errores HTTP transitorios (5xx, 408) y timeouts de Polly.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger? logger = null)
    {
        var jitter = new Random();

        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, attempt))
                    + TimeSpan.FromMilliseconds(jitter.Next(0, 1000)),
                onRetry: (outcome, timespan, attempt, context) =>
                {
                    logger?.LogWarning(
                        "Retry {Attempt} after {Delay}s due to: {Reason}",
                        attempt,
                        timespan.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });
    }

    /// <summary>
    /// Circuit Breaker: abre tras 3 fallos consecutivos y espera 30s antes de half-open.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ILogger? logger = null)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, breakDelay) =>
                {
                    logger?.LogError(
                        "Circuit OPEN for {BreakDelay}s. Reason: {Reason}",
                        breakDelay.TotalSeconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                },
                onReset: () =>
                {
                    logger?.LogInformation("Circuit RESET — tráfico normal reanudado");
                },
                onHalfOpen: () =>
                {
                    logger?.LogInformation("Circuit HALF-OPEN — permitiendo una prueba");
                });
    }

    /// <summary>
    /// Timeout: máximo 10 segundos por request (optimistic = respeta CancellationToken).
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(
            seconds: 10,
            timeoutStrategy: TimeoutStrategy.Optimistic);
    }
}
```

**¿Por qué `HandleTransientHttpError()`?** Cubre automáticamente HTTP 5xx y HTTP 408 (Request Timeout), que son errores transitorios que pueden resolverse con reintentos.

**¿Por qué `TimeoutStrategy.Optimistic`?** Funciona con `CancellationToken`, cancelando la petición limpiamente sin consumir threads adicionales.

---

### Paso 3 — Wiring en Program.cs (OrderService)

Se encadenaron las políticas al registro de `HttpClient`:

```csharp
builder.Services.AddHttpClient<IProductServiceClient, HttpProductServiceClient>(client =>
{
    client.BaseAddress = new Uri(httpUrl);
})
.AddPolicyHandler(ResiliencePolicies.GetRetryPolicy())
.AddPolicyHandler(ResiliencePolicies.GetCircuitBreakerPolicy())
.AddPolicyHandler(ResiliencePolicies.GetTimeoutPolicy());
```

**Orden de ejecución (de afuera hacia adentro):**
1. **Retry** — Si falla, reintenta hasta 3 veces
2. **Circuit Breaker** — Si hay 3 fallos consecutivos, abre el circuito
3. **Timeout** — Cada petición individual tiene máximo 10 segundos

---

### Paso 4 — Fallback con cache (HttpProductServiceClient)

Va en el cliente HTTP existente:

**Archivo: `src/Services/OrderService/Clients/HttpProductServiceClient.cs`**

No va en `Program.cs` ni en `ResiliencePolicies.cs`. Polly reintenta/corta el circuito; este fallback es la **última red de seguridad** en el cliente: si tras Polly sigue fallando, se sirve la última respuesta exitosa en memoria.

Resumen de cambios en esa clase:

```csharp
// Cache estático para fallback (en HttpProductServiceClient)
private static readonly ConcurrentDictionary<string, ProductInfoDto> _productCache = new();

public async Task<ProductInfoDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
{
    var cacheKey = $"product:{id}";
    try
    {
        // llamar a ProductService...
        // si OK → _productCache[cacheKey] = product;
        return product;
    }
    catch (Exception ex)
    {
        // Fallo (tras retries/circuit breaker) → retornar desde cache
        return GetFallbackProduct(cacheKey);
    }
}
```

**¿Por qué `ConcurrentDictionary`?** Es thread-safe y estático, sobrevive entre requests. En producción se usaría Redis o `IDistributedCache`, pero para este laboratorio es suficiente.

---

### Paso 5 — Health Checks

Todo esto va en el **`Program.cs`** de cada proyecto (no en un controller). Hay **dos partes**:

1. **Registro** → `builder.Services.AddHealthChecks()...` (antes de `var app = builder.Build()`)
2. **Endpoints** → `app.MapHealthChecks(...)` (después de `builder.Build()`, junto a `MapControllers`)

#### ProductService — `src/Services/ProductService/Program.cs`

**Paquetes** (si aún no están):
```bash
cd src/Services/ProductService
dotnet add package AspNetCore.HealthChecks.NpgSql
dotnet add package AspNetCore.HealthChecks.Redis
```

**1) Registro** (después de configurar Redis / connection strings, antes de `var app = builder.Build()`):

```csharp
// Health Checks (Módulo 11)
var healthChecksBuilder = builder.Services.AddHealthChecks();

if (!string.IsNullOrEmpty(connectionString))
{
    healthChecksBuilder.AddNpgSql(
        connectionString,
        name: "postgresql",
        tags: new[] { "db", "dependency" });
}

if (!string.IsNullOrEmpty(redisConnection))
{
    healthChecksBuilder.AddRedis(
        redisConnection,
        name: "redis",
        tags: new[] { "cache", "dependency" });
}
```

**2) Endpoints** (después de `app.MapControllers()`):

> Por defecto `MapHealthChecks` responde **texto plano** (`Healthy`). Para poder usar `| jq`, configura un `ResponseWriter` JSON:

```csharp
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = WriteHealthJson
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,  // solo liveness: proceso vivo, sin dependencias
    ResponseWriter = WriteHealthJson
});

// Al final de Program.cs (función local):
static Task WriteHealthJson(HttpContext context, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
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
```

Usa el **mismo** `WriteHealthJson` + `MapHealthChecks` en OrderService y Gateway.

#### OrderService — `src/Services/OrderService/Program.cs`

**Paquete:**
```bash
cd src/Services/OrderService
dotnet add package AspNetCore.HealthChecks.Uris
```

**1) Registro** (después de definir `httpUrl`, antes de `var app = builder.Build()`):

```csharp
// Health Checks (Módulo 11) — verifica ProductService downstream
builder.Services.AddHealthChecks()
    .AddUrlGroup(
        new Uri($"{httpUrl}/api/v1/Products"),
        name: "product-service",
        tags: new[] { "dependency" });
```

**2) Endpoints** (después de `app.MapControllers()`):

> Por defecto `MapHealthChecks` responde **texto plano** (`Healthy`). Para poder usar `| jq`, usa el mismo `ResponseWriter` JSON (`WriteHealthJson`) documentado arriba en ProductService.

```csharp
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = WriteHealthJson
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthJson
});
```

#### Gateway — `src/Gateway/Program.cs`

**Paquete:**
```bash
cd src/Gateway
dotnet add package AspNetCore.HealthChecks.Uris
```

**1) Registro** (antes de `var app = builder.Build()`):

```csharp
builder.Services.AddHealthChecks()
    .AddUrlGroup(new Uri("http://localhost:5001/health"), name: "product-service", tags: new[] { "dependency" })
    .AddUrlGroup(new Uri("http://localhost:5003/health"), name: "order-service", tags: new[] { "dependency" });
```

**2) Endpoints** (después de `app.MapReverseProxy()` o junto a él):

Usa el mismo `WriteHealthJson` que en ProductService:

```csharp
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = WriteHealthJson
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = WriteHealthJson
});
```

Cada servicio expone:
- `GET /health` — Estado completo (incluye dependencias)
- `GET /health/live` — Liveness (solo verifica que el proceso responde)

> **Nota:** Estos endpoints son públicos (sin JWT). No hace falta `[AllowAnonymous]` porque no pasan por controllers con `[Authorize]`.

---

### Paso 6 — Configuración en appsettings.json

Se agregó la sección `Resilience` en `OrderService/appsettings.json`:

```json
{
  "Resilience": {
    "Retry": {
      "MaxRetryAttempts": 3,
      "BackoffBaseSeconds": 2
    },
    "CircuitBreaker": {
      "AllowedFailures": 3,
      "BreakDurationSeconds": 30
    },
    "Timeout": {
      "Seconds": 10
    }
  }
}
```

---

## 🧪 Cómo probar

### Prerrequisitos
Asegúrate de que la infraestructura está corriendo:
```bash
docker compose up -d   # PostgreSQL, Redis, RabbitMQ
```

### 1. Iniciar los servicios

```bash
# Terminal 1 — ProductService
cd src/Services/ProductService
dotnet run

# Terminal 2 — OrderService
cd src/Services/OrderService
dotnet run

# Terminal 3 — Gateway
cd src/Gateway
dotnet run
```

### 2. Obtener un JWT token (necesario para endpoints protegidos)

OrderService tiene `[Authorize]` a nivel de controlador. Los endpoints `GET /api/v1/Orders` y `GET /api/v1/Orders/available-products` son `[AllowAnonymous]`, pero **crear y eliminar órdenes requieren token con rol Admin**.

**Usuarios de prueba disponibles:**

| Usuario | Contraseña | Rol |
|---------|-----------|-----|
| `admin` | `admin123` | Admin |
| `reader` | `reader123` | Reader |
| `user` | `user123` | User |

```bash
# Obtener token (desde OrderService o ProductService — ambos tienen AuthController)
TOKEN=$(curl -s -X POST http://localhost:5003/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' | jq -r '.token')

echo $TOKEN
```

**Usar el token en endpoints protegidos:**
```bash
# 1) Obtener un productId real (no uses placeholders tipo <PRODUCT_ID>)
PRODUCT_ID=$(curl -s http://localhost:5001/api/v1/Products | jq -r '.[0].id')
echo "PRODUCT_ID=$PRODUCT_ID"

# 2) Crear una orden (requiere rol Admin)
curl -s -X POST http://localhost:5003/api/v1/Orders \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{
    \"customerName\": \"Juan Pérez\",
    \"items\": [
      { \"productId\": \"$PRODUCT_ID\", \"quantity\": 1 }
    ]
  }" | jq
```

> **Nota:** Los endpoints de lectura (`GET /api/v1/Orders`, `GET /api/v1/Orders/available-products`) y los health checks (`/health`, `/health/live`) **no requieren token**.

### 3. Probar Health Checks

```bash
# ProductService health (con detalle de PostgreSQL y Redis)
curl -s http://localhost:5001/health | jq

# OrderService health (verifica si ProductService responde)
curl -s http://localhost:5003/health | jq

# Gateway health (verifica ambos servicios)
curl -s http://localhost:5010/health | jq

# Liveness probe (respuesta mínima)
curl -s http://localhost:5001/health/live
curl -s http://localhost:5003/health/live
curl -s http://localhost:5010/health/live
```

**Respuesta esperada de `/health`:**
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "postgresql",
      "status": "Healthy",
      "description": null,
      "duration": "15.2ms"
    },
    {
      "name": "redis",
      "status": "Healthy",
      "description": null,
      "duration": "3.1ms"
    }
  ],
  "totalDuration": "18.5ms"
}
```

### 4. Probar Retry + Circuit Breaker

> **Nota:** El endpoint `available-products` es `[AllowAnonymous]`, no requiere token. Estos comandos funcionan directamente sin autenticación.

#### a) Funcionamiento normal (ProductService arriba)
```bash
# Obtener productos disponibles (funciona normal)
curl -s http://localhost:5003/api/v1/Orders/available-products | jq
```

#### b) Simular fallo — Detener ProductService
```bash
# Detener ProductService (Ctrl+C en Terminal 1)
# Luego intentar obtener productos:
curl -s http://localhost:5003/api/v1/Orders/available-products | jq
```

**¿Qué pasa internamente?**
1. OrderService intenta llamar a ProductService → falla
2. **Retry**: Reintenta 3 veces con backoff exponencial (~2s, ~4s, ~8s)
3. Los logs muestran cada reintento:
   ```
   warn: Retry 1 after 2345ms — (null)
   warn: Retry 2 after 4678ms — (null)
   warn: Retry 3 after 8901ms — (null)
   ```
4. Después de 3 fallos consecutivos el **Circuit Breaker** se abre:
   ```
   [CircuitBreaker] OPEN — pausing for 30s
   ```
5. Las siguientes llamadas fallan inmediatamente (sin esperar timeout)
6. El **Fallback** retorna datos cacheados (si hubo llamadas exitosas previas)

#### c) Probar recuperación — Reiniciar ProductService
```bash
# Iniciar ProductService de nuevo
cd src/Services/ProductService
dotnet run

# Esperar ~30 segundos (duración del break)
# El circuit breaker pasa a Half-Open:
# [CircuitBreaker] HALF-OPEN — testing...

# La siguiente llamada se ejecuta como prueba:
curl -s http://localhost:5003/api/v1/Orders/available-products | jq

# Si tiene éxito:
# [CircuitBreaker] CLOSED — recovered.
```

### 5. Probar Fallback cache

```bash
# 1. Primero, hacer una llamada exitosa (llena el cache)
curl -s http://localhost:5003/api/v1/Orders/available-products | jq

# 2. Detener ProductService
# (Ctrl+C en Terminal 1)

# 3. Llamar de nuevo — retorna datos cacheados
curl -s http://localhost:5003/api/v1/Orders/available-products | jq
# Los logs muestran:
# [Fallback] Returning 3 cached products
```

### 6. Probar Timeout

Para probar el timeout, puedes simular un servicio lento con un debugger o agregar un `await Task.Delay(15000)` temporal en el controlador de ProductService. Si la respuesta tarda más de 10 segundos, Polly cancela la petición y activa el retry.

---

## 📊 Diagrama de flujo de resiliencia

```
OrderService                           ProductService
    │                                       │
    ├─── HTTP Request ─────────────────────►│
    │    ┌─────────────┐                    │
    │    │  Timeout    │ (10s máx)          │
    │    │  Policy     │                    │
    │    └──────┬──────┘                    │
    │           │                           │
    │    ┌──────▼──────┐                    │
    │    │  Circuit    │                    │
    │    │  Breaker    │◄── 3 fallos = OPEN │
    │    └──────┬──────┘                    │
    │           │                           │
    │    ┌──────▼──────┐                    │
    │    │  Retry      │                    │
    │    │  (3x exp.)  │ ──── reintento ───►│
    │    └──────┬──────┘                    │
    │           │                           │
    │    ┌──────▼──────┐                    │
    │    │  Fallback   │                    │
    │    │  (cache)    │                    │
    │    └─────────────┘                    │
    │                                       │
```

---

## 📦 Paquetes NuGet utilizados

| Paquete | Servicio | Propósito |
|---------|----------|-----------|
| `Microsoft.Extensions.Http.Polly` | OrderService | Integración Polly con `IHttpClientFactory` |
| `AspNetCore.HealthChecks.Uris` | OrderService, Gateway | Health check via HTTP URL |
| `AspNetCore.HealthChecks.NpgSql` | ProductService | Health check PostgreSQL |
| `AspNetCore.HealthChecks.Redis` | ProductService | Health check Redis |

---

## 📎 Referencias

- [Polly — Resilience library for .NET](https://github.com/App-vNext/Polly)
- [Microsoft — Implement resilient HTTP requests](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/implement-http-call-retries-exponential-backoff-polly)
- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [Circuit Breaker Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker)

