# Módulo 12 – Kubernetes (AKS) y Balanceo de carga

## 🧠 Teoría

### Load Balancer vs Application Gateway

**Load Balancer (L4):**
- Balanceo a nivel de red (TCP/UDP)
- Basado en IP y puerto
- Más rápido, menos inteligente
- Ideal para tráfico interno entre servicios

**Application Gateway (L7):**
- Balanceo a nivel de aplicación (HTTP/HTTPS)
- Basado en URL, headers, cookies
- SSL termination
- WAF (Web Application Firewall) integrado

### Kubernetes Service Types

```
                Internet
                    │
              ┌─────▼──────┐
              │   Ingress   │  ← L7 routing (path, host)
              │   (nginx)   │
              └──────┬──────┘
                     │
        ┌────────────┼────────────┐
        ▼            ▼            ▼
  ┌───────────┐ ┌──────────┐ ┌──────────┐
  │LoadBalancer│ │ NodePort │ │ClusterIP │
  │ (L4, ext) │ │(ext,port)│ │(internal)│
  └───────────┘ └──────────┘ └──────────┘
```

**ClusterIP** (por defecto):
- Solo accesible dentro del cluster
- Kubernetes asigna una IP virtual
- Ideal para comunicación entre microservicios

**NodePort:**
- Expone un puerto (30000-32767) en cada nodo del cluster
- Acceso externo básico sin cloud provider

**LoadBalancer:**
- Provisiona un balanceador de carga externo (Azure LB, AWS ELB)
- IP pública dedicada
- Se usa para el Gateway/punto de entrada

**Ingress:**
- Routing HTTP/HTTPS inteligente
- SSL termination
- Path-based routing (`/api/v1/Products` → ProductService)
- Host-based routing (`api.example.com` → servicio)

### Probes en Kubernetes

Kubernetes usa probes para saber si un pod está saludable:

| Probe | Propósito | ¿Qué pasa si falla? |
|-------|-----------|---------------------|
| **readinessProbe** | ¿Puede recibir tráfico? | Se saca del Service (no recibe requests) |
| **livenessProbe** | ¿Está vivo el proceso? | Se reinicia el contenedor |
| **startupProbe** | ¿Ya arrancó? | No ejecuta liveness/readiness hasta que pase |

---

## 🧪 Laboratorio 12

### Objetivo
1. Desplegar los microservicios en Kubernetes (AKS o local con Docker Desktop/Podman):
1. Crear manifiestos para infraestructura (PostgreSQL, Redis, RabbitMQ)
2. Crear manifiestos para los 3 microservicios
3. Configurar Ingress para routing
4. Usar los Health Checks del Módulo 11 como probes
5. Script de despliegue automatizado

### Resumen de archivos creados

| Archivo | Contenido |
|---------|-----------|
| `infrastructure/kubernetes/namespace.yaml` | Namespace `microservices` |
| `infrastructure/kubernetes/postgres.yaml` | Deployment + Service + PVC + ConfigMap + Secret |
| `infrastructure/kubernetes/redis.yaml` | Deployment + Service |
| `infrastructure/kubernetes/rabbitmq.yaml` | Deployment + Service |
| `infrastructure/kubernetes/product-service.yaml` | Deployment (2 replicas) + Service + ConfigMap |
| `infrastructure/kubernetes/order-service.yaml` | Deployment (2 replicas) + Service + ConfigMap |
| `infrastructure/kubernetes/gateway.yaml` | Deployment (2 replicas) + Service (LoadBalancer) + ConfigMap |
| `infrastructure/kubernetes/ingress.yaml` | Ingress con nginx para routing HTTP |
| `infrastructure/kubernetes/deploy.sh` | Script de despliegue automatizado |

---

### Paso 1 — Namespace

Todos los recursos se agrupan en el namespace `microservices`:

```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: microservices
```

```bash
kubectl apply -f infrastructure/kubernetes/namespace.yaml
```

---

### Paso 2 — Infraestructura (PostgreSQL, Redis, RabbitMQ)

