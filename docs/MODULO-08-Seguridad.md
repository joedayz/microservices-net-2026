# Módulo 8 – Seguridad: JWT Authentication en Microservicios

## 🧠 Teoría

### OAuth2, OIDC y JWT

**OAuth2:**
- Framework de autorización estándar de la industria
- Define flujos (flows) para obtener tokens de acceso
- Separa la autenticación de la autorización
- Scopes definen los permisos del token

**OpenID Connect (OIDC):**
- Extensión de OAuth2 para **autenticación**
- Agrega un `id_token` (JWT) además del `access_token`
- Proporciona endpoint `/userinfo` con datos del usuario
- Es el estándar para "Login con Google/Microsoft/etc."

**JSON Web Token (JWT):**
- Formato compacto y auto-contenido para transmitir claims entre partes
- Estructura: `Header.Payload.Signature` (Base64URL)
- **Header:** algoritmo y tipo (`{"alg": "HS256", "typ": "JWT"}`)
- **Payload:** claims del usuario (`sub`, `name`, `role`, `exp`, etc.)
- **Signature:** garantiza que el token no fue alterado

```
eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJhZG1pbiIsInJvbGUiOiJBZG1pbiJ9.firma_digital
|___ Header ___|.___________ Payload ___________|.___ Signature ___|
```

### Estrategia de Seguridad en Microservicios

```
┌──────────┐    ┌──────────────┐    ┌─────────────────┐
│  Cliente  │───▶│  Auth        │───▶│  Genera JWT     │
│  (SPA)   │    │  Controller  │    │  con roles      │
└──────────┘    └──────────────┘    └─────────────────┘
                                              │
                      Token JWT               │
                ┌─────────────────────────────┘
                ▼
┌──────────────────────────────────────────────────────┐
│              Authorization Header                     │
│         Bearer eyJhbGciOi...                         │
├──────────────────────────────────────────────────────┤
│                                                       │
│  ┌──────────────┐   ┌──────────────┐                 │
│  │ ProductService│   │ OrderService │                 │
│  │              │   │              │                 │
│  │ [Authorize]  │   │ [Authorize]  │                 │
│  │ Valida JWT   │   │ Valida JWT   │                 │
│  │ Verifica rol │   │ Verifica rol │                 │
│  └──────────────┘   └──────────────┘                 │
│                                                       │
│  Misma clave secreta ──▶ Validación independiente    │
└──────────────────────────────────────────────────────┘
```

**Principios aplicados:**
1. **Autenticación centralizada:** Un único endpoint genera los tokens
2. **Validación distribuida:** Cada microservicio valida el JWT independientemente
3. **Zero Trust:** Cada request debe incluir un token válido
4. **Principio de mínimo privilegio:** Roles y políticas granulares

### Azure AD (Producción)

En producción, reemplazaríamos nuestro `AuthController` por Azure AD:
- Autenticación empresarial con SSO
- Multi-factor authentication (MFA)
- Integración con Microsoft 365
- App Registrations para cada microservicio

> **Nota:** En este laboratorio usamos un AuthController local para simplificar.
> En el Módulo 9 (API Gateway) veremos cómo centralizar la autenticación.

---

## 🧪 Laboratorio 8 – Proteger APIs con JWT

### Objetivo

Al finalizar este laboratorio habrás:
1. ✅ Configurado autenticación JWT en ProductService y OrderService
2. ✅ Creado un endpoint de login que genera tokens con roles
3. ✅ Protegido endpoints con `[Authorize]` y políticas por rol
4. ✅ Configurado Swagger UI para enviar tokens JWT
5. ✅ Probado el flujo completo: login → token → acceso protegido

### Arquitectura de seguridad

```
Usuarios de prueba:
┌─────────────────────────────────────────────┐
│  admin  / admin123   → Rol: Admin           │
│  reader / reader123  → Rol: Reader          │
│  user   / user123    → Rol: User            │
└─────────────────────────────────────────────┘

Políticas de autorización:
┌─────────────────────────────────────────────┐
│  AdminOnly  → Solo rol "Admin"              │
│  ReadOnly   → Roles "Admin" o "Reader"      │
└─────────────────────────────────────────────┘

Protección de endpoints:
┌──────────────────────────────────────────────────────┐
│  GET  /api/v1/products        → [AllowAnonymous]     │
│  GET  /api/v1/products/{id}   → [AllowAnonymous]     │
│  POST /api/v1/products        → [Authorize] AdminOnly│
│  PUT  /api/v1/products/{id}   → [Authorize] AdminOnly│
│  DEL  /api/v1/products/{id}   → [Authorize] AdminOnly│
│  GET  /api/v2/products/*      → [Authorize] ReadOnly │
│  GET  /api/v1/config          → [Authorize] AdminOnly│
│                                                       │
│  GET  /api/v1/orders          → [AllowAnonymous]     │
│  GET  /api/v1/orders/{id}     → [Authorize]          │
│  POST /api/v1/orders          → [Authorize] AdminOnly│
│  DEL  /api/v1/orders/{id}     → [Authorize] AdminOnly│
│                                                       │
│  POST /api/auth/login         → Público (genera JWT) │
│  GET  /api/auth/users         → Público (ver users)  │
└──────────────────────────────────────────────────────┘
```

---

### Paso 1 – Agregar paquete NuGet

Instalar el paquete de autenticación JWT en ambos servicios:

