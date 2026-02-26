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

**`Infrastructure/Messaging/RabbitMqEventPublisher.cs`** - Publica eventos a RabbitMQ con exchange tipo `topic` y routing key `domainEvent.EventType`.

**`Infrastructure/Messaging/LogEventPublisher.cs`** - Fallback que solo hace `_logger.LogInformation` cuando RabbitMQ no está disponible.

### Paso 6: Implementar ProductEventConsumer (BackgroundService)

Consumidor que escucha `product.*` en RabbitMQ y procesa los eventos. Ver código en el proyecto ProductService.

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

En `Program.cs`: `AddGrpc()`, `AddGrpcReflection()`, `MapGrpcService<ProductGrpcService>()`, `MapGrpcReflectionService()`.

Configurar Kestrel: puerto 5001 (REST) y 5002 (gRPC).

### Paso 8: OrderService

OrderService tiene `IProductServiceClient` con implementaciones:
- **HttpProductServiceClient** - Llama a `http://localhost:5001/api/v1/Products/{id}`
- **GrpcProductServiceClient** - Llama a ProductService vía gRPC en puerto 5002

Configurar en `appsettings.json`:
```json
{
  "ProductService": {
    "CommunicationMode": "http",
    "HttpUrl": "http://localhost:5001",
    "GrpcUrl": "http://localhost:5002"
  }
}
```

### Paso 9: Probar

```bash
# Terminal 1: ProductService
cd src/Services/ProductService && dotnet run

# Terminal 2: OrderService
cd src/Services/OrderService && dotnet run

# Ver productos (OrderService → ProductService)
curl http://localhost:5003/api/v1/Orders/available-products | jq

# Crear producto (publica evento)
curl -X POST http://localhost:5001/api/v1/Products \
  -H "Content-Type: application/json" \
  -d '{"name":"Monitor","description":"4K","price":499.99,"stock":10}' | jq

# Probar gRPC
grpcurl -plaintext localhost:5002 list
grpcurl -plaintext localhost:5002 productservice.ProductGrpc/GetAllProducts
```

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
- [ ] OrderService: HTTP o gRPC hacia ProductService
- [ ] Crear producto publica evento (ver logs o RabbitMQ UI)
- [ ] grpcurl funciona con reflection
- [ ] (Opcional) Azure Service Bus con User Secrets

### 🐛 Solución de Problemas

**RabbitMQ eacces en Podman:** Usar `podman run` directo con `--userns=keep-id:uid=999,gid=999`.

**grpcurl "server does not support reflection":** Agregar `Grpc.AspNetCore.Server.Reflection`, `AddGrpcReflection()`, `MapGrpcReflectionService()`.

**Azure Service Bus "MessagingGatewayNotFoundStatusCode":** El SKU Basic no soporta topics. Usar SKU Standard.

**Secret en Git:** Usar `dotnet user-secrets` para connection strings. Nunca commitear en `appsettings.json`. Si ya se subió, rotar la clave en Azure y reescribir historial con `git rebase -i`.
