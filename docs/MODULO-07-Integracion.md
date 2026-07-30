# Módulo 7 – Integración entre microservicios

## 🧠 Teoría

### Comunicación Síncrona vs Asíncrona

En una arquitectura de microservicios, los servicios necesitan comunicarse entre sí. Existen dos patrones fundamentales:

**Comunicación Síncrona (Request/Response):**
- El servicio emisor espera la respuesta
- Acoplamiento temporal (ambos servicios deben estar disponibles)
- Más simple de implementar y depurar
- Protocolos: REST (HTTP/JSON), gRPC (HTTP/2 + Protobuf)

**Comunicación Asíncrona (Event-Driven):**
- El servicio emisor no espera respuesta
- Desacoplamiento temporal (el receptor puede estar caído)
- Mayor resiliencia y escalabilidad
- Protocolos: Message Queues (RabbitMQ, Azure Service Bus), Event Streaming (Kafka, Event Hub)

```
┌──────────────┐  REST/gRPC (síncrono)  ┌──────────────┐
│ OrderService │ ────────────────────► │ProductService│
└──────────────┘                        └──────────────┘
       │                                       │
       │ Publish Event                         │ Publish Event
       ▼                                       ▼
┌─────────────────────────────────────────────────────┐
│              Message Broker (RabbitMQ)               │
│              (comunicación asíncrona)                │
└─────────────────────────────────────────────────────┘
       │                          │
       ▼                          ▼
┌──────────────┐          ┌──────────────┐
│NotifyService │          │ Analytics    │
└──────────────┘          └──────────────┘
```

### REST vs gRPC

| Característica | REST | gRPC |
|---------------|------|------|
| Protocolo | HTTP/1.1 o HTTP/2 | HTTP/2 |
| Formato | JSON (texto) | Protocol Buffers (binario) |
| Rendimiento | Bueno | Excelente (hasta 10x más rápido) |
| Streaming | No nativo | Bidireccional nativo |
| Tipado | Documentación manual (OpenAPI) | Contrato fuerte (.proto) |
| Debugging | Fácil (JSON legible) | Requiere herramientas (grpcurl) |
| Navegador | Nativo | Requiere gRPC-Web |
| Uso ideal | APIs públicas, frontend | Comunicación interna entre servicios |

**Recomendación para microservicios:**
- **REST** para APIs públicas (consumidas por frontends, terceros)
- **gRPC** para comunicación interna entre microservicios

### Event-Driven Architecture

**Patrones de mensajería:**

1. **Point-to-Point (Queue):** Un productor, un consumidor
   - Ejemplo: OrderService envía "ProcessPayment" → PaymentService lo procesa

2. **Publish/Subscribe (Topic/Exchange):** Un productor, múltiples consumidores
   - Ejemplo: ProductService publica "ProductCreated" → OrderService, NotifyService, AnalyticsService lo reciben

3. **Event Sourcing:** Almacenar todos los eventos como fuente de verdad
   - Reconstruir estado a partir de la secuencia de eventos

**RabbitMQ vs Azure Service Bus:**

| Característica | RabbitMQ | Azure Service Bus |
|---------------|----------|-------------------|
| Hosting | Self-hosted / Docker | Managed (Azure) |
| Protocolo | AMQP | AMQP |
| Costo | Gratuito (open source) | Pay-per-use |
| SKU Basic | Queues only | Queues only |
| SKU Standard | Topics, subscriptions | Topics, subscriptions |
| Uso ideal | Desarrollo local, on-premise | Producción en Azure |

### Domain Events

Los eventos de dominio representan algo que ocurrió en el sistema:
- `ProductCreatedEvent` - Se creó un producto
- `ProductUpdatedEvent` - Se actualizó un producto
- `ProductDeletedEvent` - Se eliminó un producto
- `OrderPlacedEvent` - Se realizó una orden

**Principio:** Un microservicio publica eventos cuando algo importante ocurre. Otros microservicios suscritos reaccionan a esos eventos.

## 🧪 Laboratorio 7 - Paso a Paso

### Objetivo
Implementar comunicación entre microservicios:
- gRPC en ProductService para comunicación interna
- Eventos de dominio con RabbitMQ
- OrderService que consume ProductService via REST/gRPC
- Publicar eventos cuando se crean/actualizan/eliminan productos
- (Opcional) Azure Service Bus

