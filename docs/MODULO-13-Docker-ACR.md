# Módulo 13 – Docker, ACR y Containerización

## 🧠 Teoría

### Resource Groups, VNets, Subnets

**Resource Groups:**
- Agrupación lógica de recursos en Azure
- Facilita gestión y facturación
- Lifecycle management

**Virtual Networks (VNets):**
- Redes privadas en Azure
- Aislamiento de recursos
- Peering entre VNets

**Subnets:**
- Segmentación de VNets
- Network security groups
- Control de tráfico

### Container Registry (ACR)

Azure Container Registry almacena imágenes Docker:
- Privado y seguro
- Integración con AKS
- Escaneo de vulnerabilidades
- Geo-replicación

### Docker Multi-Stage Build

Patrón de build en dos etapas que produce imágenes más pequeñas y seguras:

```
Stage 1 (SDK):           Stage 2 (Runtime):
┌─────────────────┐      ┌─────────────────┐
│ .NET SDK (~800MB)│      │ ASP.NET (~200MB) │
│ Restore          │      │ Solo DLLs        │
│ Build            │─────►│ Usuario no-root  │
│ Publish          │      │ ENTRYPOINT       │
└─────────────────┘      └─────────────────┘
```

---

## 🧪 Laboratorio 13

### Objetivo
Dockerizar los 3 microservicios y prepararlos para ACR:
1. Crear Dockerfiles multi-stage para cada servicio
2. Crear `.dockerignore` para optimizar builds
3. Crear `docker-compose.apps.yml` para correr todo containerizado
4. Documentar flujo de push a ACR

### Resumen de cambios realizados

| Archivo | Cambio |
|---------|--------|
| `.dockerignore` | **NUEVO** — Excluye `bin/`, `obj/`, `.vs/`, docs |
| `src/Services/ProductService/Dockerfile` | **NUEVO** — Multi-stage build, puertos 5001+5002 |
| `src/Services/OrderService/Dockerfile` | **NUEVO** — Multi-stage build, puerto 5003, copia proto compartido |
| `src/Gateway/Dockerfile` | **NUEVO** — Multi-stage build, puerto 5000 |
| `docker-compose.apps.yml` | **NUEVO** — Orquestación completa con infra + servicios |
| `ProductService/Program.cs` | `ListenLocalhost` → `ListenAnyIP` (para Docker) |
| `OrderService/Program.cs` | `ListenLocalhost` → `ListenAnyIP` (para Docker) |
| `Gateway/Program.cs` | `ListenLocalhost` → `ListenAnyIP` (para Docker) |

---

### Paso 1 — `.dockerignore`

Se creó `.dockerignore` en la raíz para excluir archivos innecesarios del build context:

```
**/bin/
**/obj/
**/.vs/
**/node_modules/
**/docs/
**/*.md
**/.git/
```

Esto reduce significativamente el tamaño del build context y acelera los builds.

---

### Paso 2 — Cambio de Kestrel: `ListenLocalhost` → `ListenAnyIP`

**Problema:** `ListenLocalhost` solo acepta conexiones desde `127.0.0.1`, que dentro de un contenedor Docker es solo el propio contenedor. Otros contenedores no pueden comunicarse con él.

**Solución:** Cambiar a `ListenAnyIP` que escucha en `0.0.0.0` (todas las interfaces).

```csharp
// ANTES (solo localhost — no funciona en Docker):
options.ListenLocalhost(5001, o => o.Protocols = HttpProtocols.Http1);

// DESPUÉS (todas las interfaces — funciona en Docker y local):
options.ListenAnyIP(5001, o => o.Protocols = HttpProtocols.Http1);
```

Este cambio se aplicó en los 3 servicios:
- **ProductService**: puertos 5001 (HTTP/1) y 5002 (HTTP/2 gRPC)
- **OrderService**: puerto 5003 (HTTP/1)
- **Gateway**: puerto 5000 (HTTP/1)

> **Nota:** `ListenAnyIP` también funciona en desarrollo local, por lo que el cambio es compatible.

---

### Paso 3 — Dockerfile de ProductService

```dockerfile
# Build stage — SDK completo para compilar
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ProductService/ProductService.csproj ProductService/
COPY ProductService/Protos/ ProductService/Protos/
RUN dotnet restore ProductService/ProductService.csproj

COPY ProductService/ ProductService/
RUN dotnet publish ProductService/ProductService.csproj -c Release -o /app/publish --no-restore

# Runtime stage — imagen mínima
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser
COPY --from=build /app/publish .
EXPOSE 5001 5002
ENV ASPNETCORE_ENVIRONMENT=Production
USER appuser
ENTRYPOINT ["dotnet", "ProductService.dll"]
```

**Build context:** `src/Services/` (porque necesita acceso a los Protos).

---

### Paso 4 — Dockerfile de OrderService

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia el proto de ProductService (referencia cruzada)
COPY OrderService/OrderService.csproj OrderService/
COPY ProductService/Protos/ ProductService/Protos/
RUN dotnet restore OrderService/OrderService.csproj

COPY OrderService/ OrderService/
RUN dotnet publish OrderService/OrderService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser
COPY --from=build /app/publish .
EXPOSE 5003
ENV ASPNETCORE_ENVIRONMENT=Production
USER appuser
ENTRYPOINT ["dotnet", "OrderService.dll"]
```

**Nota importante:** OrderService referencia `../ProductService/Protos/product.proto` en su `.csproj`. Por eso el build context debe ser `src/Services/` y no `src/Services/OrderService/`.

---

### Paso 5 — Dockerfile del Gateway

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Gateway.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser
COPY --from=build /app/publish .
EXPOSE 5000
ENV ASPNETCORE_ENVIRONMENT=Production
USER appuser
ENTRYPOINT ["dotnet", "Gateway.dll"]
```

