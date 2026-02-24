# Módulo 6 – Configuración centralizada

## 🧠 Teoría

### Configuración en .NET

.NET proporciona un sistema de configuración flexible con múltiples proveedores:

```
Prioridad (de menor a mayor):
1. appsettings.json               ← Base
2. appsettings.{Environment}.json ← Sobreescribe según ambiente
3. User Secrets                   ← Solo en Development (no va al repo)
4. Variables de entorno            ← Ideal para contenedores/CI
5. Argumentos de línea de comando  ← Máxima prioridad
```

**Principio clave:** Los valores de mayor prioridad sobreescriben a los de menor prioridad. Esto permite tener una configuración base y personalizarla por ambiente sin cambiar código.

### Options Pattern

El **Options Pattern** permite mapear secciones de configuración a clases C# fuertemente tipadas:

```csharp
// En lugar de esto (frágil, strings mágicos):
var host = configuration["Redis:Host"];

// Hacer esto (fuertemente tipado, validable):
var host = options.Value.Host;
```

**Variantes de inyección:**
| Interfaz | Comportamiento |
|----------|---------------|
| `IOptions<T>` | Singleton, lee una vez al inicio |
| `IOptionsSnapshot<T>` | Scoped, re-lee en cada request |
| `IOptionsMonitor<T>` | Singleton, notifica cambios en tiempo real |

### User Secrets

Almacena configuración sensible **fuera del repositorio** durante desarrollo:
- Se guardan en `~/.microsoft/usersecrets/{id}/secrets.json`
- Nunca se commitean al repositorio
- Solo disponibles en el ambiente `Development`
- Ideal para connection strings, API keys, passwords

### Feature Flags

Permiten activar/desactivar funcionalidades **sin redeploy**:
- **A/B testing**: Mostrar diferentes versiones a usuarios
- **Rollout gradual**: Activar feature para un % de usuarios
- **Kill switches**: Desactivar features problemáticas inmediatamente
- **Configuración dinámica**: Cambiar comportamiento en runtime

### Config Server (Azure App Configuration)

Azure App Configuration proporciona un servicio centralizado para gestionar configuraciones:
- Configuración centralizada para múltiples microservicios
- Feature flags con filtros avanzados
- Versionamiento y auditoría de cambios
- Integración con Key Vault para secretos

### Secret Management (Azure Key Vault)

Azure Key Vault almacena secretos de forma segura:
- Connection strings, API keys, certificados
- Rotación automática de secretos
- Control de acceso granular (RBAC)
- Auditoría de acceso

## 🧪 Laboratorio 6 - Paso a Paso

### Objetivo
Implementar configuración centralizada y buenas prácticas:
- Options Pattern con clases fuertemente tipadas
- User Secrets para datos sensibles en desarrollo
- Configuración por ambiente (Development/Production)
- Feature Flags locales
- (Opcional) Azure App Configuration + Key Vault

### Paso 1: Analizar la configuración actual

Actualmente el `ProductService` tiene esta configuración:

**Archivo: `appsettings.json`**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=microservices_db;Username=postgres;Password=postgres",
    "Redis": "localhost:6379"
  }
}
```

**Problemas:**
- Las credenciales de PostgreSQL están hardcodeadas en el JSON
- No hay configuración tipada (se usan strings mágicos)
- No hay feature flags
- No hay diferenciación clara entre ambientes

### Paso 2: Crear clases de configuración (Options Pattern)

**Archivo: `Application/Configuration/ProductServiceSettings.cs`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace ProductService.Application.Configuration;

/// <summary>
/// Configuración general del servicio.
/// Se mapea desde la sección "ProductService" de appsettings.json.
/// </summary>
public class ProductServiceSettings
{
    public const string SectionName = "ProductService";

    [Required]
    public string ServiceName { get; set; } = "ProductService";

    public string ServiceVersion { get; set; } = "1.0.0";

    public int MaxPageSize { get; set; } = 50;

    public int DefaultPageSize { get; set; } = 10;
}
```

**Archivo: `Application/Configuration/CacheSettings.cs`**