### Paso 1: Agregar RabbitMQ a docker-compose

**Archivo: `docker-compose.yml`** (agregar servicio)

```yaml
  rabbitmq:
    image: rabbitmq:4-management-alpine
    container_name: microservices-rabbitmq
    ports:
      - "5672:5672"
      - "15672:15672"
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest
      RABBITMQ_ERLANG_COOKIE: "microservices-secret-cookie"
    tmpfs:
      - /var/lib/rabbitmq
    healthcheck:
      test: ["CMD", "rabbitmq-diagnostics", "check_port_connectivity"]
      interval: 10s
      timeout: 5s
      retries: 5
```

**Nota para Podman:** Si RabbitMQ falla con `eacces` en `.erlang.cookie`, ejecútalo directamente:
```bash
podman rm -f microservices-rabbitmq 2>/dev/null
podman run -d --name microservices-rabbitmq \
  --userns=keep-id:uid=999,gid=999 \
  -p 5672:5672 -p 15672:15672 \
  -e RABBITMQ_DEFAULT_USER=guest \
  -e RABBITMQ_DEFAULT_PASS=guest \
  rabbitmq:4-management-alpine
```

### Paso 2: Instalar paquetes NuGet (ProductService)

```bash
cd src/Services/ProductService
dotnet add package RabbitMQ.Client
dotnet add package Grpc.AspNetCore
dotnet add package Grpc.AspNetCore.Server.Reflection
```

> **Nota — RabbitMQ.Client 7.x:** desde la versión 7, la API es totalmente async (`IChannel` en lugar de `IModel`, `CreateConnectionAsync`, `BasicPublishAsync`, `AsyncEventingBasicConsumer`, etc.). Los pasos 5 y 6 usan esa API. Si instalas una 6.x antigua, el código del lab no compilará.

### Paso 3: Configurar appsettings.json (ProductService)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "...",
    "Redis": "localhost:6379",
    "RabbitMq": "amqp://guest:guest@localhost:5672"
  },
  "RabbitMq": {
    "Exchange": "product-events",
    "ExchangeType": "topic"
  },
  "ServiceBus": {
    "ConnectionString": "",
    "TopicName": "product-events"
  },
  "Messaging": {
    "Provider": "rabbitmq"
  }
}
```

### Paso 4: Crear Domain Events (ProductService)

**`Domain/Events/DomainEvent.cs`**
```csharp
namespace ProductService.Domain.Events;

public abstract class DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public abstract string EventType { get; }
}
```

**`Domain/Events/IEventPublisher.cs`**
```csharp
namespace ProductService.Domain.Events;

public interface IEventPublisher
{
    Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default)
        where T : DomainEvent;
}
```

**`Domain/Events/ProductCreatedEvent.cs`**
```csharp
namespace ProductService.Domain.Events;

public class ProductCreatedEvent : DomainEvent
{
    public override string EventType => "product.created";
    public Guid ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Stock { get; init; }
}
```

**`Domain/Events/ProductUpdatedEvent.cs`**, **`ProductDeletedEvent.cs`** - similar estructura.

### Paso 5: Implementar RabbitMqEventPublisher y LogEventPublisher

**`Infrastructure/Messaging/RabbitMqEventPublisher.cs`** - Publica eventos a RabbitMQ con exchange tipo `topic` y routing key `domainEvent.EventType` (API async de RabbitMQ.Client 7.x).

```csharp
using System.Text;
using System.Text.Json;
using ProductService.Domain.Events;
using RabbitMQ.Client;

namespace ProductService.Infrastructure.Messaging;