#### PostgreSQL
- **Deployment** con imagen `postgres:16-alpine`
- **PersistentVolumeClaim** de 1Gi para persistencia de datos
- **ConfigMap** para `POSTGRES_DB` y `POSTGRES_USER`
- **Secret** para `POSTGRES_PASSWORD` y el connection string completo
- **Readiness/Liveness probes** con `pg_isready`

```yaml
# Extracto de postgres.yaml
readinessProbe:
  exec:
    command: ["pg_isready", "-U", "postgres"]
  initialDelaySeconds: 5
  periodSeconds: 10
```

#### Redis
- **Deployment** con imagen `redis:7-alpine`
- **Service** ClusterIP en puerto 6379
- Probes con `redis-cli ping`

#### RabbitMQ
- **Deployment** con imagen `rabbitmq:4-management-alpine`
- Dos puertos: 5672 (AMQP) y 15672 (Management UI)
- Probes con `rabbitmq-diagnostics -q ping` (startup/readiness/liveness)

---

### Paso 3 — ProductService (Deployment + Service)

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: product-service
  namespace: microservices
spec:
  replicas: 2    # ← Alta disponibilidad
  selector:
    matchLabels:
      app: product-service
  template:
    spec:
      containers:
        - name: product-service
          image: ${ACR_NAME}.azurecr.io/product-service:latest
          ports:
            - containerPort: 5001    # REST
            - containerPort: 5002    # gRPC
          env:
            - name: ConnectionStrings__DefaultConnection
              valueFrom:
                secretKeyRef:
                  name: postgres-secret
                  key: CONNECTION_STRING
          # Usa los health checks del Módulo 11
          readinessProbe:
            httpGet:
              path: /health          # ← Verifica PostgreSQL + Redis
              port: 5001
          livenessProbe:
            httpGet:
              path: /health/live     # ← Solo verifica que el proceso responde
              port: 5001
```

**Puntos clave:**
- **2 réplicas** para alta disponibilidad y balanceo de carga
- **ConfigMap** con variables de entorno no sensibles
- **Secret reference** para el connection string de PostgreSQL
- **readinessProbe** usa `/health` (verifica DB + Redis)
- **livenessProbe** usa `/health/live` (solo verifica que el proceso responde)
- **Service ClusterIP** porque solo necesita acceso interno

---

### Paso 4 — OrderService (Deployment + Service)

```yaml
spec:
  containers:
    - name: order-service
      env:
        # Comunicación inter-servicio via nombre de Service K8s
        - name: ProductService__HttpUrl
          value: "http://product-service:5001"
        - name: ProductService__GrpcUrl
          value: "http://product-service:5002"
```

**¿Cómo se comunican los servicios?** Kubernetes DNS resuelve `product-service` al ClusterIP del Service. OrderService llama a `http://product-service:5001` y Kubernetes balancea entre las 2 réplicas automáticamente.

---

### Paso 5 — Gateway (Deployment + Service LoadBalancer)

```yaml
apiVersion: v1
kind: Service
metadata:
  name: gateway
spec:
  type: LoadBalancer   # ← IP pública externa
  ports:
    - port: 80          # Puerto externo
      targetPort: 5000  # Puerto del contenedor
```

El Gateway es el **único servicio expuesto externamente** via LoadBalancer. YARP enruta las peticiones a los servicios internos.

**ConfigMap** sobreescribe las URLs de YARP para apuntar a los Services de K8s:
```yaml
data:
  ReverseProxy__Clusters__productservice__Destinations__destination1__Address: "http://product-service:5001"
  ReverseProxy__Clusters__orderservice__Destinations__destination1__Address: "http://order-service:5003"
```

---

### Paso 6 — Ingress (opcional, alternativa al LoadBalancer)

El Ingress proporciona routing L7 más sofisticado:

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: microservices-ingress
  annotations:
    nginx.ingress.kubernetes.io/ssl-redirect: "false"
spec:
  ingressClassName: nginx
  rules:
    - host: microservices.local
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: gateway
                port:
                  number: 80