```csharp
namespace ProductService.Application.Configuration;

/// <summary>
/// Configuración de cache.
/// Se mapea desde la sección "Cache" de appsettings.json.
/// </summary>
public class CacheSettings
{
    public const string SectionName = "Cache";

    /// <summary>Habilitar o deshabilitar cache</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Tiempo de expiración absoluta en minutos</summary>
    public int AbsoluteExpirationMinutes { get; set; } = 5;

    /// <summary>Tiempo de expiración deslizante en minutos</summary>
    public int SlidingExpirationMinutes { get; set; } = 1;

    /// <summary>Prefijo para las keys de Redis</summary>
    public string KeyPrefix { get; set; } = "products";
}
```

**Archivo: `Application/Configuration/FeatureFlagSettings.cs`**

```csharp
namespace ProductService.Application.Configuration;

/// <summary>
/// Feature flags del servicio.
/// Se mapea desde la sección "FeatureFlags" de appsettings.json.
/// </summary>
public class FeatureFlagSettings
{
    public const string SectionName = "FeatureFlags";

    /// <summary>Habilitar endpoint de búsqueda por nombre</summary>
    public bool EnableSearchByName { get; set; } = false;

    /// <summary>Habilitar respuestas con metadata extendida</summary>
    public bool EnableExtendedMetadata { get; set; } = false;

    /// <summary>Habilitar logging detallado de cache hits/misses</summary>
    public bool EnableCacheLogging { get; set; } = true;
}
```

Crear la carpeta necesaria:

**Linux/macOS (Bash/Zsh):**
```bash
mkdir -p Application/Configuration
```

**Windows (CMD):**
```cmd
mkdir Application\Configuration
```

**Windows (PowerShell):**
```powershell
mkdir -Force Application/Configuration
```

### Paso 3: Actualizar appsettings.json

**Archivo: `appsettings.json`** (reemplazar contenido completo)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=microservices_db;Username=postgres;Password=postgres",
    "Redis": "localhost:6379"
  },
  "ProductService": {
    "ServiceName": "ProductService",
    "ServiceVersion": "1.0.0",
    "MaxPageSize": 50,
    "DefaultPageSize": 10
  },
  "Cache": {
    "Enabled": true,
    "AbsoluteExpirationMinutes": 5,
    "SlidingExpirationMinutes": 1,
    "KeyPrefix": "products"
  },
  "FeatureFlags": {
    "EnableSearchByName": false,
    "EnableExtendedMetadata": false,
    "EnableCacheLogging": true
  }
}
```

### Paso 4: Crear appsettings por ambiente

**Archivo: `appsettings.Development.json`** (actualizar)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "Cache": {
    "AbsoluteExpirationMinutes": 1,
    "SlidingExpirationMinutes": 0
  },
  "FeatureFlags": {
    "EnableSearchByName": true,
    "EnableExtendedMetadata": true,
    "EnableCacheLogging": true
  }
}
```

**Archivo: `appsettings.Production.json`** (crear)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "Cache": {
    "Enabled": true,
    "AbsoluteExpirationMinutes": 10,
    "SlidingExpirationMinutes": 3
  },
  "FeatureFlags": {
    "EnableSearchByName": false,
    "EnableExtendedMetadata": false,
    "EnableCacheLogging": false
  }
}
```

**¿Qué cambia por ambiente?**

| Configuración | Development | Production |
|---------------|------------|------------|
| Log Level | Debug (más detalle) | Warning (solo problemas) |
| Cache expiration | 1 min (ver cambios rápido) | 10 min (rendimiento) |
| Feature Flags | Activados (testing) | Desactivados (estable) |
| Cache Logging | Sí | No |

### Paso 5: Configurar User Secrets

User Secrets permite guardar datos sensibles **fuera del repositorio**. Ideal para connection strings con credenciales reales.

**Linux/macOS (Bash/Zsh):**
```bash
# Inicializar User Secrets en el proyecto
dotnet user-secrets init

# Guardar connection strings como secretos
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=microservices_db;Username=postgres;Password=postgres"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"

# Verificar secretos guardados
dotnet user-secrets list
```

**Windows (CMD):**
```cmd
REM Inicializar User Secrets en el proyecto
dotnet user-secrets init