public class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly string _exchange;
    private readonly string _exchangeType;
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly ILogger<RabbitMqEventPublisher> _logger;

    public RabbitMqEventPublisher(IConfiguration configuration, ILogger<RabbitMqEventPublisher> logger)
    {
        _logger = logger;
        _exchange = configuration["RabbitMq:Exchange"] ?? "product-events";
        _exchangeType = configuration["RabbitMq:ExchangeType"] ?? "topic";

        var connectionString = configuration.GetConnectionString("RabbitMq")
            ?? "amqp://guest:guest@localhost:5672";

        // RabbitMQ.Client 7.x es async; el ctor de DI no puede ser async,
        // así que inicializamos de forma síncrona al arrancar el singleton.
        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();
        _channel.ExchangeDeclareAsync(_exchange, _exchangeType, durable: true).GetAwaiter().GetResult();
    }

    public async Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default)
        where T : DomainEvent
    {
        var json = JsonSerializer.Serialize(domainEvent, domainEvent.GetType());
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        await _channel.BasicPublishAsync(
            exchange: _exchange,
            routingKey: domainEvent.EventType,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Published event {EventType} ({EventId})", domainEvent.EventType, domainEvent.EventId);
    }

    public async ValueTask DisposeAsync()
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
    }
}
```

**`Infrastructure/Messaging/LogEventPublisher.cs`** - Fallback que solo hace `_logger.LogInformation` cuando RabbitMQ no está disponible:

```csharp
using ProductService.Domain.Events;

namespace ProductService.Infrastructure.Messaging;

public class LogEventPublisher : IEventPublisher
{
    private readonly ILogger<LogEventPublisher> _logger;

    public LogEventPublisher(ILogger<LogEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default)
        where T : DomainEvent
    {
        _logger.LogInformation(
            "[Fallback] Event {EventType} ({EventId}) not sent — RabbitMQ unavailable",
            domainEvent.EventType, domainEvent.EventId);
        return Task.CompletedTask;
    }
}
```

**Publicar eventos desde `Application/Services/ProductService.cs`** - inyectar `IEventPublisher` y llamar a `PublishAsync` cuando se crea, actualiza o elimina un producto:

```csharp
private readonly IEventPublisher _eventPublisher;   // ← NUEVO campo

public ProductService(
    IProductRepository repository,
    IProductCache cache,
    IEventPublisher eventPublisher,                 // ← NUEVO parámetro
    ILogger<ProductService> logger)
{
    _repository = repository;
    _cache = cache;
    _eventPublisher = eventPublisher;
    _logger = logger;
}
```

En `CreateAsync`, después de guardar en cache:
```csharp
await _cache.SetAsync(createdProduct.Id, result, cancellationToken);

await _eventPublisher.PublishAsync(new ProductCreatedEvent
{
    ProductId = createdProduct.Id,
    Name = createdProduct.Name,
    Price = createdProduct.Price,
    Stock = createdProduct.Stock
}, cancellationToken);

return result;
```

En `UpdateAsync`, después de invalidar el cache (dentro del `if (updated)`):
```csharp
if (updated)
{
    await _cache.RemoveAsync(id, cancellationToken);

    await _eventPublisher.PublishAsync(new ProductUpdatedEvent
    {
        ProductId = product.Id,
        Name = product.Name,
        Price = product.Price,
        Stock = product.Stock
    }, cancellationToken);
}
```

En `DeleteAsync`, después de invalidar el cache (dentro del `if (deleted)`):
```csharp
if (deleted)
{
    await _cache.RemoveAsync(id, cancellationToken);

    await _eventPublisher.PublishAsync(new ProductDeletedEvent
    {
        ProductId = id
    }, cancellationToken);
}
```

> `ProductUpdatedEvent` y `ProductDeletedEvent` siguen la misma estructura que `ProductCreatedEvent` (Paso 4): heredan de `DomainEvent`, definen su propio `EventType` (`"product.updated"` / `"product.deleted"`) y las propiedades que necesites.

### Paso 6: Implementar ProductEventConsumer (BackgroundService)

Consumidor que escucha `product.*` en RabbitMQ y procesa los eventos (API async de RabbitMQ.Client 7.x; la lógica va en `ExecuteAsync`):

**`Infrastructure/Messaging/ProductEventConsumer.cs`**
```csharp
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ProductService.Infrastructure.Messaging;

