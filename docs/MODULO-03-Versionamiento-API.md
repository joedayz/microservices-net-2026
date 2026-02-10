# Módulo 3 – Buenas prácticas de diseño

## 🧠 Teoría

### Versionamiento de API

El versionamiento permite evolucionar APIs sin romper clientes existentes.

**Estrategias comunes:**

1. **URL Versioning** (Recomendado)
   - `GET /api/v1/products`
   - `GET /api/v2/products`

2. **Header Versioning**
   - `X-Version: 2.0`
   - `Accept: application/vnd.api+json;version=2`

3. **Query String Versioning**
   - `GET /api/products?version=2`

**Mejores prácticas:**
- Versionar desde el inicio (v1)
- Documentar cambios entre versiones
- Mantener versiones anteriores por tiempo razonable
- Comunicar deprecación con anticipación

### DTOs vs Entities

**Entities (Domain):**
- Representan conceptos del negocio
- Contienen lógica de dominio
- No deben exponerse directamente en APIs

**DTOs (Data Transfer Objects):**
- Objetos planos para transferencia
- Sin lógica de negocio
- Pueden diferir de entidades
- Protegen el dominio

**Ventajas de usar DTOs:**
- Desacoplamiento de dominio
- Control de qué se expone
- Flexibilidad para cambios
- Mejor rendimiento (proyecciones)

### Idempotencia

Operaciones idempotentes pueden ejecutarse múltiples veces sin cambiar el resultado:
- `GET /products/{id}` - Siempre idempotente
- `PUT /products/{id}` - Debe ser idempotente
- `DELETE /products/{id}` - Debe ser idempotente
- `POST /products` - NO es idempotente (usa idempotency-key)

### Regla del 12-Factor

Aplicación para construir aplicaciones SaaS modernas:

1. **Codebase**: Un código base, múltiples despliegues
2. **Dependencies**: Declarar y aislar dependencias
3. **Config**: Configuración en el entorno
4. **Backing services**: Tratar servicios de respaldo como recursos adjuntos
5. **Build, release, run**: Separar etapas de construcción y ejecución
6. **Processes**: Ejecutar la aplicación como uno o más procesos sin estado
7. **Port binding**: Exportar servicios mediante vinculación de puertos
8. **Concurrency**: Escalar mediante el modelo de procesos
9. **Disposability**: Maximizar la robustez con inicio rápido y apagado elegante
10. **Dev/prod parity**: Mantener desarrollo, staging y producción lo más similares posible
11. **Logs**: Tratar logs como flujos de eventos
12. **Admin processes**: Ejecutar tareas administrativas como procesos de un solo uso

## 🧪 Laboratorio 3 - Paso a Paso

### Objetivo
Implementar versionamiento de API y mejorar Swagger:
- Versión 1.0: API básica
- Versión 2.0: API con paginación
- Swagger UI mejorado

### Paso 1: Agregar Paquetes NuGet

**Linux/macOS (Bash/Zsh):**
```bash
# Desde la carpeta ProductService
dotnet add package Asp.Versioning.Mvc
dotnet add package Asp.Versioning.Mvc.ApiExplorer
dotnet add package Swashbuckle.AspNetCore
```

**Windows (CMD):**
```cmd
REM Desde la carpeta ProductService
dotnet add package Asp.Versioning.Mvc
dotnet add package Asp.Versioning.Mvc.ApiExplorer
dotnet add package Swashbuckle.AspNetCore
```

**Windows (PowerShell):**
```powershell
# Desde la carpeta ProductService
dotnet add package Asp.Versioning.Mvc
dotnet add package Asp.Versioning.Mvc.ApiExplorer
dotnet add package Swashbuckle.AspNetCore
```

El `.csproj` debería quedar así:

```xml
<ItemGroup>
  <PackageReference Include="Asp.Versioning.Mvc" Version="8.1.1" />
  <PackageReference Include="Asp.Versioning.Mvc.ApiExplorer" Version="8.1.1" />
  <PackageReference Include="Swashbuckle.AspNetCore" Version="10.1.2" />
</ItemGroup>
```