REM Guardar connection strings como secretos
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=microservices_db;Username=postgres;Password=postgres"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"

REM Verificar secretos guardados
dotnet user-secrets list
```

**Windows (PowerShell):**
```powershell
# Inicializar User Secrets en el proyecto
dotnet user-secrets init

# Guardar connection strings como secretos
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=microservices_db;Username=postgres;Password=postgres"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"

# Verificar secretos guardados
dotnet user-secrets list
```

Esto agrega un `UserSecretsId` al `.csproj`:

```xml
<PropertyGroup>
  <UserSecretsId>un-guid-generado-automaticamente</UserSecretsId>
</PropertyGroup>
```

Los secretos se guardan en:
- **Windows:** `%APPDATA%\Microsoft\UserSecrets\{id}\secrets.json`
- **macOS/Linux:** `~/.microsoft/usersecrets/{id}/secrets.json`

**⚠️ Nota:** En producción, los secretos se inyectan por variables de entorno o Azure Key Vault, nunca User Secrets.

### Paso 6: Registrar Options en Program.cs

**Archivo: `Program.cs`** (agregar después de `builder.Services.AddControllers()`)

```csharp
using ProductService.Application.Configuration;

// ... después de AddControllers() ...

// Configuración centralizada (Options Pattern)
builder.Services.Configure<ProductServiceSettings>(
    builder.Configuration.GetSection(ProductServiceSettings.SectionName));

builder.Services.Configure<CacheSettings>(
    builder.Configuration.GetSection(CacheSettings.SectionName));

builder.Services.Configure<FeatureFlagSettings>(
    builder.Configuration.GetSection(FeatureFlagSettings.SectionName));
```

### Paso 7: Crear endpoint de configuración (solo Development)

Este endpoint permite verificar la configuración activa. Solo debe estar disponible en desarrollo.

**Archivo: `Controllers/V1/ConfigController.cs`**

```csharp
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProductService.Application.Configuration;

namespace ProductService.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly IOptionsSnapshot<ProductServiceSettings> _serviceSettings;
    private readonly IOptionsSnapshot<CacheSettings> _cacheSettings;
    private readonly IOptionsSnapshot<FeatureFlagSettings> _featureFlags;
    private readonly IWebHostEnvironment _environment;

    public ConfigController(
        IOptionsSnapshot<ProductServiceSettings> serviceSettings,
        IOptionsSnapshot<CacheSettings> cacheSettings,
        IOptionsSnapshot<FeatureFlagSettings> featureFlags,
        IWebHostEnvironment environment)
    {
        _serviceSettings = serviceSettings;
        _cacheSettings = cacheSettings;
        _featureFlags = featureFlags;
        _environment = environment;
    }

    /// <summary>
    /// Obtener configuración activa (solo Development).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetConfig()
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        return Ok(new
        {
            Environment = _environment.EnvironmentName,
            Service = _serviceSettings.Value,
            Cache = _cacheSettings.Value,
            FeatureFlags = _featureFlags.Value
        });
    }

    /// <summary>
    /// Obtener feature flags activos.
    /// </summary>
    [HttpGet("features")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetFeatureFlags()
    {
        return Ok(_featureFlags.Value);
    }
}
```

**¿Por qué `IOptionsSnapshot<T>` y no `IOptions<T>`?**
- `IOptionsSnapshot<T>` es **Scoped**: re-lee la configuración en cada request
- Esto permite cambiar `appsettings.json` en caliente y ver los cambios sin reiniciar
- `IOptions<T>` es **Singleton**: solo lee una vez al inicio

### Paso 8: Usar Feature Flags en el controlador V2

**Archivo: `Controllers/V2/ProductsV2Controller.cs`** (agregar endpoint con feature flag)

Agregar al controlador V2 existente un endpoint de búsqueda controlado por feature flag:

```csharp
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ProductService.Application.Configuration;
using ProductService.Application.DTOs;
using ProductService.Application.Services;