public class ProductEventConsumer : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProductEventConsumer> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public ProductEventConsumer(IConfiguration configuration, ILogger<ProductEventConsumer> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var exchange = _configuration["RabbitMq:Exchange"] ?? "product-events";
        var exchangeType = _configuration["RabbitMq:ExchangeType"] ?? "topic";
        var connectionString = _configuration.GetConnectionString("RabbitMq")
            ?? "amqp://guest:guest@localhost:5672";

        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await _channel.ExchangeDeclareAsync(exchange, exchangeType, durable: true, cancellationToken: stoppingToken);

        // Cola exclusiva y auto-eliminable: solo existe mientras corre este servicio
        var queue = await _channel.QueueDeclareAsync(cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queue.QueueName, exchange, routingKey: "product.*", cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());
            _logger.LogInformation("Event received [{RoutingKey}]: {Body}", ea.RoutingKey, body);
            await Task.CompletedTask;
        };
        await _channel.BasicConsumeAsync(queue.QueueName, autoAck: true, consumer, stoppingToken);

        // Mantener el BackgroundService vivo hasta que se cancele
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown normal
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
            await _channel.CloseAsync(cancellationToken);
        if (_connection is not null)
            await _connection.CloseAsync(cancellationToken);

        await base.StopAsync(cancellationToken);
    }
}
```

Este `BackgroundService` corre dentro del mismo proceso de `ProductService` y sirve para que los alumnos vean en consola cómo se reciben los eventos que ellos mismos publican al crear/actualizar/eliminar productos.

### Paso 7: Agregar gRPC a ProductService

**`Protos/product.proto`**
```protobuf
syntax = "proto3";
option csharp_namespace = "ProductService.Grpc";
package productservice;

service ProductGrpc {
  rpc GetProduct (GetProductRequest) returns (ProductReply);
  rpc GetAllProducts (GetAllProductsRequest) returns (ProductListReply);
}
message GetProductRequest { string id = 1; }
message GetAllProductsRequest {}
message ProductReply { string id = 1; string name = 2; string description = 3; double price = 4; int32 stock = 5; string created_at = 6; }
message ProductListReply { repeated ProductReply products = 1; }
```

Agregar la referencia en **`ProductService.csproj`** para que se generen las clases del contrato:
```xml
<ItemGroup>
  <Protobuf Include="Protos/product.proto" GrpcServices="Server" />
</ItemGroup>
```

**`Services/ProductGrpcService.cs`** - implementa el contrato del `.proto` reutilizando `IProductRepository`:

```csharp
using Grpc.Core;
using ProductService.Domain;

namespace ProductService.Grpc;

public class ProductGrpcService : ProductGrpc.ProductGrpcBase
{
    private readonly IProductRepository _repository;

    public ProductGrpcService(IProductRepository repository)
    {
        _repository = repository;
    }

    public override async Task<ProductReply> GetProduct(GetProductRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.Id, out var id))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid product id"));

        var product = await _repository.GetByIdAsync(id, context.CancellationToken);
        if (product == null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Product {request.Id} not found"));

        return ToReply(product);
    }

    public override async Task<ProductListReply> GetAllProducts(GetAllProductsRequest request, ServerCallContext context)
    {
        var products = await _repository.GetAllAsync(context.CancellationToken);
        var reply = new ProductListReply();
        reply.Products.AddRange(products.Select(ToReply));
        return reply;
    }

    private static ProductReply ToReply(Product product) => new()
    {
        Id = product.Id.ToString(),
        Name = product.Name,
        Description = product.Description,
        Price = (double)product.Price,
        Stock = product.Stock,
        CreatedAt = product.CreatedAt.ToString("O")
    };
}
```

**Actualizar `Program.cs` de ProductService** con todo lo de este módulo. Agregar estos `using`:
```csharp
using ProductService.Domain.Events;
using ProductService.Infrastructure.Messaging;
```

Después del registro de `IProductCache` (Redis/InMemory) y antes de `var app = builder.Build();`, agregar:
```csharp
// Domain Events: RabbitMQ o fallback a logging, según configuración (Módulo 7)
var messagingProvider = builder.Configuration["Messaging:Provider"] ?? "rabbitmq";
if (messagingProvider == "rabbitmq")
{
    builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
}
else
{
    builder.Services.AddSingleton<IEventPublisher, LogEventPublisher>();
}
builder.Services.AddHostedService<ProductEventConsumer>();

