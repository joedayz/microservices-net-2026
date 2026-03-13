# Módulo 9 – API Gateway (YARP)

## 🧠 Teoría

### ¿Qué es un API Gateway?

Un **API Gateway** es el punto de entrada único para las peticiones de los clientes hacia los microservicios. Centraliza:

- **Routing**: Enviar cada petición al microservicio correcto según la ruta o el host.
- **Cross-cutting**: Autenticación, límite de tasa (rate limiting), logging, métricas.
- **Desacoplamiento**: El cliente no conoce las URLs internas de cada servicio.

```
                    ┌─────────────────┐
  Cliente (Web/App) │   API Gateway   │  puerto 5000
                    │   (YARP)        │
                    └────────┬────────┘
                             │
           ┌─────────────────┼─────────────────┐
           ▼                 ▼                 ▼
   /api/v1/Products    /api/v1/Orders    (futuro: otros)
   → ProductService    → OrderService
   (5001)              (5003)
```

### Ocelot vs YARP

| Característica   | Ocelot              | YARP (Yet Another Reverse Proxy)   |
|------------------|---------------------|-------------------------------------|
| Mantenedor       | Comunidad           | Microsoft                            |
| Configuración    | JSON (archivo)      | JSON o código                        |
| Rendimiento      | Bueno               | Muy alto (proxy inverso nativo)     |
| .NET 6+          | Compatible          | Diseñado para .NET 6+                |
| Rate limiting    | Integrado           | Extensible / middleware              |
| Load balancing   | Sí                  | Sí (clusters con varios destinos)    |

**Recomendación para este taller:** YARP, por ser oficial de Microsoft y encajar bien con .NET 10.

### Conceptos YARP

- **Route**: Regla que relaciona un path (o criterio) con un **Cluster**.
- **Cluster**: Conjunto de **Destinations** (backend servers). Opcionalmente con load balancing.
- **Destination**: URL base de un servicio (ej. `http://localhost:5001`).

La configuración se suele definir en `appsettings.json` bajo la sección `ReverseProxy`.

### ¿Y si quiero usar Azure API Management (APIM)?

**Azure API Management (APIM)** es un servicio gestionado en la nube que actúa como API Gateway: publica, protege, transforma y analiza tus APIs. No lo hospedas tú; Azure se encarga de la disponibilidad y el escalado.

| Aspecto            | YARP (self-hosted)       | Azure API Management (APIM)        |
|--------------------|--------------------------|------------------------------------|
| Dónde corre        | Tu app/servidor (ej. 5000) | Servicio Azure (managed)           |
| Coste              | Solo el host             | Pago por uso o tier (Consumption, Developer, Basic, Standard, Premium) |
| Desarrollo local   | Muy sencillo             | Developer tier o emulador local    |
| Portal para devs   | No (solo tu Swagger)     | Sí (portal de desarrolladores)     |
| Políticas          | Código / middleware      | Políticas declarativas (rate limit, JWT, transformación XML/JSON) |
| Analytics          | Lo implementas tú       | Integrado (métricas, logs, trazabilidad) |
| Publicar APIs      | Config (JSON/código)     | Azure Portal, ARM, Bicep, Terraform |
| Autenticación      | La añades en tu app      | OAuth2, JWT, subscription keys, etc. |

**Cuándo usar YARP (este taller):**
- Aprendizaje, desarrollo local, equipos que quieren control total del código.
- Entornos on-premise o donde no quieres depender de Azure.
- Coste cero de gateway (solo infra donde corre).

**Cuándo usar APIM:**
- Producción en Azure con necesidad de portal de desarrolladores, suscripciones (API keys) y analytics.
- Políticas avanzadas (throttling, caching, transformación) sin escribir código.
- Publicar APIs hacia partners o terceros con documentación y versionado centralizado.

**Cómo usar APIM con estos microservicios (resumen):**