```bash
# ProductService
cd src/Services/ProductService
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer

# OrderService
cd ../OrderService
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

Verificar que el `.csproj` de cada servicio incluya:

```xml
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.3" />
```

---

### Paso 2 – Configurar JWT en appsettings.json

#### ProductService – `appsettings.json`

Agregar la sección `Jwt` al archivo de configuración:

```json
{
  "Jwt": {
    "Key": "S3cur3K3y_F0r_D3v3l0pm3nt_Purp0s3s_Only_2025!",
    "Issuer": "microservices-net-2025",
    "Audience": "microservices-api",
    "ExpirationMinutes": 60
  }
}
```

#### OrderService – `appsettings.json`

Agregar la misma sección (misma clave para que ambos servicios validen el mismo token):

```json
{
  "Jwt": {
    "Key": "S3cur3K3y_F0r_D3v3l0pm3nt_Purp0s3s_Only_2025!",
    "Issuer": "microservices-net-2025",
    "Audience": "microservices-api",
    "ExpirationMinutes": 60
  }
}
```

> **⚠️ Importante:** En producción, la clave NUNCA se pone en `appsettings.json`.
> Se usa `dotnet user-secrets`, Azure Key Vault o variables de entorno.

---

### Paso 3 – Registrar Authentication y Authorization en Program.cs

#### ProductService – `Program.cs`

Agregar los **usings** necesarios al inicio del archivo:

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
```

Luego, **después** del bloque de API Versioning y **antes** de Swagger, agregar la configuración JWT:

```csharp
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
        ValidateIssuer = true,           // Valida quién emitió el token
        ValidateAudience = true,         // Valida para quién es el token
        ValidateLifetime = true,         // Valida que no haya expirado
        ValidateIssuerSigningKey = true, // Valida la firma digital
        ValidIssuer = jwtSettings["Issuer"] ?? "microservices-net-2025",
        ValidAudience = jwtSettings["Audience"] ?? "microservices-api",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero       // Sin tolerancia de tiempo
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ReadOnly", policy => policy.RequireRole("Admin", "Reader"));
});
```

**¿Qué hace cada validación?**

| Parámetro | Propósito |
|---|---|
| `ValidateIssuer` | Rechaza tokens emitidos por otro sistema |
| `ValidateAudience` | Rechaza tokens destinados a otra API |
| `ValidateLifetime` | Rechaza tokens expirados |
| `ValidateIssuerSigningKey` | Rechaza tokens con firma inválida |
| `ClockSkew = Zero` | Sin los 5 minutos de tolerancia por defecto |

#### Configurar Swagger con soporte JWT

Reemplazar la configuración simple de Swagger por una que incluya el botón "Authorize":

```csharp
// Swagger / OpenAPI (versioned)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Configurar JWT en Swagger UI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el token JWT. Ejemplo: eyJhbGciOi..."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Name = "Bearer"
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();
```

#### Agregar middleware de Authentication en el pipeline

Buscar la línea `app.UseAuthorization()` y agregar `app.UseAuthentication()` **justo antes**:

```csharp
app.UseHttpsRedirection();
app.UseAuthentication();   // ← NUEVO: Valida el token JWT
app.UseAuthorization();    // ← Ya existía: Verifica roles/políticas
app.MapControllers();
```

> **⚠️ El orden importa:** `UseAuthentication()` SIEMPRE va antes de `UseAuthorization()`.

#### OrderService – `Program.cs`

Aplicar exactamente la misma configuración. Agregar los usings:

```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
```

Y registrar Authentication + Authorization después de `AddSingleton<IOrderRepository>`:

```csharp
builder.Services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

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
```

Y en el pipeline HTTP:

```csharp
app.UseHttpsRedirection();
app.UseAuthentication();   // ← NUEVO
app.UseAuthorization();
app.MapControllers();
```

---

### Paso 4 – Crear los DTOs de autenticación

#### ProductService – `DTOs/LoginDto.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace ProductService.DTOs;

public class LoginDto
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class TokenResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
```

#### OrderService – `DTOs/LoginDto.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace OrderService.DTOs;

public class LoginDto
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class TokenResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
```

---

### Paso 5 – Crear el AuthController

Este controller simula un Identity Provider (IdP) para el laboratorio. En producción,
esto lo haría Azure AD, Keycloak, Auth0, etc.

#### ProductService – `Controllers/AuthController.cs`

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using ProductService.DTOs;

namespace ProductService.Controllers;

/// <summary>
/// Controller para autenticación (desarrollo/laboratorio).
/// En producción se usaría Azure AD, Keycloak u otro IdP.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    // Usuarios simulados para el laboratorio
    private static readonly Dictionary<string, (string Password, string Role)> Users = new()
    {
        ["admin"] = ("admin123", "Admin"),
        ["reader"] = ("reader123", "Reader"),
        ["user"] = ("user123", "User")
    };

    public AuthController(IConfiguration configuration, ILogger<AuthController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Genera un JWT token para el usuario autenticado.
    /// Usuarios disponibles: admin/admin123, reader/reader123, user/user123
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Login([FromBody] LoginDto loginDto)
    {
        // Validar credenciales simuladas
        if (!Users.TryGetValue(loginDto.Username.ToLower(), out var userData) ||
            userData.Password != loginDto.Password)
        {
            _logger.LogWarning("Login fallido para usuario: {Username}", loginDto.Username);
            return Unauthorized(new { Message = "Credenciales inválidas" });
        }

        var (_, role) = userData;
        var token = GenerateJwtToken(loginDto.Username, role);
        var expiration = DateTime.UtcNow.AddMinutes(
            _configuration.GetValue<int>("Jwt:ExpirationMinutes", 60));

        _logger.LogInformation("Login exitoso: {Username} con rol {Role}",
            loginDto.Username, role);

        return Ok(new TokenResponseDto
        {
            Token = token,
            Expiration = expiration,
            Username = loginDto.Username,
            Role = role
        });
    }

    /// <summary>
    /// Muestra los usuarios disponibles para pruebas.
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetTestUsers()
    {
        var users = Users.Select(u => new
        {
            Username = u.Key,
            Password = u.Value.Password,
            Role = u.Value.Role
        });

        return Ok(users);
    }

    private string GenerateJwtToken(string username, string role)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = jwtSettings["Key"]
                  ?? "S3cur3K3y_F0r_D3v3l0pm3nt_Purp0s3s_Only_2025!";
        var issuer = jwtSettings["Issuer"] ?? "microservices-net-2025";
        var audience = jwtSettings["Audience"] ?? "microservices-api";
        var expirationMinutes = _configuration.GetValue<int>(
            "Jwt:ExpirationMinutes", 60);

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(
            securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)  // ← Clave para [Authorize(Roles)]
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

**Anatomía del token generado:**

```json
// Header
{
  "alg": "HS256",
  "typ": "JWT"
}

