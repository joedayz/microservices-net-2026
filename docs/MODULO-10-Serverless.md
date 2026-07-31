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
- Cuenta Azure (el Service Bus se crea en este laboratorio si aún no lo tienes)
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

En `Program.cs` (proyecto isolated worker **Worker 2.x**) usa `FunctionsApplication`, no el `HostBuilder` antiguo:

```csharp
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

// Worker 2.x: FunctionsApplication (no HostBuilder + ConfigureFunctionsWorkerDefaults)
var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Build().Run();
```

> **⚠️ Error frecuente:** Si pegas el ejemplo viejo con `new HostBuilder().ConfigureFunctionsWorkerDefaults()`, fallará con la plantilla actual (`FunctionsApplicationBuilder` no tiene ese método).

---

### Paso 5: Configuración local

> **¿Aún no tienes Azure Service Bus?** Completa primero la sección siguiente (*Si no has creado Azure Service Bus*) y luego vuelve aquí a pegar la connection string.

- **`local.settings.json`** no se sube al repo (está en `.gitignore`). Contenido completo:

**Archivo: `src/Functions/local.settings.json`**

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ServiceBusConnection": "Endpoint=sb://TU-NAMESPACE.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=TU_KEY"
  }
}
```

| Clave | Valor |
|-------|--------|
| `AzureWebJobsStorage` | Storage local (`UseDevelopmentStorage=true`). En Mac/Linux suele requerir [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (`npx azurite`). También puedes poner una connection string real de Storage Account. |
| `FUNCTIONS_WORKER_RUNTIME` | Debe ser `dotnet-isolated` |
| `ServiceBusConnection` | Connection string del Service Bus (Portal → Service Bus → Shared access policies → RootManageSharedAccessKey → **Primary connection string**) |

Plantilla en el repo (sin secretos reales):

```bash
# Linux/macOS
cp src/Functions/local.settings.json.example src/Functions/local.settings.json

# Windows (PowerShell)
Copy-Item src\Functions\local.settings.json.example src\Functions\local.settings.json
```

Luego edita `local.settings.json` y reemplaza `ServiceBusConnection` con tu connection string real.

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

### Si no has creado Azure Service Bus

Sigue estos pasos **antes** de configurar `ServiceBusConnection` en `local.settings.json`.

#### 1. Login y resource group

```bash
az login

# Si no existe el resource group del taller:
az group create --name rg-microservices --location eastus
```

#### 2. Crear el namespace de Service Bus

> El nombre del namespace debe ser **único a nivel global** en Azure. Cambia el sufijo si `sb-microservices-XXXX` ya está tomado.
>
> **SKU Standard** (no Basic): el SKU Basic **no soporta topics**.

```bash
RESOURCE_GROUP=rg-microservices
LOCATION=eastus
# Cambia el nombre si ya está en uso
NAMESPACE=sb-microservices-$RANDOM

az servicebus namespace create \
  --resource-group $RESOURCE_GROUP \
  --name $NAMESPACE \
  --location $LOCATION \
  --sku Standard

echo "Namespace creado: $NAMESPACE"
```

#### 3. Crear topic y suscripción

```bash
az servicebus topic create \
  --resource-group $RESOURCE_GROUP \
  --namespace-name $NAMESPACE \
  --name product-events

az servicebus topic subscription create \
  --resource-group $RESOURCE_GROUP \
  --namespace-name $NAMESPACE \
  --topic-name product-events \
  --name product-events-sub
```

#### 4. Obtener la connection string

```bash
az servicebus namespace authorization-rule keys list \
  --resource-group $RESOURCE_GROUP \
  --namespace-name $NAMESPACE \
  --name RootManageSharedAccessKey \
  --query primaryConnectionString \
  -o tsv
```

Copia ese valor y pégalo en `local.settings.json` → `Values.ServiceBusConnection`.

**Desde el Portal (alternativa):**

1. Azure Portal → **Create a resource** → **Service Bus** → Create  
2. Resource group: `rg-microservices` · Name: único · Pricing tier: **Standard**  
3. Tras crear → **Shared access policies** → **RootManageSharedAccessKey** → copia **Primary connection string**  
4. **Entities** → **Topics** → `product-events` → **Subscriptions** → `product-events-sub`

---

### Paso 6: Crear topic y suscripción (si el namespace ya existía)

Si creaste el namespace en la sección anterior, **ya tienes** topic y suscripción. Omite este paso.

Si el namespace ya existía de antes, crea solo topic + suscripción:

En Azure Portal:

1. Ve a tu **Service Bus** namespace.
2. **Entities** → **Topics** → **+ Topic** → nombre: `product-events`.
3. Entra al topic **product-events** → **Subscriptions** → **+ Subscription** → nombre: `product-events-sub`.

O con Azure CLI:

```bash
RESOURCE_GROUP=rg-microservices
NAMESPACE=sb-microservices-TU_NOMBRE   # el que ya tengas

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