**Build context:** `src/Gateway/` directamente (no tiene dependencias cruzadas).

---

### Paso 6 — docker-compose.apps.yml

Se creó `docker-compose.apps.yml` que extiende `docker-compose.yml` (infraestructura) y agrega los microservicios:

```yaml
# Uso:
docker compose -f docker-compose.yml -f docker-compose.apps.yml up --build
```

**Características:**
- Sobreescribe las URLs de conexión con nombres de servicio Docker:
  - `postgres` en vez de `localhost:5432`
  - `redis:6379` en vez de `localhost:6379`
  - `http://product-service:5001` en vez de `http://localhost:5001`
- Health checks con `curl` en cada servicio
- `depends_on` con `condition: service_healthy` para arranque ordenado
- Red `microservices` compartida

---

### Paso 7 — Flujo ACR (Azure Container Registry)

#### a) Crear ACR
```bash
# Crear resource group
az group create --name rg-microservices --location eastus2

# Crear ACR
az acr create --resource-group rg-microservices \
  --name myacrregistry --sku Basic
```

#### b) Build y Push
```bash
# Login al ACR
az acr login --name myacrregistry

# Build con Docker
docker build -t myacrregistry.azurecr.io/product-service:v1 \
  -f src/Services/ProductService/Dockerfile src/Services/

docker build -t myacrregistry.azurecr.io/order-service:v1 \
  -f src/Services/OrderService/Dockerfile src/Services/

docker build -t myacrregistry.azurecr.io/gateway:v1 \
  -f src/Gateway/Dockerfile src/Gateway/

# Push
docker push myacrregistry.azurecr.io/product-service:v1
docker push myacrregistry.azurecr.io/order-service:v1
docker push myacrregistry.azurecr.io/gateway:v1
```

#### c) Con Podman (alternativa)
```bash
podman login myacrregistry.azurecr.io

podman build -t myacrregistry.azurecr.io/product-service:v1 \
  -f src/Services/ProductService/Dockerfile src/Services/

podman push myacrregistry.azurecr.io/product-service:v1
```

#### d) Build directo en ACR (sin Docker local)
```bash
az acr build --registry myacrregistry \
  --image product-service:v1 \
  --file src/Services/ProductService/Dockerfile src/Services/
```

---

## 🧪 Cómo probar localmente

### 1. Build y run con Docker Compose

```bash
# Desde la raíz del proyecto
docker compose -f docker-compose.yml -f docker-compose.apps.yml up --build

# Esperar a que todos los servicios estén healthy (~30-60 segundos)
```

### 2. Verificar servicios

```bash
# Gateway (punto de entrada)
curl -s http://localhost:5010/health | jq

# ProductService directo
curl -s http://localhost:5001/health | jq

# OrderService directo
curl -s http://localhost:5003/health | jq

# Productos via Gateway
curl -s http://localhost:5010/api/v1/Products | jq

# Productos via OrderService
curl -s http://localhost:5003/api/v1/Orders/available-products | jq
```

### 3. Build individual (para debug)

```bash
# Build solo ProductService
docker build -t product-service:dev \
  -f src/Services/ProductService/Dockerfile src/Services/

# Correr standalone (necesita PostgreSQL en host)
docker run --rm -p 5001:5001 \
  -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;Port=5432;Database=microservices_db;Username=postgres;Password=postgres" \
  -e ConnectionStrings__Redis="host.docker.internal:6379" \
  -e Auth__Provider=local \
  -e AppConfig__Enabled=false \
  product-service:dev
```

### 4. Detener todo

```bash
docker compose -f docker-compose.yml -f docker-compose.apps.yml down
# Con volúmenes (borra datos):
docker compose -f docker-compose.yml -f docker-compose.apps.yml down -v
```

---

## 📊 Arquitectura containerizada

```
                    ┌──────────────────────────────────────┐
                    │           Docker Network              │
                    │           (microservices)              │
  Puerto 5000  ┌───┴───────┐                               │
  ────────────►│  Gateway   │                               │
               │  (YARP)    │                               │
               └──┬────┬────┘                               │
                  │    │                                    │
        ┌─────────┘    └──────────┐                        │
        ▼                         ▼                        │
  ┌──────────────┐    ┌──────────────┐                     │
  │  Product     │    │  Order       │                     │
  │  Service     │    │  Service     │                     │
  │  :5001 :5002 │◄───│  :5003       │                     │
  └───┬──┬───────┘    └──────────────┘                     │
      │  │                                                 │
      │  └──────────┐                                      │
      ▼             ▼                                      │
  ┌────────┐  ┌─────────┐  ┌───────────┐                  │
  │Postgres│  │  Redis   │  │ RabbitMQ  │                  │
  │  :5432 │  │  :6379   │  │   :5672   │                  │
  └────────┘  └─────────┘  └───────────┘                  │
                    └──────────────────────────────────────┘
```

---

## 📦 Seguridad de imágenes

Las imágenes siguen buenas prácticas de seguridad:
- **Multi-stage build**: la imagen final no contiene el SDK ni código fuente
- **Usuario no-root**: `USER appuser` evita ejecución como root
- **Imagen base oficial**: `mcr.microsoft.com/dotnet/aspnet:10.0` (Microsoft)
- **`.dockerignore`**: evita incluir archivos sensibles en el build context

---

## 📎 Referencias

- [Dockerize ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images)
- [Azure Container Registry](https://learn.microsoft.com/en-us/azure/container-registry/)
- [Docker Compose Override](https://docs.docker.com/compose/extends/)
- [Multi-stage builds](https://docs.docker.com/build/building/multi-stage/)

Ver documentación oficial de ACR y AKS.