// Payload (claims)
{
  "sub": "admin",
  "jti": "a1b2c3d4-...",
  "iat": 1709337600,
  "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": "admin",
  "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "Admin",
  "exp": 1709341200,
  "iss": "microservices-net-2025",
  "aud": "microservices-api"
}
```

#### OrderService – `Controllers/AuthController.cs`

Crear el mismo controller pero con el namespace `OrderService.Controllers` y usando `OrderService.DTOs`.

> **💡 Tip:** El código es idéntico al de ProductService, solo cambian los namespaces.
> Ambos servicios comparten la misma clave JWT, así que un token generado en uno
> es válido en el otro.

---

### Paso 6 – Proteger los Controllers con [Authorize]

#### ProductService – V1 ProductsController

Agregar `using Microsoft.AspNetCore.Authorization;` y los atributos:

```csharp
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.DTOs;
using ProductService.Application.Services;

namespace ProductService.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]                                          // ← Protege TODO el controller
public class ProductsController: ControllerBase
{
    // ... constructor sin cambios ...

    [HttpGet]
    [AllowAnonymous]                                 // ← Lectura pública
    [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll(...)

    [HttpGet("{id}")]
    [AllowAnonymous]                                 // ← Lectura pública
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductDto>> GetById(...)

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]                // ← Solo Admin puede crear
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProductDto>> Create(...)

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]                // ← Solo Admin puede editar
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(...)

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]                // ← Solo Admin puede eliminar
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(...)
}
```

#### ProductService – V2 ProductsController

```csharp
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = "ReadOnly")]                     // ← Admin o Reader
public class ProductsController : ControllerBase
```

#### ProductService – ConfigController

```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize(Policy = "AdminOnly")]                    // ← Solo Admin ve la config
public class ConfigController: ControllerBase
```

#### OrderService – OrdersController

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Clients;
using OrderService.Domain;
using OrderService.DTOs;

namespace OrderService.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]                                          // ← Protege TODO el controller
public class OrdersController : ControllerBase
{
    // ... constructor sin cambios ...

    [HttpGet]
    [AllowAnonymous]                                 // ← Listar órdenes es público
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAll(...)

    [HttpGet("{id}")]                                // ← Requiere autenticación
    public async Task<ActionResult<OrderDto>> GetById(...)

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]                // ← Solo Admin crea órdenes
    public async Task<ActionResult<OrderDto>> Create(...)

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]                // ← Solo Admin elimina
    public async Task<IActionResult> Delete(...)
}
```

**Resumen de atributos:**

| Atributo | Efecto |
|---|---|
| `[Authorize]` (en clase) | Todos los endpoints requieren token válido |
| `[AllowAnonymous]` | Excepciona un endpoint del `[Authorize]` de clase |
| `[Authorize(Policy = "AdminOnly")]` | Requiere token con `role: Admin` |
| `[Authorize(Policy = "ReadOnly")]` | Requiere token con `role: Admin` o `Reader` |

---

### Paso 7 – Verificar la compilación

```bash
cd src/Services/ProductService
dotnet build

cd ../OrderService
dotnet build
```

Ambos servicios deben compilar sin errores nuevos.

---

### Paso 8 – Probar el flujo completo

#### 8.1 – Levantar ProductService

```bash
cd src/Services/ProductService
dotnet run
```

#### 8.2 – Ver usuarios disponibles

```bash
curl http://localhost:5001/api/auth/users | jq
```

Respuesta:
```json
[
  { "username": "admin",  "password": "admin123",  "role": "Admin"  },
  { "username": "reader", "password": "reader123", "role": "Reader" },
  { "username": "user",   "password": "user123",   "role": "User"   }
]
```

#### 8.3 – Probar acceso SIN token (endpoints públicos)

```bash
# ✅ GET productos es público
curl http://localhost:5001/api/v1/products | jq

# ✅ GET producto por ID es público
curl http://localhost:5001/api/v1/products/{id} | jq
```

#### 8.4 – Probar acceso SIN token (endpoints protegidos)

```bash
# ❌ POST producto sin token → 401 Unauthorized
curl -X POST http://localhost:5001/api/v1/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Test","description":"Test","price":10,"stock":5}'
```

Respuesta esperada: **401 Unauthorized**

#### 8.5 – Obtener un token JWT (Login)

```bash
# Login como admin
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' | jq
```