// gRPC (Módulo 7)
builder.Services.AddGrpc();
builder.Services.AddGrpcReflection();
```

Reemplazar (o agregar, si todavía no existe) la configuración de Kestrel para exponer REST en 5001 y gRPC en 5002:
```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5001, o => o.Protocols = HttpProtocols.Http1);       // REST
    options.ListenLocalhost(5002, o => o.Protocols = HttpProtocols.Http2);       // gRPC
});
```
(agregar `using Microsoft.AspNetCore.Server.Kestrel.Core;` para `HttpProtocols`).

Después de `var app = builder.Build();` y antes de `app.Run();`, junto a `app.MapControllers();`:
```csharp
app.MapControllers();
app.MapGrpcService<ProductGrpcService>();
app.MapGrpcReflectionService();
```

> **Nota:** gRPC requiere HTTP/2. Si `grpcurl` falla con errores de protocolo, confirma que el puerto 5002 está configurado con `HttpProtocols.Http2` y no `Http1`.

### Paso 8: Crear OrderService

`OrderService` no existe todavía en el proyecto: hay que crearlo desde cero, igual que se hizo con `ProductService` en el Módulo 1, con persistencia en PostgreSQL (Módulo 4) y un cliente hacia `ProductService`.

#### Paso 8.1: Crear el proyecto y la estructura de carpetas

```bash
cd src/Services
dotnet new webapi -n OrderService --no-https --use-controllers
cd OrderService

mkdir -p Domain Infrastructure Clients
mkdir -p Application/DTOs
mkdir -p Controllers/V1
rm -f Controllers/WeatherForecastController.cs WeatherForecast.cs 2>/dev/null
```

Agregar el proyecto a la solución si estás usando una `.sln`:
```bash
cd ../../..
dotnet sln add src/Services/OrderService/OrderService.csproj
```

#### Paso 8.2: Instalar paquetes NuGet

`OrderService` usa PostgreSQL igual que `ProductService` desde el Módulo 4, así que suma también los paquetes de EF Core:

```bash
cd src/Services/OrderService
dotnet add package Asp.Versioning.Mvc
dotnet add package Asp.Versioning.Mvc.ApiExplorer
dotnet add package Grpc.Net.ClientFactory
dotnet add package Google.Protobuf
dotnet add package Grpc.Tools
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
```

> Si todavía no instalaste la herramienta `dotnet-ef` en el Módulo 4, hazlo ahora: `dotnet tool install --global dotnet-ef`.

#### Paso 8.3: Referenciar el `.proto` de ProductService

`OrderService` reutiliza el contrato gRPC que ya definiste en `ProductService/Protos/product.proto` (Paso 7). Editar **`OrderService.csproj`** y agregar:

```xml
<ItemGroup>
  <Protobuf Include="../ProductService/Protos/product.proto" GrpcServices="Client" Link="Protos/product.proto" />
</ItemGroup>
```

> **Importante:** esta referencia cruzada es la razón por la que, más adelante (Módulo 13), el build de Docker de `OrderService` debe usar `src/Services/` como build context y no `src/Services/OrderService/`.

#### Paso 8.4: Entidades de dominio

**`Domain/Order.cs`**
```csharp
namespace OrderService.Domain;

public class Order
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
    public decimal Total => Items.Sum(i => i.UnitPrice * i.Quantity);
    public DateTime CreatedAt { get; set; }

    public Order(string customerName, List<OrderItem> items)
    {
        Id = Guid.NewGuid();
        CustomerName = customerName;
        Items = items;
        CreatedAt = DateTime.UtcNow;
    }
}

public class OrderItem
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
```

**`Domain/IOrderRepository.cs`**
```csharp
namespace OrderService.Domain;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Order>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
```

#### Paso 8.5: Persistencia con EF Core + PostgreSQL

Como ya trabajamos con PostgreSQL desde el Módulo 4, `OrderService` persiste sus órdenes en la misma base `microservices_db` (tablas separadas de las de `ProductService`), en lugar de guardarlas en memoria.

**Connection string** — agregar a `appsettings.json` (ver Paso 8.10 completo más abajo):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=microservices_db;Username=postgres;Password=postgres"
  }
}
```

**`Infrastructure/OrderDbContext.cs`**
```csharp
using Microsoft.EntityFrameworkCore;
using OrderService.Domain;

namespace OrderService.Infrastructure;

public class OrderDbContext : DbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Ignore(e => e.Total); // Propiedad calculada, no se persiste

            // Items como owned entities en su propia tabla (OrderItems)
            entity.OwnsMany(e => e.Items, item =>
            {
                item.WithOwner().HasForeignKey("OrderId");
                item.Property<int>("Id");
                item.HasKey("Id");
                item.ToTable("OrderItems");

                item.Property(i => i.ProductId).IsRequired();
                item.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
                item.Property(i => i.UnitPrice).HasColumnType("decimal(18,2)");
                item.Property(i => i.Quantity).IsRequired();
            });

            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
```

