# Módulo 10 – Serverless & Event-Driven

## 🧠 Teoría

### Azure Functions

Azure Functions permite ejecutar código sin infraestructura:
- **Pay-per-use**: solo pagas por las ejecuciones
- **Escalado automático**: se escala según la carga
- **Triggers variados**: HTTP, Queue, Timer, Service Bus, Event Hub, etc.
- Integración nativa con otros servicios Azure

### Durable Functions

Durable Functions extiende Azure Functions:
- Orquestación de funciones (workflows)
- State management entre llamadas
- Patrones: Fan-out/Fan-in, Human interaction, Chaining

### Integración con eventos

Las Functions pueden:
- Consumir eventos de **Service Bus** (colas y topics)
- Procesar eventos de **Event Hub**
- Reaccionar a cambios en **Cosmos DB**
- Integrar con **Logic Apps**

---

## 🧪 Laboratorio 10 – Azure Functions + Service Bus

### Objetivo

Crear una Azure Function que consuma mensajes del topic **product-events** de Service Bus (publicados por ProductService u otro emisor) y procesarlos.

### Prerrequisitos

- .NET 8 SDK (Azure Functions isolated worker usa .NET 8; puede coexistir con .NET 10 en el resto del taller)
- Cuenta Azure con un **Service Bus** namespace
- **Azure Functions Core Tools** (para ejecutar en local)

---

### Paso 1: Instalar Azure Functions Core Tools + plantillas

**macOS (Homebrew):**
```bash
brew tap azure/functions
brew install azure-functions-core-tools@4
```

**Windows (npm):**
```cmd
npm install -g azure-functions-core-tools@4
```

**Linux (Ubuntu/Debian):**
```bash
wget https://github.com/Azure/azure-functions-core-tools/releases/download/4.0.5455/Azure.Functions.Cli.linux-x64.4.0.5455.zip
sudo unzip Azure.Functions.Cli.linux-x64.4.0.5455.zip -d /usr/local/azure-functions
sudo chmod +x /usr/local/azure-functions/func
# Añadir al PATH si es necesario
```

Comprobar Core Tools:
```bash
func --version
```

**Plantilla de `dotnet new` (obligatoria si usas `dotnet new func`):**
```bash
dotnet new install Microsoft.Azure.Functions.Worker.ProjectTemplates
dotnet new list func
```

> Si ves `No templates or subcommands found matching: 'func'`, es porque faltan estas plantillas (tener `func` en el PATH no las instala).

---

### Paso 2: Crear el proyecto Azure Functions

En la raíz del repo:

```bash
# Linux/macOS
mkdir -p src/Functions
cd src/Functions
dotnet new func -n Functions -F net8.0

# Windows (PowerShell)
New-Item -ItemType Directory -Force -Path src\Functions
cd src\Functions
dotnet new func -n Functions -F net8.0
```

> **Nota:** La plantilla actual ya genera **isolated worker**. No uses `--template "Isolated worker"` (ese parámetro ya no existe). Alternativa con Core Tools: `func init . --worker-runtime dotnet-isolated --target-framework net8.0`.

Añadir paquetes para Service Bus:

```bash
cd src/Functions
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.ServiceBus --version 5.22.0
```

---

### Paso 3: Estructura del proyecto

Estructura esperada:

```
src/Functions/
├── Functions.csproj
├── Program.cs
├── host.json
├── local.settings.json.example   # Plantilla (sí se sube al repo)
├── local.settings.json          # Secretos locales (NO subir; en .gitignore)
└── ProcessProductEvent.cs        # Function con Service Bus trigger
```

---

### Paso 4: Código de la Function (Service Bus trigger)

Crear `ProcessProductEvent.cs`:

```csharp
using System;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Functions;

public class ProcessProductEvent
{
    private readonly ILogger<ProcessProductEvent> _logger;

    public ProcessProductEvent(ILogger<ProcessProductEvent> logger)
    {
        _logger = logger;
    }

    [Function(nameof(ProcessProductEvent))]
    public async Task Run(
        [ServiceBusTrigger("product-events", "product-events-sub", Connection = "ServiceBusConnection")]
        ServiceBusReceivedMessage message,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Message received: MessageId = {MessageId}", message.MessageId);

        try
        {
            var body = message.Body.ToString();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("EventType", out var eventType))
                _logger.LogInformation("EventType: {EventType}", eventType.GetString());
            if (root.TryGetProperty("ProductId", out var productId))
                _logger.LogInformation("ProductId: {ProductId}", productId.GetString());

            // Aquí podrías: actualizar caché, notificar, escribir en otro sistema, etc.
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message");
            throw;
        }
    }
}
```

En `Program.cs` (proyecto isolated worker) debe estar registrado el host y los servicios. Ejemplo mínimo:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

await host.RunAsync();
```

---

### Paso 5: Configuración local

- **`local.settings.json`** no se sube al repo (está en `.gitignore`). Copia la plantilla:

```bash
# Linux/macOS
cp src/Functions/local.settings.json.example src/Functions/local.settings.json

# Windows (PowerShell)
Copy-Item src\Functions\local.settings.json.example src\Functions\local.settings.json
```

Edita `local.settings.json` y pon tu **connection string** de Service Bus en `ServiceBusConnection` (desde Azure Portal → Service Bus → Shared access policies → RootManageSharedAccessKey → Connection string).

- **`host.json`** (ejemplo con extensión Service Bus):

```json
{
  "version": "2.0",
  "extensions": {
    "serviceBus": {
      "prefetchCount": 0,
      "messageHandlerOptions": {
        "autoComplete": true,
        "maxConcurrentCalls": 16
      }
    }
  }
}
```

---

### Paso 6: Crear topic y suscripción en Azure Service Bus

En Azure Portal:

1. Ve a tu **Service Bus** namespace (ej. `sb-microservices-joedayz`).
2. **Entities** → **Topics** → **+ Topic** → nombre: `product-events`.
3. Entra al topic **product-events** → **Subscriptions** → **+ Subscription** → nombre: `product-events-sub`.

O con Azure CLI:

```bash
# Variables (ajusta nombres y grupo de recursos)
RESOURCE_GROUP=rg-microservices
NAMESPACE=sb-microservices-joedayz

az servicebus topic create --resource-group $RESOURCE_GROUP --namespace-name $NAMESPACE --name product-events
az servicebus topic subscription create --resource-group $RESOURCE_GROUP --namespace-name $NAMESPACE --topic-name product-events --name product-events-sub
```

---

### Paso 7: Enviar un mensaje de prueba

La CLI de Azure **no** tiene `az servicebus topic send`. Formas de probar:

**Opción A – Azure Portal (Service Bus Explorer)**  
1. Service Bus → Topics → **product-events** → **Service Bus Explorer**.  
2. Pestaña **Send messages**.  
3. Body (ejemplo):  
   `{"EventType":"product.created","ProductId":"11111111-1111-1111-1111-111111111111"}`  
4. **Send**.

**Opción B – Desde código**  
Usar el paquete `Azure.Messaging.ServiceBus` en un script o en ProductService (si está configurado para publicar en Service Bus) para enviar al topic `product-events`.

Al ejecutar la Function en local (`func start`), deberías ver en consola el log del mensaje recibido.

---

### Paso 8: Ejecutar la Function en local

```bash
cd src/Functions
func start
```

Comprueba que aparece la Function `ProcessProductEvent` y que, al enviar un mensaje al topic (Portal o código), se procesa y ves los logs.

---

## 🌐 ngrok e APIM (exponer servicios locales y llamar por APIM)

Si quieres que **APIM** llame a tus servicios corriendo en local, puedes exponerlos con **ngrok** y luego registrar las APIs en APIM con los comandos que sí funcionan en la CLI.

### Instalar ngrok

**macOS (Homebrew):**
```bash
brew install ngrok
```

**Windows (winget):**
```powershell
winget install ngrok.ngrok
```

**Windows (Chocolatey):**
```powershell
choco install ngrok
```

**Linux:** descarga desde [ngrok.com](https://ngrok.com/download) o con snap:
```bash
sudo snap install ngrok
```

Configurar autenticación (token de ngrok.com):
```bash
ngrok config add-authtoken TU_TOKEN
```

Exponer servicios (en terminales separadas o con un `ngrok.yml` con varios túneles):
```bash
# ProductService en 5001
ngrok http 5001