1. **Crear recurso APIM** en Azure (portal o CLI):
   ```bash
   az apim create --name apim-microservices --resource-group rg-microservices --publisher-name "TuNombre" --publisher-email "tu@email.com" -l eastus --sku-name Developer
   ```

2. **Definir backends** en APIM apuntando a tus servicios:
   - ProductService: URL base donde esté desplegado (ej. `https://productservice.azurewebsites.net` o la URL de tu App Service / AKS).
   - OrderService: igual con su URL.

3. **Crear APIs** en APIM:
   - API "Products" con path base `/api/v1/Products`, operaciones (GET, POST, etc.) y asociarla al backend de ProductService.
   - API "Orders" con path base `/api/v1/Orders` y asociarla al backend de OrderService.

4. **Configurar políticas** (opcional): rate limiting, validación JWT, set-backend-service, cache, etc.

5. **Probar** contra la URL de APIM (ej. `https://apim-microservices.azure-api.net/api/v1/Products`) en lugar de llamar directo a cada microservicio.

Para desarrollo local con APIM puedes usar el tier **Developer** (económico) o el **emulador local** de APIM si lo necesitas sin desplegar en la nube. La documentación oficial está en [Azure API Management](https://learn.microsoft.com/azure/api-management/).

En este módulo el laboratorio usa **YARP** para que todo funcione en tu máquina sin dependencias de Azure; más adelante puedes exponer los mismos backends a través de APIM en producción.

## 🧪 Laboratorios – Elegir opción

Puedes seguir una de estas dos opciones (o ambas):

| Opción | Gateway | Cuándo usarla |
|--------|---------|----------------|
| **Sección 1** | **YARP** (self-hosted, puerto 5000) | Desarrollo local, sin Azure, coste cero. |
| **Sección 2** | **Azure API Management (APIM)** | Producción en Azure, portal de desarrolladores, políticas y analytics. |

- Para **usar YARP** → sigue la [Sección 1 – YARP](#-laboratorio-9-opción-1-api-gateway-con-yarp).
- Para **cambiar a APIM** → ve a la [Sección 2 – APIM](#-laboratorio-9-opción-2-api-gateway-con-azure-api-management-apim).

---

## 🧪 Laboratorio 9 (opción 1): API Gateway con YARP

### Objetivo

- Crear un API Gateway con YARP en el puerto **5000**.
- Enrutar `/api/v1/Products` → ProductService (5001) y `/api/v1/Orders` → OrderService (5003).
- Probar que las peticiones pasan por el gateway y llegan a cada microservicio.

### Prerrequisitos

- Tener **ProductService** y **OrderService** implementados y corriendo (Módulo 7).
- ProductService escuchando en **5001** (REST) y opcionalmente **5002** (gRPC).
- OrderService escuchando en **5003**.

### Paso 1: Crear el proyecto Gateway

**Linux/macOS:**
```bash
cd src
mkdir -p Gateway
dotnet new webapi -n Gateway -o Gateway --no-https -f net10.0
cd Gateway
```

**PowerShell (Windows):**
```powershell
cd src
New-Item -ItemType Directory -Force -Path Gateway
dotnet new webapi -n Gateway -o Gateway --no-https -f net10.0
cd Gateway
```

**CMD (Windows):**
```cmd
cd src
mkdir Gateway
dotnet new webapi -n Gateway -o Gateway --no-https -f net10.0
cd Gateway
```

### Paso 2: Añadir el paquete YARP

```bash
dotnet add package Yarp.ReverseProxy
```

### Paso 3: Configurar `Program.cs`

Sustituir el contenido de `Program.cs` por algo equivalente a:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(5000, o => o.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1);
});

var app = builder.Build();

app.MapReverseProxy();

app.Run();
```

Así el gateway solo hace reverse proxy y lee rutas y clusters desde configuración.

### Paso 4: Configurar rutas en `appsettings.json`

Añadir la sección `ReverseProxy` con rutas y clusters. Ejemplo (los puertos deben coincidir con tus servicios):

```json
{
  "ReverseProxy": {
    "Routes": {
      "products": {
        "ClusterId": "productservice",
        "Match": { "Path": "/api/v1/Products/{**catch-all}" }
      },
      "products-base": {
        "ClusterId": "productservice",
        "Match": { "Path": "/api/v1/Products" }
      },
      "orders": {
        "ClusterId": "orderservice",
        "Match": { "Path": "/api/v1/Orders/{**catch-all}" }
      },
      "orders-base": {
        "ClusterId": "orderservice",
        "Match": { "Path": "/api/v1/Orders" }
      },
      "orders-available-products": {
        "ClusterId": "orderservice",
        "Match": { "Path": "/api/v1/Orders/available-products" }
      }
    },
    "Clusters": {
      "productservice": {
        "Destinations": {
          "destination1": { "Address": "http://localhost:5001" }
        }
      },
      "orderservice": {
        "Destinations": {
          "destination1": { "Address": "http://localhost:5003" }
        }
      }
    }
  }
}
```

Ajusta `Address` si tus servicios usan otros puertos o hosts.

### Paso 5: Añadir el Gateway a la solución

Desde la raíz del repo (o desde la carpeta que contiene la solución):

**Linux/macOS:**
```bash
cd src/Services/ProductService
dotnet sln add ../../Gateway/Gateway.csproj
```

**PowerShell / CMD:** Ajustar rutas si la solución está en otro sitio; el path relativo al `.sln` debe ser correcto (por ejemplo `..\..\Gateway\Gateway.csproj` si el sln está en `Services/ProductService`).

### Paso 6: Probar el gateway

1. **Levantar los backends:**
   - ProductService en 5001.
   - OrderService en 5003.

2. **Levantar el Gateway:**
   ```bash
   cd src/Gateway
   dotnet run
   ```
   Debe escuchar en **http://localhost:5010**.

3. **Peticiones vía gateway (mismo path que a los servicios):**

   **Productos (GET):**
   ```bash
   curl http://localhost:5010/api/v1/Products
   ```

   **Producto por ID:**
   ```bash
   curl http://localhost:5010/api/v1/Products/{id}
   ```

   **Órdenes – productos disponibles:**
   ```bash
   curl http://localhost:5010/api/v1/Orders/available-products
   ```

   **Órdenes (GET):**
   ```bash
   curl http://localhost:5010/api/v1/Orders
   ```

Si los backends responden correctamente cuando se llaman directo (5001 y 5003), y por el gateway (5000) obtienes las mismas respuestas, el módulo está funcionando.

### Paso 7 (opcional): Swagger de ProductService por el gateway

Si quieres servir la UI de Swagger de ProductService a través del gateway, añade una ruta que envíe `/swagger` (y subpaths) al cluster de ProductService, por ejemplo:

```json
"swagger-products": {
  "ClusterId": "productservice",
  "Match": { "Path": "/swagger/{**catch-all}" }
}
```

Luego podrías abrir `http://localhost:5010/swagger` y ver la documentación de ProductService (si ese servicio expone Swagger en `/swagger`).

### ✅ Checklist

- [ ] Proyecto Gateway creado con .NET 10 y paquete `Yarp.ReverseProxy`.
- [ ] `Program.cs` usa `AddReverseProxy()` y `LoadFromConfig(ReverseProxy)`.
- [ ] `appsettings.json` tiene rutas para Products y Orders y clusters con las URLs correctas.
- [ ] Gateway escucha en el puerto 5010.
- [ ] Con ProductService y OrderService en marcha, `curl http://localhost:5010/api/v1/Products` y `curl http://localhost:5010/api/v1/Orders/available-products` responden correctamente.

### 🐛 Solución de problemas

- **502 Bad Gateway:** El backend (5001 o 5003) no está corriendo o la URL del cluster es incorrecta. Comprueba que los servicios arrancan y que `Address` en `ReverseProxy:Clusters` es la correcta.
- **404 en una ruta:** Revisa que el `Path` de la ruta coincida exactamente con lo que pides (incluido `/api/v1/...`). El orden de las rutas puede influir; rutas más específicas suelen ir antes.
- **Puerto 5010 en uso:** Cambia el puerto en `ListenLocalhost(5010, ...)` y en las llamadas de prueba.

### Próximos pasos

- Añadir **rate limiting** (middleware o extensión).
- Configurar **load balancing** con varios destinos en un cluster.
- Añadir **health checks** de los destinos y exponer un endpoint de salud del gateway.

Documentación oficial: [YARP - Yet Another Reverse Proxy](https://microsoft.github.io/reverse-proxy/).

---

## 🧪 Laboratorio 9 (opción 2): API Gateway con Azure API Management (APIM)

**Sección 2** – Usa esta sección si quieres **cambiar a APIM** en lugar de (o además de) YARP. Necesitas una suscripción de Azure y Azure CLI (`az`) instalada y autenticada (`az login`).

### Objetivo

- Crear un recurso APIM en Azure.
- Registrar ProductService y OrderService como backends.
- Exponer dos APIs (Products y Orders) a través de APIM y probar las llamadas.

### Prerrequisitos

- Suscripción de Azure y permisos para crear recursos en un resource group.
- Azure CLI instalado: `az --version`.
- ProductService y OrderService desplegados y accesibles por URL (App Service, AKS, o túnel tipo **ngrok** si siguen en local).

### Paso 1: Crear el recurso API Management

Crea un resource group y una instancia APIM (SKU **Developer** para desarrollo; tiene coste reducido).

**Linux/macOS / PowerShell:**
```bash
# Variables (ajusta nombre y región)
RESOURCE_GROUP="rg-microservices"
APIM_NAME="apim-microservices-joedayz"   # debe ser único globalmente
LOCATION="eastus"

# Crear resource group si no existe
az group create --name $RESOURCE_GROUP --location $LOCATION

# Crear APIM (Developer = bajo coste, 1 nodo)
az apim create \
  --name $APIM_NAME \
  --resource-group $RESOURCE_GROUP \
  --publisher-name "TuNombre" \
  --publisher-email "tu@email.com" \
  --location $LOCATION \
  --sku-name Developer
```

**CMD (Windows):**
```cmd
set RESOURCE_GROUP=rg-microservices
set APIM_NAME=apim-microservices-joedayz
set LOCATION=eastus
az group create --name %RESOURCE_GROUP% --location %LOCATION%
az apim create --name %APIM_NAME% --resource-group %RESOURCE_GROUP% --publisher-name "TuNombre" --publisher-email "tu@email.com" --location %LOCATION% --sku-name Developer
```

Anota el **nombre de tu APIM**; la URL base será `https://<APIM_NAME>.azure-api.net`.

### Paso 2: Crear backends (ProductService y OrderService)

Un backend en APIM es la URL base del servicio. Sustituye las URLs por las tuyas (App Service, AKS, o una URL de ngrok si estás en local).

**ProductService (ejemplo: App Service o ngrok):**
```bash
az apim backend create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --backend-id "productservice" \
  --url "https://tu-productservice.azurewebsites.net" \
  --title "ProductService"
```

**OrderService:**
```bash
az apim backend create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --backend-id "orderservice" \
  --url "https://tu-orderservice.azurewebsites.net" \
  --title "OrderService"
```

Si usas **ngrok** en local (ej. `ngrok http 5001` y `ngrok http 5003`), pon aquí las URLs HTTPS que te da ngrok.

### Paso 3: Crear la API "Products" y operaciones

Crea una API en APIM con path base `/api/v1/Products` y enlázala al backend de ProductService.

**Desde Azure Portal (recomendado la primera vez):**

1. Ve a tu recurso APIM → **APIs** → **+ Add API** → **HTTP**.
2. **Display name:** `Products`, **Name:** `products`, **Web service URL:** `https://tu-productservice.azurewebsites.net` (o la URL de tu backend).
3. **API URL suffix:** `products` (las peticiones serán `https://<apim>.azure-api.net/products/...`; si quieres mantener `/api/v1/Products`, usa suffix `api/v1/Products`).
4. Crear la API.
5. En **Settings** de la API, en **Backend**, selecciona el backend "ProductService" (o la URL correcta).
6. Añade **Operations** (GET, POST, etc.) o importa desde OpenAPI/Swagger si tu ProductService expone `/swagger/v1/swagger.json`.

**Con Azure CLI** (ejemplo creando API y una operación GET):

```bash
# Crear API
az apim api create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --api-id "products-api" \
  --path "api/v1/Products" \
  --display-name "Products API" \
  --service-url "https://tu-productservice.azurewebsites.net"

# Crear operación GET (todos)
az apim api operation create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --api-id "products-api" \
  --operation-id "get-all-products" \
  --method GET \
  --url-template "/" \
  --display-name "Get all products"
```

Repite o añade más operaciones (GET por id, POST, etc.) según tu backend.

### Paso 4: Crear la API "Orders"

Igual que antes: crea una API con path base que coincida con tu OrderService (ej. `api/v1/Orders`) y apunta el backend a la URL de OrderService. Añade operaciones para `GET /`, `GET /available-products`, `GET /{id}`, `POST /`, etc.

**Portal:** APIs → Add API → configurar backend y operations.

**CLI (ejemplo):**
```bash
az apim api create \
  --resource-group $RESOURCE_GROUP \
  --service-name $APIM_NAME \
  --api-id "orders-api" \
  --path "api/v1/Orders" \
  --display-name "Orders API" \
  --service-url "https://tu-orderservice.azurewebsites.net"
```

### Paso 5: Suscripción y probar llamadas

1. **Obtener subscription key** (APIM → Subscriptions → default o crea una) y anota la clave.
2. Llamar a APIM en lugar del servicio directo:

```bash
# Sustituir <APIM_NAME> y <SUBSCRIPTION_KEY>
curl -H "Ocp-Apim-Subscription-Key: <SUBSCRIPTION_KEY>" \
  "https://<APIM_NAME>.azure-api.net/api/v1/Products"

curl -H "Ocp-Apim-Subscription-Key: <SUBSCRIPTION_KEY>" \
  "https://<APIM_NAME>.azure-api.net/api/v1/Orders/available-products"
```

Si configuraste el path suffix distinto (ej. solo `products`), usa la URL que te muestre APIM (ej. `https://<APIM_NAME>.azure-api.net/products`).

### Cambiar entre YARP y APIM

- **YARP:** El cliente llama a `http://localhost:5010/api/v1/Products`; no necesitas subscription key.
- **APIM:** El cliente llama a `https://<APIM_NAME>.azure-api.net/...` y envía el header `Ocp-Apim-Subscription-Key`. Puedes usar la misma app cliente y solo cambiar la **base URL** y los headers según el entorno (desarrollo = YARP, producción = APIM) vía configuración o variables de entorno.

### ✅ Checklist (opción APIM)

- [ ] Recurso APIM creado (SKU Developer o superior).
- [ ] Backends creados para ProductService y OrderService con URLs correctas.
- [ ] API Products creada y operaciones configuradas.
- [ ] API Orders creada y operaciones configuradas.
- [ ] Llamada de prueba con `curl` + subscription key devuelve 200 y datos correctos.

### Enlaces

- [Azure API Management - Documentación](https://learn.microsoft.com/azure/api-management/)
- [Crear una instancia de APIM](https://learn.microsoft.com/azure/api-management/get-started-create-service-instance)
- [Importar API desde OpenAPI](https://learn.microsoft.com/azure/api-management/import-api-from-oas-definition)