**`Infrastructure/DesignTimeDbContextFactory.cs`** (necesario para que `dotnet ef migrations` funcione sin levantar toda la app):
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OrderService.Infrastructure;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrderDbContext>
{
    public OrderDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrderDbContext>();

        var connectionString = "Host=localhost;Port=5432;Database=microservices_db;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(connectionString);

        return new OrderDbContext(optionsBuilder.Options);
    }
}
```

**Crear la migración inicial** (con PostgreSQL corriendo, `docker-compose up -d postgres`):
```bash
cd src/Services/OrderService
dotnet ef migrations add InitialCreate --output-dir Infrastructure/Migrations
```

**`Infrastructure/EfOrderRepository.cs`**
```csharp
using Microsoft.EntityFrameworkCore;
using OrderService.Domain;

namespace OrderService.Infrastructure;

public class EfOrderRepository : IOrderRepository
{
    private readonly OrderDbContext _context;
    private readonly ILogger<EfOrderRepository> _logger;

    public EfOrderRepository(OrderDbContext context, ILogger<EfOrderRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Order>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Order> CreateAsync(Order order, CancellationToken cancellationToken = default)
    {
        _context.Orders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Created order with ID: {OrderId}", order.Id);
        return order;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _context.Orders.FindAsync(new object[] { id }, cancellationToken);
        if (order == null)
        {
            return false;
        }

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted order with ID: {OrderId}", id);
        return true;
    }
}
```

> Este es el `EfOrderRepository` que el Módulo 8 registra con `builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();` (reemplaza cualquier referencia previa a un repositorio en memoria).

#### Paso 8.6: DTOs

**`Application/DTOs/OrderDtos.cs`**
```csharp
using System.ComponentModel.DataAnnotations;

namespace OrderService.Application.DTOs;

public class ProductInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

public class CreateOrderItemDto
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }
}

public class CreateOrderDto
{
    [Required]
    public string CustomerName { get; set; } = string.Empty;

    [MinLength(1)]
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}

public class OrderDto
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public List<OrderItemDto> Items { get; set; } = new();
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

#### Paso 8.7: Cliente hacia ProductService (HTTP y gRPC)

**`Clients/IProductServiceClient.cs`**
```csharp
using OrderService.Application.DTOs;

namespace OrderService.Clients;

public interface IProductServiceClient
{
    Task<IEnumerable<ProductInfoDto>> GetAvailableProductsAsync(CancellationToken cancellationToken = default);
    Task<ProductInfoDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
```

**`Clients/HttpProductServiceClient.cs`** - Llama a `ProductService` vía REST (`http://localhost:5001`):
```csharp
using OrderService.Application.DTOs;

namespace OrderService.Clients;

public class HttpProductServiceClient : IProductServiceClient
{
    private readonly HttpClient _httpClient;

    public HttpProductServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IEnumerable<ProductInfoDto>> GetAvailableProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = await _httpClient.GetFromJsonAsync<List<ProductInfoDto>>(
            "/api/v1/Products", cancellationToken);
        return products ?? [];
    }

    public async Task<ProductInfoDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<ProductInfoDto>(
            $"/api/v1/Products/{id}", cancellationToken);
    }
}
```

**`Clients/GrpcProductServiceClient.cs`** - Llama a `ProductService` vía gRPC (puerto 5002), usando el cliente generado a partir de `product.proto`:
```csharp
using OrderService.Application.DTOs;
using ProductService.Grpc;

namespace OrderService.Clients;

public class GrpcProductServiceClient : IProductServiceClient
{
    private readonly ProductGrpc.ProductGrpcClient _client;

    public GrpcProductServiceClient(ProductGrpc.ProductGrpcClient client)
    {
        _client = client;
    }

    public async Task<IEnumerable<ProductInfoDto>> GetAvailableProductsAsync(CancellationToken cancellationToken = default)
    {
        var reply = await _client.GetAllProductsAsync(new GetAllProductsRequest(), cancellationToken: cancellationToken);
        return reply.Products.Select(p => new ProductInfoDto
        {
            Id = Guid.Parse(p.Id),
            Name = p.Name,
            Price = (decimal)p.Price,
            Stock = p.Stock
        });
    }

    public async Task<ProductInfoDto?> GetProductByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var reply = await _client.GetProductAsync(new GetProductRequest { Id = id.ToString() }, cancellationToken: cancellationToken);
        return new ProductInfoDto
        {
            Id = Guid.Parse(reply.Id),
            Name = reply.Name,
            Price = (decimal)reply.Price,
            Stock = reply.Stock
        };
    }
}
```