namespace ProductService.Controllers.V2;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IOptionsSnapshot<FeatureFlagSettings> _featureFlags;
    private readonly IOptionsSnapshot<ProductServiceSettings> _serviceSettings;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IProductService productService,
        IOptionsSnapshot<FeatureFlagSettings> featureFlags,
        IOptionsSnapshot<ProductServiceSettings> serviceSettings,
        ILogger<ProductsController> logger)
    {
        _productService = productService;
        _featureFlags = featureFlags;
        _serviceSettings = serviceSettings;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        // Usar configuración tipada para el tamaño de página
        var effectivePageSize = Math.Min(
            pageSize ?? _serviceSettings.Value.DefaultPageSize,
            _serviceSettings.Value.MaxPageSize);

        var allProducts = await _productService.GetAllAsync(cancellationToken);
        var productsList = allProducts.ToList();

        var totalCount = productsList.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)effectivePageSize);

        var pagedProducts = productsList
            .Skip((page - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToList();

        var result = new PagedResult<ProductDto>
        {
            Items = pagedProducts,
            Page = page,
            PageSize = effectivePageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        return Ok(result);
    }

    /// <summary>
    /// Buscar productos por nombre (controlado por Feature Flag).
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SearchByName(
        [FromQuery] string name,
        CancellationToken cancellationToken = default)
    {
        // Verificar feature flag
        if (!_featureFlags.Value.EnableSearchByName)
        {
            _logger.LogWarning("SearchByName feature is disabled");
            return NotFound("This feature is currently disabled");
        }

        _logger.LogInformation("Searching products by name: {Name}", name);

        var allProducts = await _productService.GetAllAsync(cancellationToken);
        var filtered = allProducts
            .Where(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Ok(filtered);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productService.GetByIdAsync(id, cancellationToken);

        if (product == null)
        {
            return NotFound($"Product with ID {id} not found");
        }

        return Ok(product);
    }
}
```

### Paso 9: Usar CacheSettings en RedisProductCache

Actualizar el cache para usar la configuración tipada en lugar de valores hardcodeados.

**Archivo: `Infrastructure/Cache/RedisProductCache.cs`** (actualizar constructor y métodos)

```csharp
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using ProductService.Application.Configuration;
using ProductService.Application.DTOs;

namespace ProductService.Infrastructure.Cache;

public class RedisProductCache : IProductCache
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisProductCache> _logger;
    private readonly CacheSettings _cacheSettings;
    private readonly FeatureFlagSettings _featureFlags;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RedisProductCache(
        IDistributedCache cache,
        IOptionsSnapshot<CacheSettings> cacheSettings,
        IOptionsSnapshot<FeatureFlagSettings> featureFlags,
        ILogger<RedisProductCache> logger)
    {
        _cache = cache;
        _cacheSettings = cacheSettings.Value;
        _featureFlags = featureFlags.Value;
        _logger = logger;
    }

    private string AllProductsKey => $"{_cacheSettings.KeyPrefix}:all";
    private string ProductKey(Guid id) => $"{_cacheSettings.KeyPrefix}:{id}";

    private DistributedCacheEntryOptions GetCacheOptions() => new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheSettings.AbsoluteExpirationMinutes),
        SlidingExpiration = _cacheSettings.SlidingExpirationMinutes > 0
            ? TimeSpan.FromMinutes(_cacheSettings.SlidingExpirationMinutes)
            : null
    };

    public async Task<ProductDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_cacheSettings.Enabled) return null;

        var cached = await _cache.GetStringAsync(ProductKey(id), cancellationToken);

        if (string.IsNullOrEmpty(cached))
        {
            if (_featureFlags.EnableCacheLogging)
                _logger.LogDebug("Cache MISS for product {ProductId}", id);
            return null;
        }

        if (_featureFlags.EnableCacheLogging)
            _logger.LogDebug("Cache HIT for product {ProductId}", id);

        return JsonSerializer.Deserialize<ProductDto>(cached, JsonOptions);
    }

    public async Task<IEnumerable<ProductDto>?> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_cacheSettings.Enabled) return null;

        var cached = await _cache.GetStringAsync(AllProductsKey, cancellationToken);

        if (string.IsNullOrEmpty(cached))
        {
            if (_featureFlags.EnableCacheLogging)
                _logger.LogDebug("Cache MISS for all products");
            return null;
        }

        if (_featureFlags.EnableCacheLogging)
            _logger.LogDebug("Cache HIT for all products");

        return JsonSerializer.Deserialize<IEnumerable<ProductDto>>(cached, JsonOptions);
    }

    public async Task SetAsync(Guid id, ProductDto product, CancellationToken cancellationToken = default)
    {
        if (!_cacheSettings.Enabled) return;

        var json = JsonSerializer.Serialize(product, JsonOptions);
        await _cache.SetStringAsync(ProductKey(id), json, GetCacheOptions(), cancellationToken);
        await RemoveAllAsync(cancellationToken);
    }

    public async Task SetAllAsync(IEnumerable<ProductDto> products, CancellationToken cancellationToken = default)
    {
        if (!_cacheSettings.Enabled) return;

        var json = JsonSerializer.Serialize(products, JsonOptions);
        await _cache.SetStringAsync(AllProductsKey, json, GetCacheOptions(), cancellationToken);
    }

    public async Task RemoveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(ProductKey(id), cancellationToken);
        await RemoveAllAsync(cancellationToken);
    }

    public async Task RemoveAllAsync(CancellationToken cancellationToken = default)
    {
        await _cache.RemoveAsync(AllProductsKey, cancellationToken);
    }
}
```

### Paso 10: Usar configuración por variables de entorno

Las variables de entorno sobrescriben `appsettings.json`. Esto es fundamental para contenedores y CI/CD.

**Convención de nombres:** Los `:` del JSON se reemplazan por `__` (doble guión bajo).

**Linux/macOS (Bash/Zsh):**
```bash
# Sobrescribir connection string por variable de entorno
export ConnectionStrings__DefaultConnection="Host=prod-server;Port=5432;Database=prod_db;Username=app;Password=SecureP@ss"