Configurar autenticación (token desde [ngrok Dashboard](https://dashboard.ngrok.com/get-started/your-authtoken)):

```bash
ngrok config add-authtoken TU_TOKEN
```

Eso guarda el token en el config del usuario (no en el repo):

| SO | Ruta de `ngrok.yml` |
|----|---------------------|
| macOS | `~/Library/Application Support/ngrok/ngrok.yml` |
| Linux | `~/.config/ngrok/ngrok.yml` |
| Windows | `%LocalAppData%\ngrok\ngrok.yml` |

Comprobar: `ngrok config check`

Exponer servicios (en terminales separadas):

```bash
# ProductService en 5001
ngrok http 5001

# OrderService en 5003 (otra terminal)
ngrok http 5003
```

Copia la URL **https** que te da ngrok (ej. `https://xxxx.ngrok-free.app`) para usarla como `--service-url` en APIM.

### Si no has creado Azure API Management (APIM)

Sigue estos pasos **antes** de crear las APIs con `az apim api create`.

> APIM en SKU **Developer** tarda **30–45 minutos** en aprovisionarse. El nombre debe ser **único a nivel global**.

```bash
az login

RESOURCE_GROUP=rg-microservices
LOCATION=eastus
# Cambia el nombre si ya está tomado
APIM_NAME=apim-microservices-$RANDOM
PUBLISHER_NAME="JoeDayz"
PUBLISHER_EMAIL="tu@email.com"

# Resource group (si no existe)
az group create --name $RESOURCE_GROUP --location $LOCATION

# Crear APIM (SKU Developer = barato para labs; no es SLA de producción)
az apim create \
  --name $APIM_NAME \
  --resource-group $RESOURCE_GROUP \
  --publisher-name "$PUBLISHER_NAME" \
  --publisher-email "$PUBLISHER_EMAIL" \
  --location $LOCATION \
  --sku-name Developer

echo "APIM creado: $APIM_NAME"
echo "Gateway URL: https://$APIM_NAME.azure-api.net"
```

Comprobar estado (debe quedar en `Succeeded` / `Online`):

```bash
az apim show --name $APIM_NAME --resource-group $RESOURCE_GROUP --query "{name:name,state:provisioningState,gateway:gatewayUrl}" -o table
```

**Desde el Portal (alternativa):**

1. Azure Portal → **Create a resource** → **API Management**  
2. Resource group: `rg-microservices`  
3. Name: único globalmente (ej. `apim-microservices-30754`)  
4. Organization name / Admin email: los tuyos  
5. Pricing tier: **Developer**  
6. Create → espera a que termine el despliegue  

Cuando esté listo, anota:
- **Gateway URL:** `https://<APIM_NAME>.azure-api.net`
- **Subscription key:** Portal → APIM → **Subscriptions** → Built-in all-access → Show/copy primary key (`Ocp-Apim-Subscription-Key`)

### Crear APIs en APIM con la CLI (comandos actualizados)

> Si aún no tienes la instancia APIM, completa primero la sección anterior.

La CLI **no** incluye `az apim backend create`. La URL del backend se configura al crear la API con `--service-url`.

> **Importante:** Solo crear la API **no basta**. Sin **operations**, APIM responde `{ "statusCode": 404, "message": "Resource not found" }`.

```bash
RESOURCE_GROUP=rg-microservices
APIM_NAME=apim-microservices-TU_NOMBRE   # el que creaste arriba
# URLs HTTPS de ngrok (sin path al final)
NGROK_PRODUCTS=https://TU-URL-NGROK-PRODUCTO.ngrok-free.app
NGROK_ORDERS=https://TU-URL-NGROK-ORDERS.ngrok-free.app

# API ProductService (APIM path: /products → backend /api/v1/Products)
az apim api create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --api-id product-api \
  --display-name "Product API" \
  --path products \
  --service-url "$NGROK_PRODUCTS/api/v1/Products" \
  --protocols https

az apim api operation create \
  --resource-group $RESOURCE_GROUP --service-name $APIM_NAME --api-id product-api \
  --operation-id get-all-products --display-name "Get all products" \
  --method GET --url-template "/"

az apim api operation create \
  --resource-group $RESOURCE_GROUP --service-name $APIM_NAME --api-id product-api \
  --operation-id get-product-by-id --display-name "Get product by id" \
  --method GET --url-template "/{id}" \
  --template-parameters name=id type=string required=true

# API OrderService (APIM path: /orders → backend /api/v1/Orders)
az apim api create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --api-id order-api \
  --display-name "Order API" \
  --path orders \
  --service-url "$NGROK_ORDERS/api/v1/Orders" \
  --protocols https

az apim api operation create \
  --resource-group $RESOURCE_GROUP --service-name $APIM_NAME --api-id order-api \
  --operation-id get-all-orders --display-name "Get all orders" \
  --method GET --url-template "/"

az apim api operation create \
  --resource-group $RESOURCE_GROUP --service-name $APIM_NAME --api-id order-api \
  --operation-id get-available-products --display-name "Get available products" \
  --method GET --url-template "/available-products"
```

Verificar operations:

```bash
az apim api operation list --resource-group $RESOURCE_GROUP --service-name $APIM_NAME --api-id product-api -o table
az apim api operation list --resource-group $RESOURCE_GROUP --service-name $APIM_NAME --api-id order-api -o table
```

Después, en el Portal de APIM puedes añadir más **operations** (POST, PUT, DELETE). Para probar:

```bash
curl -H "Ocp-Apim-Subscription-Key: <SUBSCRIPTION_KEY>" \
  "https://<APIM_NAME>.azure-api.net/products"
curl -H "Ocp-Apim-Subscription-Key: <SUBSCRIPTION_KEY>" \
  "https://<APIM_NAME>.azure-api.net/orders/available-products"
```

> Si ngrok reinicia, la URL cambia: actualiza con  
> `az apim api update ... --set serviceUrl="https://NUEVA-URL.ngrok-free.app/api/v1/Products"`.

#### Si las APIs ya existen pero dan 404 (`Resource not found`)

Causa habitual: la API se creó con `az apim api create` **sin** `az apim api operation create`. Comprueba:

```bash
az apim api list --resource-group $RESOURCE_GROUP --service-name $APIM_NAME -o table
az apim api operation list --resource-group $RESOURCE_GROUP --service-name $APIM_NAME --api-id product-api -o table
az apim api operation list --resource-group $RESOURCE_GROUP --service-name $APIM_NAME --api-id order-api -o table
```

Si la lista de operations está vacía, crea las operations (mismos comandos `az apim api operation create` de arriba) y corrige el `serviceUrl` para incluir el path del backend:

```bash
# Sustituye NGROK_* por tus URLs actuales de ngrok
az apim api update \
  --resource-group $RESOURCE_GROUP --service-name $APIM_NAME --api-id product-api \
  --set serviceUrl="$NGROK_PRODUCTS/api/v1/Products"

az apim api update \
  --resource-group $RESOURCE_GROUP --service-name $APIM_NAME --api-id order-api \
  --set serviceUrl="$NGROK_ORDERS/api/v1/Orders"
```

Mapeo resultante:

| Llamada a APIM | Backend (ngrok + servicio) |
|----------------|----------------------------|
| `GET .../products` | `GET {NGROK_PRODUCTS}/api/v1/Products` |
| `GET .../products/{id}` | `GET {NGROK_PRODUCTS}/api/v1/Products/{id}` |
| `GET .../orders` | `GET {NGROK_ORDERS}/api/v1/Orders` |
| `GET .../orders/available-products` | `GET {NGROK_ORDERS}/api/v1/Orders/available-products` |

Asegúrate también de que ProductService, OrderService y los túneles **ngrok** estén corriendo; si ngrok está caído, APIM devolverá error de backend (no el 404 de “Resource not found”).

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
- [ ] (Opcional) ngrok instalado; APIM creado; APIs **y operations** creadas (`az apim api create` + `az apim api operation create`); `curl` a `/products` y `/orders/available-products` responde 200.
- [ ] (Opcional) ProductService publicando en Service Bus vía user-secrets.

---

## Enlaces

- [Azure Functions (isolated worker)](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide)
- [Service Bus trigger](https://learn.microsoft.com/azure/azure-functions/functions-bindings-service-bus-trigger)
- [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local)
- [ngrok](https://ngrok.com/docs)