Respuesta:
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiration": "2025-03-02T20:00:00Z",
  "username": "admin",
  "role": "Admin"
}
```

> **💡 Tip:** Copia el valor de `token` y pégalo en [jwt.io](https://jwt.io) para ver los claims decodificados.

#### 8.6 – Acceder con token (Admin)

```bash
# Guardar token en variable
TOKEN=$(curl -s -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' | jq -r '.token')

# ✅ POST producto con token Admin → 201 Created
curl -X POST http://localhost:5001/api/v1/products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"name":"Tablet","description":"Nueva tablet","price":499.99,"stock":15}' | jq

# ✅ DELETE producto con token Admin → 204
curl -X DELETE http://localhost:5001/api/v1/products/{id} \
  -H "Authorization: Bearer $TOKEN"

# ✅ GET config con token Admin → 200
curl http://localhost:5001/api/v1/config \
  -H "Authorization: Bearer $TOKEN" | jq
```

#### 8.7 – Probar con rol Reader

```bash
# Login como reader
TOKEN_READER=$(curl -s -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"reader","password":"reader123"}' | jq -r '.token')

# ✅ GET v2 products (ReadOnly policy) → 200
curl http://localhost:5001/api/v2/products \
  -H "Authorization: Bearer $TOKEN_READER" | jq

# ❌ POST producto con Reader → 403 Forbidden
curl -X POST http://localhost:5001/api/v1/products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN_READER" \
  -d '{"name":"Test","description":"Test","price":10,"stock":5}'
```

Respuesta esperada: **403 Forbidden** (autenticado pero sin permisos)

#### 8.8 – Probar con rol User (sin políticas)

```bash
TOKEN_USER=$(curl -s -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"user","password":"user123"}' | jq -r '.token')

# ❌ GET v2 products (necesita ReadOnly) → 403 Forbidden
curl http://localhost:5001/api/v2/products \
  -H "Authorization: Bearer $TOKEN_USER"

# ❌ POST producto (necesita AdminOnly) → 403 Forbidden
curl -X POST http://localhost:5001/api/v1/products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN_USER" \
  -d '{"name":"Test","description":"Test","price":10,"stock":5}'
```

#### 8.9 – Token compartido entre servicios

El mismo token funciona en OrderService (misma clave JWT):

```bash
# Login en ProductService
TOKEN=$(curl -s -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' | jq -r '.token')

# ✅ Usar ese token en OrderService → funciona!
curl http://localhost:5003/api/v1/orders \
  -H "Authorization: Bearer $TOKEN" | jq
```

---

### Paso 9 – Probar desde Swagger UI

1. Abrir `http://localhost:5001` en el navegador (Swagger UI)
2. Hacer clic en el botón **🔓 Authorize** (arriba a la derecha)
3. En el campo "Value", pegar el token JWT (sin el prefijo "Bearer")
4. Hacer clic en **Authorize** → **Close**
5. Ahora las peticiones desde Swagger incluirán el header `Authorization: Bearer <token>`
6. Probar los endpoints protegidos

---

### Paso 10 – Credenciales inválidas

```bash
# ❌ Password incorrecto → 401
curl -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"wrong"}'
```

Respuesta:
```json
{
  "message": "Credenciales inválidas"
}
```

---

## 📊 Matriz de respuestas HTTP

| Escenario | HTTP Status | Significado |
|---|---|---|
| Sin token → endpoint público | **200 OK** | Acceso permitido |
| Sin token → endpoint protegido | **401 Unauthorized** | No autenticado |
| Token válido + rol correcto | **200/201/204** | Acceso permitido |
| Token válido + rol incorrecto | **403 Forbidden** | Autenticado pero sin permisos |
| Token expirado | **401 Unauthorized** | Token ya no es válido |
| Token con firma inválida | **401 Unauthorized** | Token alterado/falso |

---

## 🔑 Conceptos clave

### Diferencia entre 401 y 403

```
401 Unauthorized  = "No sé quién eres"  (falta token o token inválido)
403 Forbidden     = "Sé quién eres, pero no tienes permiso" (token válido, rol incorrecto)
```

### ¿Por qué ClockSkew = Zero?

Por defecto, .NET permite 5 minutos de tolerancia en la expiración del token (para compensar
diferencias de reloj entre servidores). Con `TimeSpan.Zero`, el token expira exactamente
cuando dice el claim `exp`.

### Token compartido entre servicios

Como ambos servicios usan la **misma clave secreta**, un token generado por ProductService
es válido en OrderService y viceversa. Esto es una decisión de diseño:

- **Clave compartida** → más simple, un login sirve para todo
- **Claves separadas** → más seguro, cada servicio tiene su propio scope

En producción con Azure AD, cada servicio tendría su propio App Registration con scopes específicos.

---

## ☁️ Laboratorio 8B – Integración con Azure AD (Microsoft Entra ID)

Ahora vamos a conectar nuestros microservicios con **Azure AD** real para que los tokens
sean emitidos por Microsoft. Esto es lo que usarías en producción.

### Arquitectura con Azure AD

```
┌──────────┐     ┌──────────────────────┐     ┌───────────────────────┐
│  Cliente  │────▶│  Microsoft Entra ID  │────▶│  Token JWT firmado    │
│  (Postman │     │  (Azure AD)          │     │  por Azure AD         │
│   / SPA)  │     │                      │     │                       │
└──────────┘     │  1. App Registration  │     │  iss: login.microsoft │
                  │  2. Client Credentials│     │  aud: api://{client}  │
                  │  3. Scopes / Roles    │     │  roles: ["Admin"]     │
                  └──────────────────────┘     └───────────┬───────────┘
                                                            │
                              Bearer Token                  │
                  ┌─────────────────────────────────────────┘
                  ▼
┌──────────────────────────────────────────────────────────────────┐
│                    Microservicios                                 │
│                                                                   │
│  ┌──────────────────┐          ┌──────────────────┐              │
│  │  ProductService   │          │  OrderService    │              │
│  │                   │          │                  │              │
│  │  Authority:       │          │  Authority:      │              │
│  │  login.microsoft  │          │  login.microsoft │              │
│  │  .com/{tenant}    │          │  .com/{tenant}   │              │
│  │                   │          │                  │              │
│  │  Valida firma con │          │  Valida firma con│              │
│  │  claves públicas  │          │  claves públicas │              │
│  │  de Azure AD      │          │  de Azure AD     │              │
│  └──────────────────┘          └──────────────────┘              │
└──────────────────────────────────────────────────────────────────┘
```

> **Diferencia clave:** Con Azure AD no necesitamos gestionar claves secretas.
> Azure AD firma con RS256 (clave asimétrica) y publica las claves públicas
> en un endpoint JWKS. Nuestros servicios las descargan automáticamente.

---

### Paso B1 – Crear el App Registration en Azure Portal

1. Ir a [portal.azure.com](https://portal.azure.com)
2. Buscar **"Microsoft Entra ID"** (antes Azure Active Directory)
3. En el menú lateral, ir a **App registrations** → **+ New registration**

```
Configurar:
┌──────────────────────────────────────────────────────────────┐
│  Name:                    microservices-api                   │
│  Supported account types: Single tenant                      │
│  Redirect URI:            (dejar vacío por ahora)            │
└──────────────────────────────────────────────────────────────┘
```

4. Hacer clic en **Register**
5. Anotar los valores que aparecen:

```
┌──────────────────────────────────────────────────────────────┐
│  Application (client) ID:   xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxx │  ← AUDIENCE
│  Directory (tenant) ID:     yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyy │  ← TENANT
└──────────────────────────────────────────────────────────────┘
```

---

### Paso B2 – Exponer la API (definir scopes)

1. En el App Registration, ir a **Expose an API**
2. Hacer clic en **+ Add a scope**
3. Si pide un **Application ID URI**, aceptar el valor por defecto (`api://{client-id}`) → **Save and continue**

```
Configurar el scope:
┌──────────────────────────────────────────────────────────────┐
│  Scope name:             access_as_user                      │
│  Who can consent?:       Admins and users                    │
│  Admin consent display:  Access Microservices API            │
│  Admin consent desc:     Allow the app to access the API     │
│  User consent display:   Access Microservices API            │
│  User consent desc:      Allow access to Microservices API   │
│  State:                  Enabled                             │
└──────────────────────────────────────────────────────────────┘
```

4. Hacer clic en **Add scope**

---

### Paso B3 – Definir App Roles (Admin, Reader)

1. En el App Registration, ir a **App roles** → **+ Create app role**

**Rol Admin:**
```
┌──────────────────────────────────────────────────────────────┐
│  Display name:           Admin                               │
│  Allowed member types:   Both (Users/Groups + Applications)  │
│  Value:                  Admin                               │
│  Description:            Full access to manage resources     │
│  Enable this role:       ✅                                  │
└──────────────────────────────────────────────────────────────┘
```

**Rol Reader:**
```
┌──────────────────────────────────────────────────────────────┐
│  Display name:           Reader                              │
│  Allowed member types:   Both (Users/Groups + Applications)  │
│  Value:                  Reader                              │
│  Description:            Read-only access to resources       │
│  Enable this role:       ✅                                  │
└──────────────────────────────────────────────────────────────┘
```

> **⚠️ Importante:** Seleccionar **"Both (Users/Groups + Applications)"** en Allowed member types.
> Si solo seleccionas "Users/Groups", el flujo `client_credentials` no recibirá roles en el token.

2. Hacer clic en **Apply** para cada uno

---

### Paso B4 – Asignar roles a usuarios y aplicaciones

#### A) Asignar rol a un usuario (para tokens con `az cli` o Authorization Code Flow)

1. Ir a **Microsoft Entra ID** → **Enterprise applications**
2. Buscar y seleccionar **microservices-api**
3. Ir a **Users and groups** → **+ Add user/group**
4. Seleccionar un usuario y asignarle el rol **Admin** o **Reader**

> **💡 Tip:** Si no tienes usuarios de prueba, crea uno en Entra ID → Users → + New user.

#### B) Asignar rol a la app cliente (para tokens con `client_credentials` flow)

Para que el token de `client_credentials` incluya el claim `roles`, hay que asignar
el App Role a la **aplicación cliente** (no a un usuario):

1. Ir a **Microsoft Entra ID** → **Enterprise applications**
2. Buscar y seleccionar **microservices-client** (la app cliente, no la API)
3. Ir a **Users and groups** → **+ Add user/group**
4. En "Select role", elegir **Admin** (para pruebas con permisos completos)
5. Hacer clic en **Assign**

> **⚠️ Para probar con rol Reader:** Repetir el proceso creando un **segundo Client Secret**
> en microservices-client, o bien crear una tercera App Registration
> (ej: `microservices-reader-client`) y asignarle el rol **Reader**.
> Cada app cliente solo puede tener un rol asignado en Enterprise Applications.
>
> **Alternativa más simple:** Usar `az cli` con un usuario que tenga rol Reader
> asignado (ver sección A arriba).

---

### Paso B5 – Crear un Client App (para obtener tokens)

Necesitamos una segunda App Registration que actúe como "cliente" (simula Postman/SPA):

1. **App registrations** → **+ New registration**

```
┌──────────────────────────────────────────────────────────────┐
│  Name:                    microservices-client                │
│  Supported account types: Single tenant                      │
│  Redirect URI:            Web → https://oauth.pstmn.io/v1/  │
│                           callback (para Postman)            │
└──────────────────────────────────────────────────────────────┘
```

2. Ir a **Certificates & secrets** → **+ New client secret**
   - Description: `dev-secret`
   - Expires: 6 months
   - **Copiar el Value** (solo se muestra una vez)

3. Ir a **API permissions** → **+ Add a permission**
   - Seleccionar **My APIs** → **microservices-api**
   - Marcar el scope `access_as_user`
   - Hacer clic en **Add permissions**
   - Hacer clic en **Grant admin consent for {tenant}**

Anotar los valores:
```
┌────────────────────────────────────────────────────────────────────────┐
│  microservices-client:                                                │
│    Client App ID:     cccccccc-cccc-cccc-cccc-ccccccccccccc           │
│    Client Secret:     xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx        │
│                       (⚠️ copiar el Value, NO el Secret ID)          │
│                                                                       │
│  microservices-api (del Paso B1):                                     │
│    API Client ID:     bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb           │
│    API App ID URI:    api://bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb     │
│                                                                       │
│  Tenant ID:           yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyyyyy        │
└────────────────────────────────────────────────────────────────────────┘

⚠️ En el Paso B7, CLIENT_ID/SECRET son de microservices-client,
pero API_SCOPE usa el Client ID de microservices-api. ¡No confundir!
```

---

### Paso B6 – Configurar ProductService para Azure AD

#### `appsettings.json` – Agregar sección AzureAd

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<TU-TENANT-ID>",
    "ClientId": "<CLIENT-ID-de-microservices-api>",
    "Audience": "api://<CLIENT-ID-de-microservices-api>"
  }
}
```

> **⚠️ Nota:** `ClientId` y `Audience` usan el Application ID de **microservices-api**
> (el App Registration del Paso B1), no el de microservices-client.

#### `Program.cs` – Configurar doble esquema (Local + Azure AD)

Reemplazar la sección de JWT Authentication para soportar **ambos modos**:

```csharp
// ============================
// JWT Authentication
// ============================
var authProvider = builder.Configuration.GetValue<string>("Auth:Provider") ?? "local";

if (authProvider.Equals("azuread", StringComparison.OrdinalIgnoreCase))
{
    // ── Azure AD / Microsoft Entra ID ──
    var azureAdConfig = builder.Configuration.GetSection("AzureAd");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // Authority v1 (sin /v2.0) para aceptar tokens tanto de az cli (v1) como de client_credentials (v2)
            options.Authority = $"{azureAdConfig["Instance"]}{azureAdConfig["TenantId"]}";
            options.Audience = azureAdConfig["Audience"];
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                // Aceptar issuers de ambas versiones de tokens
                ValidIssuers = new[]
                {
                    $"{azureAdConfig["Instance"]}{azureAdConfig["TenantId"]}/v2.0",
                    $"https://sts.windows.net/{azureAdConfig["TenantId"]}/"
                },
                // Aceptar audience como URI o como Client ID
                ValidAudiences = new[]
                {
                    azureAdConfig["Audience"],
                    azureAdConfig["ClientId"]
                },
                // ClaimTypes.Role mapea el claim "roles" del token al formato URI
                // que .NET usa internamente para [Authorize(Roles = "Admin")]
                RoleClaimType = System.Security.Claims.ClaimTypes.Role
            };
        });
}
else
{
    // ── JWT Local (desarrollo/laboratorio) ──
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ReadOnly", policy => policy.RequireRole("Admin", "Reader"));
});
```

> **Detalles de configuración:**
>
> | Configuración | Por qué |
> |---|---|
> | `Authority` sin `/v2.0` | La metadata v1 acepta tokens v1 (`az cli`) y v2 (`client_credentials`) |
> | `ValidIssuers` (array) | Tokens v1 usan `sts.windows.net`, tokens v2 usan `login.microsoftonline.com/…/v2.0` |
> | `ValidAudiences` (array) | Tokens v1 envían el `ClientId` como audience, tokens v2 envían `api://ClientId` |
> | `ClaimTypes.Role` | Funciona con ambos: Azure AD mapea `roles` → URI interno de .NET automáticamente |