# Sobrescribir feature flags
export FeatureFlags__EnableSearchByName=true

# Sobrescribir configuración de cache
export Cache__AbsoluteExpirationMinutes=15

# Ejecutar con la configuración sobrescrita
dotnet run
```

**Windows (CMD):**
```cmd
REM Sobrescribir connection string por variable de entorno
set ConnectionStrings__DefaultConnection=Host=prod-server;Port=5432;Database=prod_db;Username=app;Password=SecureP@ss

REM Sobrescribir feature flags
set FeatureFlags__EnableSearchByName=true

REM Ejecutar con la configuración sobrescrita
dotnet run
```

**Windows (PowerShell):**
```powershell
# Sobrescribir connection string por variable de entorno
$env:ConnectionStrings__DefaultConnection = "Host=prod-server;Port=5432;Database=prod_db;Username=app;Password=SecureP@ss"

# Sobrescribir feature flags
$env:FeatureFlags__EnableSearchByName = "true"

# Ejecutar con la configuración sobrescrita
dotnet run
```

**Esto es clave para contenedores (Docker/Podman) y Kubernetes:**
```yaml
# docker-compose.yml o podman-compose.yml o Kubernetes manifest
environment:
  - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;...
  - Cache__Enabled=true
  - FeatureFlags__EnableSearchByName=false
```

### Paso 11: Compilar y Ejecutar

**Linux/macOS (Bash/Zsh):**
```bash
# Compilar
dotnet build

# Ejecutar
dotnet run
```

**Windows (CMD):**
```cmd
REM Compilar
dotnet build

REM Ejecutar
dotnet run
```

**Windows (PowerShell):**
```powershell
# Compilar
dotnet build

# Ejecutar
dotnet run
```

### Paso 12: Probar Configuración

**⚠️ Importante:** Verifica el puerto en `Properties/launchSettings.json`. Por defecto es `5001`.

#### Ver configuración activa

**Linux/macOS (Bash/Zsh):**
```bash
# Ver toda la configuración activa (solo funciona en Development)
curl http://localhost:5001/api/v1/Config | jq