#### Paso 8.8: OrdersController

**`Controllers/V1/OrdersController.cs`**
```csharp
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using OrderService.Application.DTOs;
using OrderService.Clients;
using OrderService.Domain;

namespace OrderService.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IProductServiceClient _productServiceClient;

    public OrdersController(IOrderRepository orderRepository, IProductServiceClient productServiceClient)
    {
        _orderRepository = orderRepository;
        _productServiceClient = productServiceClient;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderDto>>> GetAll(CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.GetAllAsync(cancellationToken);
        return Ok(orders.Select(ToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(id, cancellationToken);
        if (order == null) return NotFound($"Order with ID {id} not found");
        return Ok(ToDto(order));
    }

    // IMPORTANTE: esta ruta debe declararse antes que "{id}" para que no colisione
    [HttpGet("available-products")]
    public async Task<ActionResult<IEnumerable<ProductInfoDto>>> GetAvailableProducts(CancellationToken cancellationToken)
    {
        var products = await _productServiceClient.GetAvailableProductsAsync(cancellationToken);
        return Ok(products);
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var items = new List<OrderItem>();
        foreach (var item in dto.Items)
        {
            var product = await _productServiceClient.GetProductByIdAsync(item.ProductId, cancellationToken);
            if (product == null)
                return BadRequest($"Product {item.ProductId} not found");

            items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = item.Quantity
            });
        }

        var order = new Order(dto.CustomerName, items);
        await _orderRepository.CreateAsync(order, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = order.Id, version = "1.0" }, ToDto(order));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _orderRepository.DeleteAsync(id, cancellationToken);
        if (!deleted) return NotFound($"Order with ID {id} not found");
        return NoContent();
    }

    private static OrderDto ToDto(Order order) => new()
    {
        Id = order.Id,
        CustomerName = order.CustomerName,
        Total = order.Total,
        CreatedAt = order.CreatedAt,
        Items = order.Items.Select(i => new OrderItemDto
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            UnitPrice = i.UnitPrice,
            Quantity = i.Quantity
        }).ToList()
    };
}
```

> **Nota sobre rutas:** `[HttpGet("available-products")]` debe ir antes que `[HttpGet("{id}")]` en el archivo (o ASP.NET Core intentará interpretar `available-products` como un `Guid` y devolverá 400). En ASP.NET Core el orden de las rutas atributo no importa por declaración textual sino por especificidad, pero mantenerlo así evita confusiones al leer el controller.

#### Paso 8.9: Program.cs

```csharp
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
```

#### Paso 8.10: appsettings.json

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
    "DefaultConnection": "Host=localhost;Port=5432;Database=microservices_db;Username=postgres;Password=postgres"
  },
  "ProductService": {
    "CommunicationMode": "http",
    "HttpUrl": "http://localhost:5001",
    "GrpcUrl": "http://localhost:5002"
  }
}
```

- `CommunicationMode`: `"http"` para usar `HttpProductServiceClient`, `"grpc"` para usar `GrpcProductServiceClient`. Es un buen momento para que los alumnos cambien el valor y comparen ambos modos.
- `ConnectionStrings:DefaultConnection`: misma base `microservices_db` que usa `ProductService`, pero en tablas propias (`Orders`, `OrderItems`) gracias al `OrderDbContext` del Paso 8.5.

### Paso 9: Probar

**Instalar `grpcurl`** (si no lo tienes) para probar el servicio gRPC sin escribir un cliente:
```bash
# macOS
brew install grpcurl

# Windows (choco)
choco install grpcurl