```

Para usar Ingress se necesita un Ingress Controller (ej: NGINX):
```bash
# Instalar NGINX Ingress Controller
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.12.0/deploy/static/provider/cloud/deploy.yaml
```

---

### Paso 7 — Script de despliegue

```bash
# Despliegue completo con ACR
./infrastructure/kubernetes/deploy.sh myacrjoedayzregistry

# Despliegue local (sin ACR, para Docker Desktop o Kind)
./infrastructure/kubernetes/deploy.sh
```

El script:
1. Crea el namespace
2. Despliega infraestructura (PostgreSQL, Redis, RabbitMQ)
3. Espera a que la infraestructura esté lista
4. Despliega los microservicios (reemplazando `${ACR_NAME}` si se proporcionó)
5. Configura el Ingress

---

## 🧪 Cómo probar

### Opción A — AKS (Azure)

```bash
# 1. Crear Resource Group (si no existe)
az group create --name rg-microservices --location eastus

# 2. Crear Azure Container Registry (ACR)
az acr create \
  --resource-group rg-microservices \
  --name myacrjoedayzregistry \
  --sku Basic

# 3. Iniciar sesión en el ACR
az acr login --name myacrjoedayzregistry

# 4. Build y push de imágenes al ACR (Docker o Podman)
# Nota: --platform linux/amd64 genera imágenes para clusters AKS (amd64).
# Los Dockerfiles usan FROM --platform=$BUILDPLATFORM para compilar nativamente en Apple Silicon.

# ===== Opción Docker =====
docker build --platform linux/amd64 -t myacrjoedayzregistry.azurecr.io/product-service:latest \
  -f src/Services/ProductService/Dockerfile src/Services/
docker build --platform linux/amd64 -t myacrjoedayzregistry.azurecr.io/order-service:latest \
  -f src/Services/OrderService/Dockerfile src/Services/
docker build --platform linux/amd64 -t myacrjoedayzregistry.azurecr.io/gateway:latest \
  -f src/Gateway/Dockerfile src/Gateway/

docker push myacrjoedayzregistry.azurecr.io/product-service:latest
docker push myacrjoedayzregistry.azurecr.io/order-service:latest
docker push myacrjoedayzregistry.azurecr.io/gateway:latest

# ===== Opción Podman =====
# Login al ACR con token (evita prompt interactivo de username/password)
TOKEN=$(az acr login --name myacrjoedayzregistry --expose-token --output tsv --query accessToken)
echo "$TOKEN" | podman login myacrjoedayzregistry.azurecr.io \
  -u 00000000-0000-0000-0000-000000000000 \
  --password-stdin

podman build --platform linux/amd64 -t myacrjoedayzregistry.azurecr.io/product-service:latest \
  -f src/Services/ProductService/Dockerfile src/Services/
podman build --platform linux/amd64 -t myacrjoedayzregistry.azurecr.io/order-service:latest \
  -f src/Services/OrderService/Dockerfile src/Services/
podman build --platform linux/amd64 -t myacrjoedayzregistry.azurecr.io/gateway:latest \
  -f src/Gateway/Dockerfile src/Gateway/

podman push myacrjoedayzregistry.azurecr.io/product-service:latest
podman push myacrjoedayzregistry.azurecr.io/order-service:latest
podman push myacrjoedayzregistry.azurecr.io/gateway:latest

# 5. Crear cluster AKS (vinculado al ACR)
az aks create \
  --resource-group rg-microservices \
  --name aks-microservices \
  --node-count 2 \
  --enable-managed-identity \
  --attach-acr myacrjoedayzregistry \
  --generate-ssh-keys

# 6. Obtener credenciales de kubectl
az aks get-credentials --resource-group rg-microservices --name aks-microservices

# 7. Verificar conexión
kubectl get nodes

# 8. Desplegar (con imágenes del ACR)
./infrastructure/kubernetes/deploy.sh myacrjoedayzregistry