#### `appsettings.json` – Selector de proveedor

```json
{
  "Auth": {
    "Provider": "local"
  }
}
```

Cambiar a `"azuread"` cuando quieras usar Azure AD:

```json
{
  "Auth": {
    "Provider": "azuread"
  }
}
```

#### `appsettings.Development.json` – Forzar modo local

```json
{
  "Auth": {
    "Provider": "local"
  }
}
```

Así, en Development usas JWT local y en Production usas Azure AD sin cambiar código.

---

### Paso B7 – Obtener un token de Azure AD

#### Opción A: Con `curl` (Client Credentials Flow)

Para obtener un token de aplicación (sin usuario interactivo):

```bash
# ⚠️ CLIENT_ID y CLIENT_SECRET son de microservices-client (quien pide el token)
# ⚠️ API_SCOPE usa el ID de microservices-api (el recurso protegido)
TENANT_ID="<TU-TENANT-ID>"
CLIENT_ID="<CLIENT-APP-ID-de-microservices-client>"
CLIENT_SECRET="<CLIENT-SECRET-Value-de-microservices-client>"
API_SCOPE="api://<CLIENT-ID-de-microservices-api>/.default"

curl -X POST "https://login.microsoftonline.com/$TENANT_ID/oauth2/v2.0/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials" \
  -d "client_id=$CLIENT_ID" \
  -d "client_secret=$CLIENT_SECRET" \
  -d "scope=$API_SCOPE" | jq
```