# Linux: descarga el binario desde https://github.com/fullstorydev/grpcurl/releases
```

```bash
# Terminal 0: Infraestructura (si no está corriendo todavía)
docker-compose up -d postgres rabbitmq
# Podman: podman compose up -d postgres rabbitmq (o ver Paso 1 si RabbitMQ falla con eacces)

# Terminal 1: ProductService
cd src/Services/ProductService && dotnet run

# Terminal 2: OrderService
cd src/Services/OrderService && dotnet run

# Ver productos (OrderService → ProductService)
curl http://localhost:5003/api/v1/Orders/available-products | jq

# Crear producto (publica evento product.created — revisa la consola de ProductService,
# el ProductEventConsumer debe loguear el evento recibido)
curl -X POST http://localhost:5001/api/v1/Products \
  -H "Content-Type: application/json" \
  -d '{"name":"Monitor","description":"4K","price":499.99,"stock":10}' | jq

# Guarda el "id" del producto creado y crea una orden (OrderService → ProductService)
curl -X POST http://localhost:5003/api/v1/Orders \
  -H "Content-Type: application/json" \
  -d '{"customerName":"Juan Perez","items":[{"productId":"<ID_DEL_PRODUCTO>","quantity":2}]}' | jq

# Listar las órdenes creadas (deben venir de PostgreSQL, no de memoria)
curl http://localhost:5003/api/v1/Orders | jq

# Probar gRPC
grpcurl -plaintext localhost:5002 list
grpcurl -plaintext localhost:5002 productservice.ProductGrpc/GetAllProducts

# (Opcional) Ver los mensajes en la UI de RabbitMQ
open http://localhost:15672   # usuario/clave: guest/guest
```

> Si `curl -X POST .../Orders` responde `400 Bad Request` con "Product ... not found", revisa que el `productId` sea el `id` (GUID) devuelto al crear el producto, y que `ProductService` esté corriendo antes que `OrderService` haga la llamada.

### Paso 10 (Opcional): Azure Service Bus

**Requisito:** SKU Standard (no Basic) para topics.

```bash
az servicebus namespace create --name sb-microservices --resource-group rg-microservices --sku Standard
az servicebus topic create --name product-events --namespace-name sb-microservices --resource-group rg-microservices
az servicebus topic subscription create --name product-events-sub --topic-name product-events --namespace-name sb-microservices --resource-group rg-microservices
```

**User Secrets** (nunca commitear el connection string):
```bash
dotnet user-secrets set "ServiceBus:ConnectionString" "Endpoint=sb://..."
dotnet user-secrets set "Messaging:Provider" "servicebus"
```

### ✅ Checklist

- [ ] RabbitMQ corriendo (Docker o Podman)
- [ ] ProductService: gRPC en 5002, REST en 5001
- [ ] ProductService: Domain Events + RabbitMqEventPublisher
- [ ] OrderService creado (`dotnet new webapi`) con Domain/DTOs/Repository/Controller propios
- [ ] OrderService: persistencia con EF Core + PostgreSQL (`OrderDbContext`, migración `InitialCreate`)
- [ ] OrderService: HTTP o gRPC hacia ProductService
- [ ] Crear producto publica evento (ver logs o RabbitMQ UI)
- [ ] grpcurl funciona con reflection
- [ ] (Opcional) Azure Service Bus con User Secrets

### 🐛 Solución de Problemas

**RabbitMQ eacces en Podman:** Usar `podman run` directo con `--userns=keep-id:uid=999,gid=999`.

**`IModel` could not be found / API 6.x vs 7.x:** Este lab usa **RabbitMQ.Client 7.x** (`IChannel`, métodos `*Async`). Si ves errores de `IModel` o `CreateConnection()`, estás mezclando código de la API 6.x con el paquete 7.x (o al revés). Alinea el código con los Pasos 5–6 del lab.

**grpcurl "server does not support reflection":** Agregar `Grpc.AspNetCore.Server.Reflection`, `AddGrpcReflection()`, `MapGrpcReflectionService()`.

**Azure Service Bus "MessagingGatewayNotFoundStatusCode":** El SKU Basic no soporta topics. Usar SKU Standard.

**Secret en Git:** Usar `dotnet user-secrets` para connection strings. Nunca commitear en `appsettings.json`. Si ya se subió, rotar la clave en Azure y reescribir historial con `git rebase -i`.