# 9. Obtener IP externa del Gateway (puede tardar 1-2 minutos)
kubectl get svc gateway -n microservices -w
# EXTERNAL-IP: 20.xxx.xxx.xxx

# 10. Probar
curl http://20.xxx.xxx.xxx/api/v1/Products | jq
curl http://20.xxx.xxx.xxx/health | jq
```

> **Nota:** El nombre del ACR (`myacrjoedayzregistry`) debe ser único globalmente en Azure. Cámbialo por uno propio si ya está tomado (ej: `myname2025acr`). El flag `--attach-acr` en el paso 5 permite que AKS haga pull de imágenes desde el ACR sin configuración adicional.

#### Links rápidos para probar servicios en AKS

Primero obtén la IP pública del Gateway:

```bash
AKS_IP=$(kubectl get svc gateway -n microservices -o jsonpath='{.status.loadBalancer.ingress[0].ip}')
echo "$AKS_IP"
```

Usa estos links (reemplaza `{AKS_IP}` por la IP obtenida):

- Health del Gateway: `http://{AKS_IP}/health`
- Productos (v1): `http://{AKS_IP}/api/v1/Products`
- Órdenes (v1): `http://{AKS_IP}/api/v1/Orders`
- Productos disponibles desde OrderService: `http://{AKS_IP}/api/v1/Orders/available-products`
- Swagger de ProductService (enrutado por YARP): `http://{AKS_IP}/swagger`

Prueba rápida por terminal:

```bash
curl -s "http://$AKS_IP/health" | jq
curl -s "http://$AKS_IP/api/v1/Products" | jq
curl -s "http://$AKS_IP/api/v1/Orders" | jq
curl -s "http://$AKS_IP/api/v1/Orders/available-products" | jq
```

#### Destruir todo lo creado en AKS (cleanup)

> Ejecuta esta sección cuando termines el laboratorio para evitar costos.

Opción 1 — Destruir recursos de Kubernetes y luego AKS/ACR:

```bash
# 1. Borrar recursos del laboratorio dentro del cluster
kubectl delete namespace microservices

# 2. Borrar cluster AKS
az aks delete --resource-group rg-microservices --name aks-microservices --yes --no-wait

# 3. Borrar ACR
az acr delete --resource-group rg-microservices --name myacrjoedayzregistry --yes
```

Opción 2 — Destruir TODO el Resource Group (incluye AKS, ACR y cualquier otro recurso):

```bash
az group delete --name rg-microservices --yes --no-wait
```

Verificar estado de eliminación:

```bash
az group show --name rg-microservices -o table
az aks list -o table
az acr list -o table
```

### Opción B — Local con Docker Desktop (Kubernetes integrado)

```bash
# 1. Activar Kubernetes en Docker Desktop:
#    Docker Desktop → Settings → Kubernetes → Enable Kubernetes → Apply & Restart
#    Esperar a que el indicador de Kubernetes esté en verde (esquina inferior izquierda)

# 2. Verificar que kubectl apunta al cluster local
kubectl config use-context docker-desktop
kubectl get nodes
# NAME             STATUS   ROLES           AGE   VERSION
# docker-desktop   Ready    control-plane   ...   v1.x.x

# 3. Build de imágenes locales (Docker Desktop las hace disponibles en el cluster automáticamente)
# Nota: --platform linux/amd64 genera imágenes amd64. Opcional si tu cluster K8s es ARM64.
# Los Dockerfiles usan FROM --platform=$BUILDPLATFORM para compilar nativamente en Apple Silicon.
docker build --platform linux/amd64 -t product-service:latest \
  -f src/Services/ProductService/Dockerfile src/Services/
docker build --platform linux/amd64 -t order-service:latest \
  -f src/Services/OrderService/Dockerfile src/Services/
docker build --platform linux/amd64 -t gateway:latest \
  -f src/Gateway/Dockerfile src/Gateway/

# 4. Desplegar (sin ACR = usa imágenes locales)
./infrastructure/kubernetes/deploy.sh

# 5. Acceder al Gateway
kubectl port-forward svc/gateway 5010:80 -n microservices
curl http://localhost:5010/health | jq
```