Respuesta:
```json
{
  "token_type": "Bearer",
  "expires_in": 3599,
  "access_token": "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsIng1dCI6..."
}
```

#### Opción B: Con `az cli` (usuario interactivo)

```bash
# Login interactivo
az login

# Obtener token para la API (usar el Client ID de microservices-api)
az account get-access-token \
  --resource "api://<CLIENT-ID-de-microservices-api>" \
  --query accessToken -o tsv
```

#### Opción C: Con Postman (Authorization Code Flow)

1. En Postman, ir a la pestaña **Authorization**
2. Type: **OAuth 2.0**
3. Configurar:

```
┌──────────────────────────────────────────────────────────────┐
│  Grant Type:    Authorization Code                           │
│  Auth URL:      https://login.microsoftonline.com/           │
│                 {tenant-id}/oauth2/v2.0/authorize            │
│  Token URL:     https://login.microsoftonline.com/           │
│                 {tenant-id}/oauth2/v2.0/token                │
│  Client ID:     {microservices-client App ID}                │
│  Client Secret: {microservices-client Secret Value}          │
│  Scope:         api://{microservices-api App ID}             │
│                 /access_as_user                              │
│  Callback URL:  https://oauth.pstmn.io/v1/callback          │
└──────────────────────────────────────────────────────────────┘
```