# Ver solo feature flags
curl http://localhost:5001/api/v1/Config/features | jq
```

**Windows (CMD):**
```cmd
curl http://localhost:5001/api/v1/Config
curl http://localhost:5001/api/v1/Config/features
```

**Windows (PowerShell):**
```powershell
Invoke-RestMethod http://localhost:5001/api/v1/Config | ConvertTo-Json -Depth 5
Invoke-RestMethod http://localhost:5001/api/v1/Config/features | ConvertTo-Json
```

**Respuesta esperada:**
```json
{
  "environment": "Development",
  "service": {
    "serviceName": "ProductService",
    "serviceVersion": "1.0.0",
    "maxPageSize": 50,
    "defaultPageSize": 10
  },
  "cache": {
    "enabled": true,
    "absoluteExpirationMinutes": 1,
    "slidingExpirationMinutes": 0,
    "keyPrefix": "products"
  },
  "featureFlags": {
    "enableSearchByName": true,
    "enableExtendedMetadata": true,
    "enableCacheLogging": true
  }
}
```

**Nota:** Los valores de `cache` y `featureFlags` vienen de `appsettings.Development.json` (sobreescriben a `appsettings.json`).

#### Probar Feature Flag: Búsqueda por nombre

**Linux/macOS (Bash/Zsh):**
```bash
# Buscar productos por nombre (v2 - feature flag habilitado en Development)
curl "http://localhost:5001/api/v2/Products/search?name=Laptop" | jq

# Buscar con nombre parcial
curl "http://localhost:5001/api/v2/Products/search?name=key" | jq
```

**Windows (CMD):**
```cmd
curl "http://localhost:5001/api/v2/Products/search?name=Laptop"
```

**Windows (PowerShell):**
```powershell
Invoke-RestMethod "http://localhost:5001/api/v2/Products/search?name=Laptop" | ConvertTo-Json
```

#### Probar paginación con configuración tipada

**Linux/macOS (Bash/Zsh):**
```bash
# Usar paginación (pageSize viene de la configuración si no se especifica)
curl "http://localhost:5001/api/v2/Products" | jq

# Intentar un pageSize mayor al MaxPageSize (50) - será limitado automáticamente
curl "http://localhost:5001/api/v2/Products?pageSize=100" | jq
```

**Windows (PowerShell):**
```powershell
# Paginación por defecto
Invoke-RestMethod "http://localhost:5001/api/v2/Products" | ConvertTo-Json -Depth 3

# Intentar exceder el máximo
Invoke-RestMethod "http://localhost:5001/api/v2/Products?pageSize=100" | ConvertTo-Json -Depth 3
```

### Paso 13: Probar cambio en caliente

Gracias a `IOptionsSnapshot`, los cambios en `appsettings.json` se aplican sin reiniciar:

1. Con el servicio corriendo, editar `appsettings.Development.json`
2. Cambiar `"EnableSearchByName": true` a `"EnableSearchByName": false`
3. Guardar el archivo
4. Hacer un request al endpoint de búsqueda:

```bash
# Debería devolver 404 "This feature is currently disabled"
curl "http://localhost:5001/api/v2/Products/search?name=Laptop" | jq
```

5. Volver a cambiar a `true` y verificar que funciona de nuevo sin reiniciar.

### Paso 14 (Opcional): Azure App Configuration + Key Vault

> **Requisito:** Necesitas una suscripción de Azure. Puedes obtener una cuenta gratuita en https://azure.microsoft.com/free/

#### 14.1: Instalar paquetes

```bash
dotnet add package Microsoft.Azure.AppConfiguration.AspNetCore
dotnet add package Azure.Identity
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
```

#### 14.2: Crear recursos en Azure

```bash
# Login en Azure
az login

# Crear resource group
az group create --name rg-microservices --location eastus

# Crear App Configuration
az appconfig create --name appconfig-microservices --resource-group rg-microservices --location eastus --sku Free

# Crear Key Vault
az keyvault create --name kv-microservices-$RANDOM --resource-group rg-microservices --location eastus

# Guardar connection string de PostgreSQL como secreto en Key Vault
az keyvault secret set --vault-name kv-microservices-[$RANDOM] --name "ConnectionStrings--DefaultConnection" --value "Host=prod-server;Port=5432;Database=prod_db;Username=app;Password=SecureP@ss"