> **Nota:** Docker Desktop comparte el daemon con Kubernetes, así que las imágenes construidas con `docker build` están automáticamente disponibles para los pods sin necesidad de un registry externo.

### Opción C — Local con Podman + Kubernetes

Podman Desktop también incluye soporte para Kubernetes (via Podman machine + Kind):

```bash
# 1. Instalar Podman Desktop (si no lo tienes)
brew install --cask podman-desktop

# 2. Iniciar Podman machine
podman machine init --cpus 4 --memory 4096
podman machine start

# 3. Instalar Kind (Kubernetes in Docker, funciona con Podman)
brew install kind

# 4. Crear cluster Kind usando Podman como provider
KIND_EXPERIMENTAL_PROVIDER=podman kind create cluster --name microservices

# 5. Verificar conexión
# ⚠️  IMPORTANTE — el contexto de kubectl con Podman + Kind se llama "kind-microservices"
#   (Kind antepone "kind-" al nombre del cluster). Si ves "connection refused", el kubeconfig
#   tiene una entrada stale con un puerto antiguo; regenera con:
KIND_EXPERIMENTAL_PROVIDER=podman kind export kubeconfig --name microservices
kubectl config use-context kind-microservices
kubectl get nodes
# NAME                          STATUS   ROLES           AGE
# microservices-control-plane   Ready    control-plane   ...

# 6. Build de imágenes con Podman
# ⚠️  Podman etiqueta las imágenes locales con prefijo "localhost/" automáticamente.
#   Usa ese mismo prefijo en el build para que coincida con lo que se cargará en Kind.
podman build --platform linux/amd64 -t localhost/product-service:latest \
  -f src/Services/ProductService/Dockerfile src/Services/
podman build --platform linux/amd64 -t localhost/order-service:latest \
  -f src/Services/OrderService/Dockerfile src/Services/
podman build --platform linux/amd64 -t localhost/gateway:latest \
  -f src/Gateway/Dockerfile src/Gateway/

# 7. Cargar imágenes al cluster Kind
# ⚠️  Kind no comparte el daemon con Podman, hay que cargar las imágenes explícitamente.
#   "kind load docker-image" no detecta imágenes de Podman directamente;
#   usa el método image-archive (exportar a tar + cargar) que es el más estable.
podman save -o /tmp/product-service-latest.tar localhost/product-service:latest
podman save -o /tmp/order-service-latest.tar localhost/order-service:latest
podman save -o /tmp/gateway-latest.tar localhost/gateway:latest

# ⚠️  El flag --name usa el nombre Kind del cluster ("microservices"), NO el contexto kubectl ("kind-microservices")
KIND_EXPERIMENTAL_PROVIDER=podman kind load image-archive /tmp/product-service-latest.tar --name microservices
KIND_EXPERIMENTAL_PROVIDER=podman kind load image-archive /tmp/order-service-latest.tar --name microservices
KIND_EXPERIMENTAL_PROVIDER=podman kind load image-archive /tmp/gateway-latest.tar --name microservices

# 8. Desplegar
# ⚠️  SIEMPRE pasar LOCAL_IMAGE_PREFIX=localhost/ para que los manifiestos
#   usen "localhost/product-service:latest" en lugar de buscarlo en Docker Hub.
LOCAL_IMAGE_PREFIX=localhost/ ./infrastructure/kubernetes/deploy.sh

# 9. Acceder al Gateway
kubectl port-forward svc/gateway 5010:80 -n microservices
curl http://localhost:5010/health | jq
```