4. Hacer clic en **Get New Access Token**
5. Login con tu usuario de Azure AD
6. Copiar el token → usarlo en las peticiones

---

### Paso B8 – Probar endpoints con token de Azure AD

#### 8.1 – Probar con rol Admin (client_credentials)

> **Prerequisito:** Haber asignado el rol Admin a microservices-client en el Paso B4-B.

```bash
# Variables (ajustar con tus valores)
TENANT_ID="<TU-TENANT-ID>"
CLIENT_ID="<CLIENT-APP-ID-de-microservices-client>"
CLIENT_SECRET="<CLIENT-SECRET-Value-de-microservices-client>"
API_SCOPE="api://<CLIENT-ID-de-microservices-api>/.default"

# Obtener token con rol Admin
TOKEN=$(curl -s -X POST \
  "https://login.microsoftonline.com/$TENANT_ID/oauth2/v2.0/token" \
  -d "grant_type=client_credentials&client_id=$CLIENT_ID&client_secret=$CLIENT_SECRET&scope=$API_SCOPE" \
  | jq -r '.access_token')

# Verificar que el token tiene el rol Admin
echo $TOKEN | cut -d'.' -f2 | base64 -d 2>/dev/null | jq '.roles'
# Esperado: ["Admin"]

# ✅ GET productos (público, no requiere token)
curl http://localhost:5001/api/v1/products | jq

# ✅ POST producto con token Admin → 201 Created
curl -X POST http://localhost:5001/api/v1/products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"name":"Cloud Product","description":"Desde Azure AD","price":99.99,"stock":10}' | jq

# ✅ GET v2 products (ReadOnly policy: Admin cumple) → 200 OK
curl http://localhost:5001/api/v2/products \
  -H "Authorization: Bearer $TOKEN" | jq

# ✅ Mismo token en OrderService → 201 Created
curl -X POST http://localhost:5003/api/v1/orders \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"customerName":"Azure User","items":[{"productId":"...","quantity":1}]}' | jq
```

#### 8.2 – Probar con rol Reader (az cli + usuario)

Para probar el rol Reader, la forma más simple es usar `az cli` con un **usuario**
que tenga el rol Reader asignado (Paso B4-A):

```bash
# Login con el usuario que tiene rol Reader
az login --tenant $TENANT_ID

# Obtener token (token delegado, incluye roles del usuario)
TOKEN_READER=$(az account get-access-token \
  --resource "api://<CLIENT-ID-de-microservices-api>" \
  --query accessToken -o tsv)

# ✅ GET v2 products (ReadOnly policy) → 200 OK
curl http://localhost:5001/api/v2/products \
  -H "Authorization: Bearer $TOKEN_READER" | jq

# ❌ POST producto (AdminOnly policy) → 403 Forbidden
curl -X POST http://localhost:5001/api/v1/products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN_READER" \
  -d '{"name":"Test","description":"Test","price":10,"stock":5}'
# Esperado: 403 Forbidden (autenticado pero sin permisos de Admin)

# ❌ DELETE producto (AdminOnly policy) → 403 Forbidden
curl -X DELETE http://localhost:5001/api/v1/products/{id} \
  -H "Authorization: Bearer $TOKEN_READER"
# Esperado: 403 Forbidden
```

#### 8.3 – Probar sin token (endpoints protegidos)

```bash
# ❌ POST producto sin token → 401 Unauthorized
curl -X POST http://localhost:5001/api/v1/products \
  -H "Content-Type: application/json" \
  -d '{"name":"Test","description":"Test","price":10,"stock":5}'
# Esperado: 401 Unauthorized

# ❌ GET v2 products sin token → 401 Unauthorized
curl http://localhost:5001/api/v2/products
# Esperado: 401 Unauthorized
```

#### 8.4 – Matriz de pruebas Azure AD

| Endpoint | Sin token | Token Admin | Token Reader |
|---|---|---|---|
| `GET /api/v1/products` | ✅ 200 | ✅ 200 | ✅ 200 |
| `POST /api/v1/products` | ❌ 401 | ✅ 201 | ❌ 403 |
| `PUT /api/v1/products/{id}` | ❌ 401 | ✅ 204 | ❌ 403 |
| `DELETE /api/v1/products/{id}` | ❌ 401 | ✅ 204 | ❌ 403 |
| `GET /api/v2/products` | ❌ 401 | ✅ 200 | ✅ 200 |
| `GET /api/v1/config` | ❌ 401 | ✅ 200 | ❌ 403 |
| `GET /api/v1/orders` | ✅ 200 | ✅ 200 | ✅ 200 |
| `POST /api/v1/orders` | ❌ 401 | ✅ 201 | ❌ 403 |

---

### Paso B9 – Decodificar el token de Azure AD