# Guardar configuración en App Configuration
az appconfig kv set --name appconfig-microservices --key "ProductService:ServiceName" --value "ProductService" --yes
az appconfig kv set --name appconfig-microservices --key "Cache:Enabled" --value "true" --yes
az appconfig kv set --name appconfig-microservices --key "FeatureFlags:EnableSearchByName" --value "true" --yes

# Crear referencia a Key Vault desde App Configuration
az appconfig kv set-keyvault --name appconfig-microservices --key "ConnectionStrings:DefaultConnection" --secret-identifier "https://kv-microservices.vault.azure.net/secrets/ConnectionStrings--DefaultConnection" --yes
```

#### 14.3: Configurar en Program.cs

```csharp
using Azure.Identity;

// Agregar Azure App Configuration (después de builder creation)
if (!string.IsNullOrEmpty(builder.Configuration["AppConfig:Endpoint"]))
{
    builder.Configuration.AddAzureAppConfiguration(options =>
    {
        var endpoint = builder.Configuration["AppConfig:Endpoint"];
        var credential = new DefaultAzureCredential();

        options.Connect(new Uri(endpoint), credential)
            .Select("ProductService:*")         // Configuración del servicio
            .Select("Cache:*")                  // Configuración de cache
            .Select("FeatureFlags:*")           // Feature flags
            .ConfigureKeyVault(kv =>
            {
                kv.SetCredential(credential);   // Para resolver referencias a Key Vault
            })
            .ConfigureRefresh(refresh =>
            {
                refresh.Register("ProductService:Sentinel", refreshAll: true)
                    .SetRefreshInterval(TimeSpan.FromSeconds(30));
            });
    });
}
```

#### 14.4: Agregar middleware de refresh

```csharp
// Después de var app = builder.Build();
if (!string.IsNullOrEmpty(builder.Configuration["AppConfig:Endpoint"]))
{
    app.UseAzureAppConfiguration();  // Middleware para refresh automático
}
```

#### 14.5: Configurar endpoint en appsettings

```json
{
  "AppConfig": {
    "Endpoint": "https://appconfig-microservices.azconfig.io"
  }
}
```

**⚠️ Nota:** En producción, el endpoint de App Configuration se inyecta por variable de entorno, no en `appsettings.json`.

### ✅ Checklist de Verificación

- [ ] Clases de configuración creadas (`ProductServiceSettings`, `CacheSettings`, `FeatureFlagSettings`)
- [ ] `appsettings.json` actualizado con secciones de configuración
- [ ] `appsettings.Development.json` con valores para desarrollo
- [ ] `appsettings.Production.json` con valores para producción
- [ ] User Secrets inicializado (`dotnet user-secrets init`)
- [ ] Options Pattern registrado en `Program.cs` con `Configure<T>()`
- [ ] `ConfigController` creado para verificar configuración activa
- [ ] Feature flag `EnableSearchByName` funciona en endpoint de búsqueda
- [ ] `IOptionsSnapshot<T>` usado en controllers (re-lee en cada request)
- [ ] Paginación usa `MaxPageSize` y `DefaultPageSize` de configuración
- [ ] `RedisProductCache` usa `CacheSettings` en lugar de valores hardcodeados
- [ ] Cambio en caliente funciona (editar JSON sin reiniciar)
- [ ] Variables de entorno sobrescriben configuración
- [ ] Proyecto compila sin errores (`dotnet build`)
- [ ] (Opcional) Azure App Configuration conectado
- [ ] (Opcional) Azure Key Vault para secretos

### 📊 Estructura Creada

```
ProductService/
├── Application/
│   └── Configuration/
│       ├── ProductServiceSettings.cs   # Configuración del servicio
│       ├── CacheSettings.cs            # Configuración de cache
│       └── FeatureFlagSettings.cs      # Feature flags
├── Controllers/
│   └── V1/
│       └── ConfigController.cs         # Endpoint de configuración
├── appsettings.json                    # Configuración base
├── appsettings.Development.json        # Configuración de desarrollo
└── appsettings.Production.json         # Configuración de producción
```

### 💡 Conceptos Clave

**Jerarquía de configuración (menor a mayor prioridad):**
```
appsettings.json
  ↓ sobreescrito por