> **Problemas comunes con Podman + Kind:**
>
> | Síntoma | Causa | Solución |
> |---------|-------|----------|
> | `connection refused` en `kubectl get nodes` | Contexto stale en kubeconfig (puerto muerto) | `KIND_EXPERIMENTAL_PROVIDER=podman kind export kubeconfig --name <cluster>` |
> | `ImagePullBackOff` / `ErrImagePull` desde Docker Hub | Imágenes sin prefijo, Kubernetes intenta pull externo | Usar `LOCAL_IMAGE_PREFIX=localhost/ ./deploy.sh` |
> | `kind load docker-image` → "not present locally" | Kind no accede al daemon de Podman directamente | Usar `podman save` + `kind load image-archive` |
> | `kind load --name kind-microservices` → "no nodes found" | El nombre Kind es `microservices`, no `kind-microservices` | Usar `--name microservices` (sin prefijo `kind-`) |

### Verificar estado del cluster

```bash
# Ver todos los pods (todos deben ser 1/1 Running)
kubectl get pods -n microservices -o wide

# Ver todos los recursos (deployments, services, ingress)
kubectl get all -n microservices

# Ver logs de un servicio
kubectl logs -l app=product-service -n microservices --tail=50
kubectl logs -l app=order-service -n microservices --tail=50

# Describir un pod con problemas
kubectl describe pod -l app=product-service -n microservices

# Escalar manualmente
kubectl scale deployment product-service --replicas=3 -n microservices
```

---

### Validar endpoints — ProductService y OrderService

> **Requisito:** Todos los pods en `1/1 Running`. Abre **terminales separadas** para los port-forward.



**Alternativa (1 sola terminal): scripts listos**

# Bash / Zsh
curl -s http://localhost:5010/health | jq
curl -s http://localhost:5001/health | jq
curl -s http://localhost:5003/health | jq

curl -s http://localhost:5001/health/live
curl -s http://localhost:5003/health/live> Si estas en Bash/Zsh usa `port-forward-all.sh`; si estas en PowerShell usa `port-forward-all.ps1`.

```bash
# macOS / Linux
./infrastructure/kubernetes/port-forward-all.sh

# Opcional: namespace distinto
./infrastructure/kubernetes/port-forward-all.sh microservices
```

```powershell
# Windows PowerShell
./infrastructure/kubernetes/port-forward-all.ps1

# Opcional: namespace distinto
./infrastructure/kubernetes/port-forward-all.ps1 -Namespace microservices
```

---

#### 1. Health checks

```bash
# macOS / Linux
curl -s http://localhost:5010/health | jq          # Gateway (JSON)
curl -s http://localhost:5001/health | jq          # ProductService (JSON)
curl -s http://localhost:5003/health | jq          # OrderService (JSON)
curl -s http://localhost:5001/health/live          # ProductService liveness (text/plain)
curl -s http://localhost:5003/health/live          # OrderService liveness (text/plain)

# Windows (PowerShell)
Invoke-RestMethod http://localhost:5010/health | ConvertTo-Json
Invoke-RestMethod http://localhost:5001/health | ConvertTo-Json
Invoke-RestMethod http://localhost:5003/health | ConvertTo-Json
Invoke-WebRequest http://localhost:5001/health/live | Select-Object -ExpandProperty Content
Invoke-WebRequest http://localhost:5003/health/live | Select-Object -ExpandProperty Content
```

---

#### 2. Obtener JWT token (requerido para operaciones de escritura)

Los endpoints GET son anónimos. POST, PUT y DELETE requieren rol `Admin`.

```bash
# macOS / Linux — obtener token de ProductService
TOKEN=$(curl -s -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}' | jq -r '.token')

echo "Token: $TOKEN"

# Windows (PowerShell)
$response = Invoke-RestMethod -Method Post http://localhost:5001/api/auth/login `
  -ContentType "application/json" `
  -Body '{"username":"admin","password":"admin123"}'
$TOKEN = $response.token
Write-Host "Token: $TOKEN"
```

> **Usuarios disponibles:** `admin/admin123` (Admin), `reader/reader123` (Reader), `user/user123` (User)

---

#### 3. ProductService — CRUD completo