Copiar el token y pegarlo en [jwt.ms](https://jwt.ms) (herramienta oficial de Microsoft).

**Token de aplicación** (client_credentials flow):

```json
{
  "aud": "api://bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  "iss": "https://login.microsoftonline.com/{tenant-id}/v2.0",
  "iat": 1709337600,
  "exp": 1709341200,
  "azp": "cccccccc-cccc-cccc-cccc-cccccccccccc",
  "roles": [
    "Admin"
  ],
  "sub": "...",
  "oid": "...",
  "tid": "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy",
  "ver": "2.0"
}
```

> **Nota:** Los tokens `client_credentials` NO tienen `preferred_username` ni `name`
> porque representan una **aplicación**, no un usuario.

**Token de usuario** (az cli / Authorization Code flow):

```json
{
  "aud": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
  "iss": "https://sts.windows.net/{tenant-id}/",
  "iat": 1709337600,
  "exp": 1709341200,
  "roles": [
    "Admin"
  ],
  "upn": "admin@tudominio.onmicrosoft.com",
  "name": "Admin User",
  "tid": "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy",
  "ver": "1.0"
}
```

**Diferencias entre los tres tipos de token:**

| Campo | JWT Local | Azure AD v1 (az cli) | Azure AD v2 (client_credentials) |
|---|---|---|---|
| `iss` | `microservices-net-2025` | `https://sts.windows.net/{tenant}/` | `https://login.microsoftonline.com/{tenant}/v2.0` |
| `aud` | `microservices-api` | `{client-id}` (GUID) | `api://{client-id}` (URI) |
| `ver` | N/A | `1.0` | `2.0` |
| Roles | `http://schemas.microsoft.com/.../role` | `roles` (array) | `roles` (array) |
| Usuario | `sub: admin` | `upn: user@domain.com` | No tiene (es app, no usuario) |
| Firma | HS256 (clave simétrica) | RS256 (clave asimétrica) | RS256 (clave asimétrica) |
| Claves | Compartida en appsettings | Publicadas en JWKS endpoint | Publicadas en JWKS endpoint |

> **Por eso** el `Program.cs` configura `ValidIssuers` y `ValidAudiences` como arrays:
> para aceptar ambas versiones de tokens sin rechazar ninguna.

---

### Paso B10 – Variables de entorno para producción

En lugar de poner secretos en `appsettings.json`, usar variables de entorno:

```bash
# En tu terminal o en el pipeline de CI/CD
export Auth__Provider="azuread"
export AzureAd__Instance="https://login.microsoftonline.com/"
export AzureAd__TenantId="yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyyyyy"
export AzureAd__ClientId="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
export AzureAd__Audience="api://xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"

dotnet run
```

O con **User Secrets** para desarrollo:

```bash
# ProductService (usar Client ID de microservices-api)
cd src/Services/ProductService
dotnet user-secrets init
dotnet user-secrets set "Auth:Provider" "azuread"
dotnet user-secrets set "AzureAd:TenantId" "<tu-tenant-id>"
dotnet user-secrets set "AzureAd:ClientId" "<CLIENT-ID-de-microservices-api>"
dotnet user-secrets set "AzureAd:Audience" "api://<CLIENT-ID-de-microservices-api>"

# OrderService (mismos valores)
cd ../OrderService
dotnet user-secrets init
dotnet user-secrets set "Auth:Provider" "azuread"
dotnet user-secrets set "AzureAd:TenantId" "<tu-tenant-id>"
dotnet user-secrets set "AzureAd:ClientId" "<CLIENT-ID-de-microservices-api>"
dotnet user-secrets set "AzureAd:Audience" "api://<CLIENT-ID-de-microservices-api>"
```

En `appsettings.json` dejar placeholders para que los valores reales nunca se commiteen:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<TU-TENANT-ID>",
    "ClientId": "<CLIENT-ID-de-microservices-api>",
    "Audience": "api://<CLIENT-ID-de-microservices-api>"
  }
}
```

> **🔒 Regla de oro:** NUNCA commitear `TenantId`, `ClientId` o `ClientSecret` en el repositorio.

---

### Resumen: Local vs Azure AD

| Aspecto | JWT Local | Azure AD |
|---|---|---|
| **Uso** | Desarrollo, laboratorio | Staging, producción |
| **Configuración** | `Auth:Provider = "local"` | `Auth:Provider = "azuread"` |
| **Quién emite tokens** | Nuestro `AuthController` | Microsoft Entra ID |
| **Algoritmo firma** | HS256 (simétrico) | RS256 (asimétrico) |
| **Gestión de claves** | Manual (appsettings) | Automática (JWKS) |
| **Usuarios** | Hardcodeados | Azure AD Users |
| **Roles** | En código | App Roles en Azure |
| **MFA** | No | Sí (configurable) |
| **SSO** | No | Sí |
| **Costo** | Gratis | Gratis (tier básico) |

---

## 🏗️ Estructura final de archivos modificados/creados

```
src/Services/
├── ProductService/
│   ├── appsettings.json              ← +Jwt section, +AzureAd section, +Auth:Provider
│   ├── appsettings.Development.json  ← +Auth:Provider = "local"
│   ├── Program.cs                     ← +Authentication (dual: local/azuread), +Swagger JWT
│   ├── DTOs/
│   │   └── LoginDto.cs               ← NUEVO
│   └── Controllers/
│       ├── AuthController.cs          ← NUEVO (genera tokens locales)
│       ├── V1/
│       │   ├── ProductsV1Controller.cs ← +[Authorize], +[AllowAnonymous]
│       │   └── ConfigController.cs     ← +[Authorize(Policy = "AdminOnly")]
│       └── V2/
│           └── ProductsV2Controller.cs ← +[Authorize(Policy = "ReadOnly")]
│
└── OrderService/
    ├── appsettings.json               ← +Jwt section, +AzureAd section, +Auth:Provider
    ├── appsettings.Development.json   ← +Auth:Provider = "local"
    ├── Program.cs                      ← +Authentication (dual: local/azuread)
    ├── DTOs/
    │   └── LoginDto.cs                ← NUEVO
    └── Controllers/
        ├── AuthController.cs           ← NUEVO (genera tokens locales)
        └── OrdersController.cs         ← +[Authorize], +[AllowAnonymous]
```

---

## 🚀 Próximos pasos

- **Módulo 9 – API Gateway:** Centralizar la validación de tokens en YARP con Azure AD
- **Azure AD avanzado:** Conditional Access, grupos anidados, custom claims
- **Mejoras:** Refresh tokens, token revocation, rate limiting por usuario