appsettings.{Environment}.json
  ↓ sobreescrito por
User Secrets (solo Development)
  ↓ sobreescrito por
Variables de entorno
  ↓ sobreescrito por
Argumentos de línea de comando
  ↓ sobreescrito por
Azure App Configuration (si configurado)
```

**Options Pattern:**
- `IOptions<T>`: Singleton, se lee una sola vez
- `IOptionsSnapshot<T>`: Scoped, se re-lee por request (recomendado para controllers)
- `IOptionsMonitor<T>`: Singleton con notificación de cambios (recomendado para servicios singleton)

**Feature Flags:**
- Permiten activar/desactivar features sin redeploy
- En desarrollo: habilitados para testing
- En producción: desactivados hasta que estén listos
- Con Azure App Configuration: se pueden cambiar en tiempo real desde el portal

**User Secrets vs Variables de entorno:**

| Característica | User Secrets | Variables de Entorno |
|---------------|-------------|---------------------|
| Ámbito | Solo Development | Cualquier ambiente |
| Persistencia | Archivo local | Proceso/sistema |
| Uso ideal | Desarrollo local | Docker/K8s/CI |
| Prioridad | 3 | 4 (más alta) |

### 🔄 Comparación: Antes vs Después

| Aspecto | Antes (Módulo 5) | Después (Módulo 6) |
|---------|-----------------|-------------------|
| Connection strings | Hardcoded en JSON | User Secrets + env vars |
| Cache config | Valores mágicos en código | `CacheSettings` tipado |
| Paginación | Valores fijos (10, 50) | `ProductServiceSettings` configurable |
| Feature flags | No existían | `FeatureFlagSettings` por ambiente |
| Ambientes | Un solo `appsettings.json` | Development + Production |
| Cambios en config | Requiere reinicio | En caliente con `IOptionsSnapshot` |

### 🐛 Solución de Problemas

**Error: "UserSecretsId not found"**
- Ejecutar `dotnet user-secrets init` desde la carpeta del proyecto
- Verificar que el `.csproj` tenga el `<UserSecretsId>`

**La configuración no cambia en caliente**
- Verificar que se usa `IOptionsSnapshot<T>` (no `IOptions<T>`)
- `IOptions<T>` es Singleton y solo lee una vez al inicio
- `IOptionsSnapshot<T>` es Scoped y re-lee por request

**Feature flag no se activa**
- Verificar que `appsettings.Development.json` sobreescribe la sección correcta
- Verificar que `ASPNETCORE_ENVIRONMENT=Development` esté configurado
- Usar el endpoint `/api/v1/Config/features` para verificar valores activos

**Variables de entorno no funcionan**
- Recordar que los `:` se reemplazan por `__` (doble guión bajo)
- Ejemplo: `Cache:Enabled` → `Cache__Enabled`
- En PowerShell usar `$env:Cache__Enabled = "true"`
- En bash usar `export Cache__Enabled=true`

**Error: "Cannot resolve IOptionsSnapshot in Singleton"**
- `IOptionsSnapshot<T>` es Scoped, no puede inyectarse en servicios Singleton
- Para servicios Singleton, usar `IOptionsMonitor<T>` en su lugar
- Ejemplo: si `RedisProductCache` es Singleton, usar `IOptionsMonitor<CacheSettings>`

**ConfigController devuelve 404**
- El endpoint solo funciona en ambiente `Development`
- Verificar que `ASPNETCORE_ENVIRONMENT=Development` esté configurado en `launchSettings.json`
- En producción, devuelve 404 intencionalmente por seguridad

**(Opcional) Azure App Configuration no conecta**
- Verificar que `az login` se ejecutó correctamente
- Verificar que `DefaultAzureCredential` tiene permisos
- Verificar el endpoint en `appsettings.json` o variable de entorno
- Ejecutar `az appconfig kv list --name appconfig-microservices` para verificar valores