**⚠️ Nota importante:** No agregues `Microsoft.AspNetCore.OpenApi` ya que causa conflictos con Swashbuckle en .NET 10. Swashbuckle incluye sus propias dependencias de OpenAPI.

### Paso 2: Crear Estructura de Carpetas

**Linux/macOS (Bash/Zsh):**
```bash
# Crear carpetas para versiones
mkdir -p Controllers/V1
mkdir -p Controllers/V2
```

**Windows (CMD):**
```cmd
REM Crear carpetas para versiones
mkdir Controllers\V1
mkdir Controllers\V2
```

**Windows (PowerShell):**
```powershell
# Crear carpetas para versiones
mkdir -Force Controllers/V1
mkdir -Force Controllers/V2
```

### Paso 3: Configurar API Versioning en Program.cs

**Archivo: `Program.cs`** (agregar los usings y configurar API Versioning después de `AddControllers()`)

```csharp
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.OpenApi;
using ProductService;
using ProductService.Application.Services;
using ProductService.Domain;
using ProductService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// API Versioning
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
```

### Paso 4: Crear ConfigureSwaggerOptions

Para que Swagger descubra automáticamente las versiones de API, necesitamos una clase que implemente `IConfigureOptions<SwaggerGenOptions>`.

**Archivo: `ConfigureSwaggerOptions.cs`** (crear en la raíz del proyecto ProductService)

```csharp
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ProductService;

public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(
                description.GroupName,
                new OpenApiInfo
                {
                    Title = "ProductService API",
                    Version = description.ApiVersion.ToString(),
                    Description = $"API version {description.ApiVersion}"
                }
            );
        }
    }
}
```

**¿Por qué una clase separada?** En .NET 10 con Swashbuckle 10.x, la API de `Microsoft.OpenApi` cambió y no se puede usar `OpenApiInfo` directamente en `Program.cs` con `using Microsoft.OpenApi.Models`. La solución es usar `IConfigureOptions<SwaggerGenOptions>` con `using Microsoft.OpenApi` directamente, que sí funciona correctamente.

### Paso 5: Configurar Swagger en Program.cs

**Archivo: `Program.cs`** (agregar después de API Versioning)

```csharp
// Swagger / OpenAPI (versioned)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();
```

**Archivo: `Program.cs`** (configurar pipeline - después de `var app = builder.Build()`)

```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                description.GroupName.ToUpperInvariant()
            );
        }

        options.RoutePrefix = string.Empty; // Swagger UI en la raíz
    });
}
```

### Paso 6: Crear Controlador V1

**Archivo: `Controllers/V1/ProductsV1Controller.cs`**

```csharp
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs;
using ProductService.Application.Services;

namespace ProductService.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IProductService productService,
        ILogger<ProductsController> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll(CancellationToken cancellationToken)
    {
        var products = await _productService.GetAllAsync(cancellationToken);
        return Ok(products);
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

    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Create(
        [FromBody] CreateProductDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var product = await _productService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = product.Id, version = "1.0" }, product);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] CreateProductDto dto,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var updated = await _productService.UpdateAsync(id, dto, cancellationToken);
        if (!updated)
        {
            return NotFound($"Product with ID {id} not found");
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _productService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound($"Product with ID {id} not found");
        }

        return NoContent();
    }
}
```

### Paso 7: Crear DTO para Respuesta Paginada

**Archivo: `Application/DTOs/PagedResult.cs`**

```csharp
namespace ProductService.Application.DTOs;

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
```

### Paso 8: Crear Controlador V2 con Paginación

**Archivo: `Controllers/V2/ProductsV2Controller.cs`**

```csharp
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs;
using ProductService.Application.Services;

namespace ProductService.Controllers.V2;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IProductService productService,
        ILogger<ProductsController> logger)
    {
        _productService = productService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var allProducts = await _productService.GetAllAsync(cancellationToken);
        var productsList = allProducts.ToList();
        
        var totalCount = productsList.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        
        var pagedProducts = productsList
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var result = new PagedResult<ProductDto>
        {
            Items = pagedProducts,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        return Ok(result);
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

### Paso 9: Actualizar Program.cs completo

**Archivo: `Program.cs`** (versión completa)

```csharp
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.OpenApi;
using ProductService;
using ProductService.Application.Services;
using ProductService.Domain;
using ProductService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// API Versioning
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