```bash
# --- GET todos los productos (anónimo) ---
curl -s http://localhost:5001/api/v1/Products | jq
# También via Gateway:
curl -s http://localhost:5010/api/v1/Products | jq

# --- POST crear producto (requiere token Admin) ---
PRODUCT=$(curl -s -X POST http://localhost:5001/api/v1/Products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "name": "Laptop Pro",
    "description": "High-performance laptop",
    "price": 1299.99,
    "stock": 10
  }')
echo $PRODUCT | jq
PRODUCT_ID=$(echo $PRODUCT | jq -r '.id')
echo "Product ID: $PRODUCT_ID"

# --- POST segundo producto (para probar la orden con múltiples items) ---
PRODUCT2=$(curl -s -X POST http://localhost:5001/api/v1/Products \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "name": "Mouse Inalámbrico",
    "description": "Mouse ergonómico inalámbrico",
    "price": 49.99,
    "stock": 50
  }')
PRODUCT_ID2=$(echo $PRODUCT2 | jq -r '.id')
echo "Product 2 ID: $PRODUCT_ID2"

# --- GET producto por ID (anónimo) ---
curl -s http://localhost:5001/api/v1/Products/$PRODUCT_ID | jq

# --- PUT actualizar producto (requiere token Admin) ---
curl -s -X PUT http://localhost:5001/api/v1/Products/$PRODUCT_ID \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "name": "Laptop Pro Max",
    "description": "High-performance laptop - updated",
    "price": 1499.99,
    "stock": 8
  }'
# → HTTP 204 No Content

# --- Verificar actualización ---
curl -s http://localhost:5001/api/v1/Products/$PRODUCT_ID | jq
```

```powershell
# Windows (PowerShell)

# GET todos
Invoke-RestMethod http://localhost:5001/api/v1/Products | ConvertTo-Json

# POST crear producto
$product = Invoke-RestMethod -Method Post http://localhost:5001/api/v1/Products `
  -ContentType "application/json" `
  -Headers @{Authorization="Bearer $TOKEN"} `
  -Body '{"name":"Laptop Pro","description":"High-performance laptop","price":1299.99,"stock":10}'
$PRODUCT_ID = $product.id
Write-Host "Product ID: $PRODUCT_ID"

# GET por ID
Invoke-RestMethod "http://localhost:5001/api/v1/Products/$PRODUCT_ID" | ConvertTo-Json
```

---

#### 4. OrderService — CRUD completo

```bash
# --- GET productos disponibles (llama a ProductService internamente) ---
# Valida que OrderService puede comunicarse con ProductService via HTTP/gRPC
curl -s http://localhost:5003/api/v1/Orders/available-products | jq
# También via Gateway:
curl -s http://localhost:5010/api/v1/Orders/available-products | jq

# --- GET todas las órdenes (anónimo) ---
curl -s http://localhost:5003/api/v1/Orders | jq