# OrderService en 5003
ngrok http 5003
```

Copia la URL **https** que te da ngrok (ej. `https://xxxx.ngrok-free.app`) para usarla como backend en APIM.

### Crear APIs en APIM con la CLI (comandos actualizados)

La CLI **no** incluye `az apim backend create`. La URL del backend se configura al crear la API con `--service-url`. Usa estos comandos (sustituye las URLs por las de ngrok o App Service):

```bash
RESOURCE_GROUP=rg-microservices
APIM_NAME=apim-microservices-joedayz

# API ProductService (path: products)
az apim api create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --api-id product-api \
  --display-name "Product API" \
  --path products \
  --service-url https://TU-URL-NGROK-PRODUCTO.ngrok-free.app \
  --protocols https

# API OrderService (path: orders)
az apim api create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --api-id order-api \
  --display-name "Order API" \
  --path orders \
  --service-url https://TU-URL-NGROK-ORDERS.ngrok-free.app \
  --protocols https
```

Después, en el Portal de APIM puedes añadir **operations** (GET, POST, etc.) a cada API. Para probar:

```bash
curl -H "Ocp-Apim-Subscription-Key: <SUBSCRIPTION_KEY>" \
  "https://<APIM_NAME>.azure-api.net/products"
curl -H "Ocp-Apim-Subscription-Key: <SUBSCRIPTION_KEY>" \
  "https://<APIM_NAME>.azure-api.net/orders/available-products"
```

Si tu backend expone rutas bajo `/api/v1/Products`, configura en la operación de APIM la **reescritura de URL** al backend (rewrite-uri) a `/api/v1/Products` para que APIM llame al path correcto.

---

## (Opcional) Conectar ProductService a Service Bus

Si quieres que **ProductService** publique eventos al topic `product-events` de Service Bus (además o en lugar de RabbitMQ):

1. Añadir en ProductService el paquete `Azure.Messaging.ServiceBus`.
2. Implementar un `IEventPublisher` que envíe mensajes al topic (por ejemplo serializando el evento a JSON).
3. Configurar la connection string en **User Secrets** o variables de entorno (nunca en appsettings en el repo):

```bash
cd src/Services/ProductService
dotnet user-secrets set "ServiceBus:ConnectionString" "Endpoint=sb://...."
```

En `appsettings.json` solo referencias: `"ServiceBus:ConnectionString": ""` o lees desde configuración.

---

## ✅ Checklist Módulo 10

- [ ] Azure Functions Core Tools instalado (`func --version`).
- [ ] Proyecto Functions creado (isolated worker, .NET 8) con trigger Service Bus.
- [ ] Topic `product-events` y suscripción `product-events-sub` creados en Service Bus.
- [ ] `local.settings.json` configurado con `ServiceBusConnection` (no subido al repo).
- [ ] Function ejecutada en local (`func start`) y mensaje de prueba recibido (Portal o código).
- [ ] (Opcional) ngrok instalado y APIs de APIM creadas con `az apim api create` (product-api, order-api).
- [ ] (Opcional) ProductService publicando en Service Bus vía user-secrets.

---

## Enlaces

- [Azure Functions (isolated worker)](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide)
- [Service Bus trigger](https://learn.microsoft.com/azure/azure-functions/functions-bindings-service-bus-trigger)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [ngrok](https://ngrok.com/docs)