// Swagger / OpenAPI (versioned)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

// DI
builder.Services.AddSingleton<IProductRepository, InMemoryProductRepository>();
builder.Services.AddSingleton<IProductService, ProductService.Application.Services.ProductService>();

var app = builder.Build();

// HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

        foreach (var description in provider.ApiVersionDescriptions)
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                description.GroupName.ToUpperInvariant()
            );
        }

        options.RoutePrefix = string.Empty; // Swagger en /
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Seed inicial
using (var scope = app.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Seeding initial data...");
    await SeedDataAsync(repository);
    logger.LogInformation("Seed data completed successfully");
}

app.Run();

static async Task SeedDataAsync(IProductRepository repository)
{
    var products = new[]
    {
        new Product("Laptop", "High-performance laptop", 1299.99m, 10),
        new Product("Mouse", "Wireless mouse", 29.99m, 50),
        new Product("Keyboard", "Mechanical keyboard", 89.99m, 30)
    };

    foreach (var product in products)
    {
        await repository.CreateAsync(product);
        Console.WriteLine($"Seeded product: {product.Name} (ID: {product.Id})");
    }
}
```

### Paso 10: Eliminar Controlador Antiguo

**Linux/macOS (Bash/Zsh):**
```bash
# Eliminar el controlador sin versión (si existe)
rm -f Controllers/ProductsController.cs
```

**Windows (CMD):**
```cmd
REM Eliminar el controlador sin versión (si existe)
del Controllers\ProductsController.cs
```

**Windows (PowerShell):**
```powershell
# Eliminar el controlador sin versión (si existe)
Remove-Item -Force Controllers/ProductsController.cs -ErrorAction SilentlyContinue
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

### Paso 12: Probar Versionamiento

**⚠️ Importante:** Verifica el puerto en `Properties/launchSettings.json`. Por defecto es `5001`.

#### Opción 1: Por URL (Recomendado)

**Linux/macOS (Bash/Zsh):**
```bash
# Versión 1 - Todos los productos
curl http://localhost:5001/api/v1/Products

# Versión 1 - Con formato JSON
curl http://localhost:5001/api/v1/Products | jq

# Versión 2 (con paginación)
curl "http://localhost:5001/api/v2/Products?page=1&pageSize=5" | jq

# Obtener producto específico (reemplazar {id} con un GUID real)
curl http://localhost:5001/api/v1/Products/{id} | jq
```

**Windows (CMD):**
```cmd
REM Versión 1 - Todos los productos
curl http://localhost:5001/api/v1/Products

REM Versión 2 (con paginación)
curl "http://localhost:5001/api/v2/Products?page=1&pageSize=5"

REM Obtener producto específico (reemplazar {id} con un GUID real)
curl http://localhost:5001/api/v1/Products/{id}
```

**Windows (PowerShell):**
```powershell
# Versión 1 - Todos los productos
Invoke-RestMethod http://localhost:5001/api/v1/Products | ConvertTo-Json

# Versión 2 (con paginación)
Invoke-RestMethod "http://localhost:5001/api/v2/Products?page=1&pageSize=5" | ConvertTo-Json

# Obtener producto específico (reemplazar {id} con un GUID real)
Invoke-RestMethod http://localhost:5001/api/v1/Products/{id} | ConvertTo-Json
```

#### Opción 2: Por Header

**Linux/macOS (Bash/Zsh):**
```bash
# Nota: Solo funciona si existe un controlador con ruta /api/[controller] sin {version}
curl -H "X-Version: 2.0" http://localhost:5001/api/Products
```

**Windows (PowerShell):**
```powershell
Invoke-RestMethod -Headers @{"X-Version"="2.0"} http://localhost:5001/api/Products | ConvertTo-Json
```

#### Opción 3: Por Query String

**Linux/macOS (Bash/Zsh):**
```bash
curl "http://localhost:5001/api/Products?version=2.0"
```

**Windows (PowerShell):**
```powershell
Invoke-RestMethod "http://localhost:5001/api/Products?version=2.0" | ConvertTo-Json
```