# --- POST crear orden (requiere token Admin, usa IDs obtenidos arriba) ---
ORDER=$(curl -s -X POST http://localhost:5003/api/v1/Orders \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{
    \"customerName\": \"Joe Diaz\",
    \"items\": [
      { \"productId\": \"$PRODUCT_ID\",  \"quantity\": 2 },
      { \"productId\": \"$PRODUCT_ID2\", \"quantity\": 1 }
    ]
  }")
echo $ORDER | jq
ORDER_ID=$(echo $ORDER | jq -r '.id')
echo "Order ID: $ORDER_ID"

# --- GET orden por ID ---
curl -s http://localhost:5003/api/v1/Orders/$ORDER_ID | jq

# --- DELETE orden (requiere token Admin) ---
curl -s -X DELETE http://localhost:5003/api/v1/Orders/$ORDER_ID \
  -H "Authorization: Bearer $TOKEN"
# → HTTP 204 No Content

# --- Verificar que se eliminó ---
curl -s http://localhost:5003/api/v1/Orders | jq
```

```powershell
# Windows (PowerShell)

# GET productos disponibles (comunicación inter-servicio)
Invoke-RestMethod http://localhost:5003/api/v1/Orders/available-products | ConvertTo-Json -Depth 3

# POST crear orden
$orderBody = @{
  customerName = "Joe Diaz"
  items = @(
    @{ productId = $PRODUCT_ID; quantity = 2 }
  )
} | ConvertTo-Json
$order = Invoke-RestMethod -Method Post http://localhost:5003/api/v1/Orders `
  -ContentType "application/json" `
  -Headers @{Authorization="Bearer $TOKEN"} `
  -Body $orderBody
$ORDER_ID = $order.id
Write-Host "Order ID: $ORDER_ID"

# GET orden por ID
Invoke-RestMethod "http://localhost:5003/api/v1/Orders/$ORDER_ID" | ConvertTo-Json -Depth 3
```

---

#### 5. Validar rutas via Gateway (YARP)

El Gateway solo enruta los paths configurados en `appsettings.json`:

| Ruta en Gateway | Destino |
|----------------|---------|
| `GET /api/v1/Products` | ProductService:5001 |
| `GET /api/v1/Products/{id}` | ProductService:5001 |
| `GET /api/v1/Orders` | OrderService:5003 |
| `GET /api/v1/Orders/available-products` | OrderService:5003 |
| `GET /swagger/{**}` | ProductService:5001 |

> **Nota:** `/api/auth/login` no está enrutado en el Gateway. El token se obtiene directamente de cada servicio.

```bash
# macOS / Linux — todo via Gateway (port-forward svc/gateway 5010:80)
curl -s http://localhost:5010/api/v1/Products | jq
curl -s http://localhost:5010/api/v1/Products/$PRODUCT_ID | jq
curl -s http://localhost:5010/api/v1/Orders | jq
curl -s http://localhost:5010/api/v1/Orders/available-products | jq

# Windows (PowerShell)
Invoke-RestMethod "http://localhost:5010/api/v1/Products" | ConvertTo-Json
Invoke-RestMethod "http://localhost:5010/api/v1/Orders" | ConvertTo-Json
Invoke-RestMethod "http://localhost:5010/api/v1/Orders/available-products" | ConvertTo-Json -Depth 3
```

---

## 📊 Arquitectura en Kubernetes

```
                    Internet
                       │
                 ┌─────▼──────┐
                 │   Ingress   │
                 │   (nginx)   │
                 └──────┬──────┘
                        │
                 ┌──────▼──────┐
                 │   Gateway   │  ← LoadBalancer Service
                 │  (2 pods)   │     IP Externa: 20.x.x.x
                 └──┬──────┬───┘
                    │      │
         ┌──────────┘      └──────────┐
         ▼                            ▼
  ┌──────────────┐          ┌──────────────┐
  │  Product     │          │  Order       │
  │  Service     │◄─────────│  Service     │  ← ClusterIP Services
  │  (2 pods)    │  HTTP/   │  (2 pods)    │     Solo acceso interno
  │  :5001 :5002 │  gRPC    │  :5003       │
  └──┬──┬────────┘          └──────────────┘
     │  │
     │  └─────────┐
     ▼            ▼
  ┌────────┐ ┌────────┐  ┌───────────┐
  │Postgres│ │ Redis  │  │ RabbitMQ  │  ← ClusterIP Services
  │ (PVC)  │ │        │  │           │     Datos persistentes
  └────────┘ └────────┘  └───────────┘
```

**Balanceo de carga:** Kubernetes distribuye automáticamente el tráfico entre las réplicas de cada Deployment. Si un pod falla el health check, se saca de la rotación y se recrea.

---

## 📎 Referencias

- [Azure Kubernetes Service (AKS)](https://learn.microsoft.com/en-us/azure/aks/)
- [Kubernetes Services](https://kubernetes.io/docs/concepts/services-networking/service/)
- [Kubernetes Ingress](https://kubernetes.io/docs/concepts/services-networking/ingress/)
- [Health Probes in K8s](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
- [Kubernetes in Docker Desktop](https://docs.docker.com/desktop/features/kubernetes/)
- [Kind — Kubernetes in Docker](https://kind.sigs.k8s.io/)
- [Podman Desktop](https://podman-desktop.io/)