**Nota sobre las rutas:**
- ✅ `/api/v1/Products` - Versión explícita v1 (recomendado para producción)
- ✅ `/api/v2/Products` - Versión explícita v2 con paginación
- El `[controller]` en la ruta se reemplaza con "Products" (sin "Controller")
- **Recomendación:** Usa rutas versionadas (`/api/v1/Products`) para mayor claridad y control
- Las opciones por Header y Query String solo funcionan si existe un controlador con ruta `/api/[controller]` sin `{version}` en la URL

### Paso 13: Acceder a Swagger UI

1. Abrir navegador: `http://localhost:5001`
   - **Nota:** Verifica el puerto en `Properties/launchSettings.json`
   - Swagger está configurado con `RoutePrefix = string.Empty`, por lo que se abre directamente en la raíz
2. Verás un **selector desplegable** en la parte superior derecha para elegir entre:
   - **V1** - API v1.0 (CRUD completo)
   - **V2** - API v2.0 (con paginación)
3. Probar endpoints desde Swagger UI
4. Swagger mostrará las rutas correctas: `/api/v1/Products` y `/api/v2/Products`

**Ejemplo visual del selector:**
```
Select a definition: [ V1  ▼ ]
                      [ V1    ]
                      [ V2    ]
```

### ✅ Checklist de Verificación

- [ ] Paquetes de versionamiento instalados (`Asp.Versioning.Mvc`, `Asp.Versioning.Mvc.ApiExplorer`)
- [ ] Swashbuckle instalado (`Swashbuckle.AspNetCore`)
- [ ] **No** se incluye `Microsoft.AspNetCore.OpenApi` ni `Microsoft.OpenApi` explícitamente
- [ ] Carpetas `Controllers/V1` y `Controllers/V2` creadas
- [ ] API Versioning configurado en `Program.cs`
- [ ] `ConfigureSwaggerOptions.cs` creado con descubrimiento dinámico de versiones
- [ ] `ConfigureSwaggerOptions` registrado con `builder.Services.ConfigureOptions<>()`
- [ ] `UseSwaggerUI` usa `IApiVersionDescriptionProvider` para endpoints dinámicos
- [ ] ProductsV1Controller creado con CRUD completo
- [ ] ProductsV2Controller creado con paginación
- [ ] PagedResult DTO creado
- [ ] Controlador antiguo sin versión eliminado
- [ ] Proyecto compila sin errores (`dotnet build`)
- [ ] Swagger UI abre en `http://localhost:5001`
- [ ] Swagger UI muestra ambas versiones (V1 y V2) en el selector
- [ ] Endpoint v1 funciona sin paginación
- [ ] Endpoint v2 funciona con paginación
- [ ] Versionamiento por URL funciona (`/api/v1/Products`, `/api/v2/Products`)

### 📊 Estructura Final

```
ProductService/
├── ConfigureSwaggerOptions.cs      # Configuración dinámica de Swagger por versión
├── Program.cs                      # Configuración principal (DI, versioning, pipeline)
├── Controllers/
│   ├── V1/
│   │   └── ProductsV1Controller.cs # API v1.0 (CRUD completo)
│   └── V2/
│       └── ProductsV2Controller.cs # API v2.0 (con paginación)
├── Application/
│   ├── DTOs/
│   │   ├── ProductDto.cs           # DTO de producto
│   │   ├── CreateProductDto.cs     # DTO para crear producto
│   │   └── PagedResult.cs          # DTO para respuestas paginadas
│   └── Services/
│       ├── IProductService.cs      # Interfaz del servicio
│       └── ProductService.cs       # Implementación del servicio
├── Domain/
│   ├── Product.cs                  # Entidad de dominio
│   └── IProductRepository.cs       # Puerto de repositorio
└── Infrastructure/
    └── InMemoryProductRepository.cs # Adaptador de repositorio
```

### 💡 Conceptos Aplicados

✅ **Versionamiento por URL**: `/api/v1/products` vs `/api/v2/products`
✅ **Versionamiento por Header**: `X-Version: 2.0`
✅ **Versionamiento por Query**: `?version=2.0`
✅ **DTOs separados de Entities**: Nunca exponer entidades directamente
✅ **Swagger versionado con `IConfigureOptions`**: Descubrimiento automático de versiones
✅ **Paginación**: Mejora rendimiento con grandes datasets
✅ **Respuestas estructuradas**: Metadatos en respuestas

### 🔄 Comparación de Versiones

| Característica | v1.0 | v2.0 |
|---------------|------|------|
| Paginación | ❌ | ✅ |
| Metadatos | ❌ | ✅ |
| CRUD completo | ✅ | Parcial (GET) |
| Compatibilidad | Base | Extendida |

### 🐛 Solución de Problemas

**Error: `CS0234: The type or namespace name 'Models' does not exist in the namespace 'Microsoft.OpenApi'`**
- En .NET 10 con Swashbuckle 10.x, la API de `Microsoft.OpenApi` cambió significativamente
- **No se puede usar** `Microsoft.OpenApi.Models.OpenApiInfo` directamente en `Program.cs`
- **Solución:** Crear la clase `ConfigureSwaggerOptions` (ver Paso 4) que usa `IConfigureOptions<SwaggerGenOptions>` e importa `using Microsoft.OpenApi;` (sin `.Models`)
- Esto permite acceder a `OpenApiInfo` correctamente desde una clase con inyección de dependencias

**Error: `NU1605: Detected package downgrade: Microsoft.OpenApi`**
- Ocurre cuando se referencia explícitamente `Microsoft.OpenApi` con una versión inferior a la requerida por Swashbuckle
- **Solución:** No agregar `Microsoft.OpenApi` manualmente al `.csproj`. Swashbuckle lo incluye como dependencia transitiva con la versión correcta
- Ejecutar: `dotnet clean && dotnet restore`

**Error: Conflicto entre `Microsoft.AspNetCore.OpenApi` y `Swashbuckle.AspNetCore`**
- Estos dos paquetes NO son compatibles entre sí
- **Solución:** Remover `Microsoft.AspNetCore.OpenApi` del `.csproj` y `builder.Services.AddOpenApi()` del `Program.cs`
- Usar solamente Swashbuckle para documentación OpenAPI

**Error: "ApiVersion not found"**
- Verificar que los paquetes `Asp.Versioning.Mvc` y `Asp.Versioning.Mvc.ApiExplorer` estén instalados
- Verificar que `AddApiVersioning()` esté configurado en `Program.cs`

**Swagger UI carga pero dice "Failed to load API definition"**
- Verificar que `ConfigureSwaggerOptions` esté registrado: `builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();`
- Verificar que el `GroupNameFormat` sea `"'v'VVV"` (genera "v1", "v2")
- Verificar que `UseSwaggerUI` use `IApiVersionDescriptionProvider` para generar los endpoints dinámicamente
- Abrir directamente `http://localhost:5001/swagger/v1/swagger.json` para diagnosticar

**Swagger solo muestra una versión**
- Verificar que existan controladores con `[ApiVersion("1.0")]` y `[ApiVersion("2.0")]`
- Verificar que ambos controladores tengan la ruta `[Route("api/v{version:apiVersion}/[controller]")]`
- Verificar que `AddApiExplorer()` tenga `SubstituteApiVersionInUrl = true`

**Endpoints no funcionan**
- Verificar rutas: deben incluir `v{version:apiVersion}`
- Verificar atributos `[ApiVersion("1.0")]` en controladores
- La ruta correcta es `/api/v1/Products` (no `/api/Products`)

**Paginación no funciona (v2)**
- Verificar que page y pageSize sean parámetros de query con valores por defecto
- URL de ejemplo: `/api/v2/Products?page=1&pageSize=5`

**"No devuelve nada" o "Connection refused"**
- Verificar el puerto correcto: por defecto es `5001` (no 5000)
- Verificar en `Properties/launchSettings.json` el puerto configurado
- Usar la ruta completa con versión: `/api/v1/Products`
- El nombre del controlador es "Products" con mayúscula P
- Verificar que el servicio esté corriendo: `dotnet run`
- Verificar que el repositorio esté registrado como `AddSingleton` (no `AddScoped`) para que los datos seed persistan

