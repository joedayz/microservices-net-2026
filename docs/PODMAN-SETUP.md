# Configuración con Podman

Este proyecto puede ejecutarse con **Podman** en lugar de Docker. Esta guía cubre dos escenarios:

1. **[docker-compose local](#1-servicios-de-infraestructura-con-podman-compose)** — PostgreSQL, Redis, RabbitMQ para desarrollo
2. **[Kubernetes con Kind](#2-kubernetes-local-con-podman--kind)** — Despliegue completo (Módulo 12)

---

## 1. Servicios de infraestructura con podman compose

Levanta PostgreSQL, Redis y RabbitMQ para desarrollo local:

**macOS / Linux:**
```bash
# Desde la raíz del proyecto
podman compose up -d

# Verificar
podman ps

# Ver logs
podman compose logs postgres
podman compose logs redis
podman compose logs rabbitmq

# Detener
podman compose down

# Reset completo (borra volúmenes)
podman compose down -v
```

**Windows (PowerShell):**
```powershell
podman compose up -d
podman ps
podman compose down
```

### Verificar conexiones

```bash
# macOS / Linux / Windows
podman exec -it microservices-postgres psql -U postgres -d microservices_db
podman exec -it microservices-redis redis-cli ping
```

### Diferencias con Docker

| Docker | Podman |
|--------|--------|
| `docker compose up -d` | `podman compose up -d` |
| `docker build` | `podman build` |
| `docker save` | `podman save` |
| Requiere daemon | No requiere daemon (rootless) |
| Imágenes sin prefijo | Imágenes locales con prefijo `localhost/` |

### Solución de problemas — compose

**"podman compose: command not found"**
```bash
# macOS
brew install podman-compose

# Linux
pip3 install podman-compose

# Windows
pip install podman-compose
```

**Puerto ya en uso:**
```bash
# macOS / Linux
lsof -i :5432
podman stop $(podman ps -q)

# Windows (PowerShell)
netstat -ano | findstr :5432
```

---

## 2. Kubernetes local con Podman + Kind

> **Prerrequisitos:** `podman` corriendo, `kind` instalado, `kubectl` instalado.

### Instalar Kind

**macOS:**
```bash
brew install kind
```

**Linux:**
```bash
curl -Lo ./kind https://kind.sigs.k8s.io/dl/v0.27.0/kind-linux-amd64
chmod +x ./kind && sudo mv ./kind /usr/local/bin/kind
```

**Windows (PowerShell como Admin):**
```powershell
winget install Kubernetes.kind
# o con Chocolatey:
choco install kind
```

---

### Flujo completo desde cero

#### Paso 0 — Limpiar cluster anterior (si existe)

```bash
# macOS / Linux
KIND_EXPERIMENTAL_PROVIDER=podman kind delete cluster --name microservices

# Windows (PowerShell)
$env:KIND_EXPERIMENTAL_PROVIDER="podman"; kind delete cluster --name microservices
```

#### Paso 1 — Crear cluster Kind con Podman

```bash
# macOS / Linux
KIND_EXPERIMENTAL_PROVIDER=podman kind create cluster --name microservices

# Windows (PowerShell)
$env:KIND_EXPERIMENTAL_PROVIDER="podman"; kind create cluster --name microservices
```

> **Por qué `KIND_EXPERIMENTAL_PROVIDER=podman`:** Kind usa Docker por defecto. Esta variable le indica que use Podman como runtime.

#### Paso 2 — Configurar kubectl (CRÍTICO — no omitir)

```bash
# macOS / Linux
KIND_EXPERIMENTAL_PROVIDER=podman kind export kubeconfig --name microservices
kubectl config use-context kind-microservices
kubectl get nodes

# Windows (PowerShell)
$env:KIND_EXPERIMENTAL_PROVIDER="podman"; kind export kubeconfig --name microservices
kubectl config use-context kind-microservices
kubectl get nodes
```

Resultado esperado:
```
NAME                          STATUS   ROLES           AGE
microservices-control-plane   Ready    control-plane   1m
```

> **⚠️ Por qué `kind export kubeconfig` SIEMPRE:**
> Kind asigna un puerto dinámico al API server. Si hay un contexto anterior (`kind-microservices`) apuntando a un puerto antiguo, `kubectl` responde `connection refused`. El `export kubeconfig` sobreescribe el contexto con el puerto correcto del cluster activo.
>
> **Regla fija:** Después de crear o recrear un cluster, siempre ejecuta `kind export kubeconfig`.

#### Paso 3 — Build de imágenes

> **⚠️ Usar prefijo `localhost/`:** Podman almacena imágenes locales bajo `localhost/`. Sin ese prefijo, Kind buscará la imagen en Docker Hub → `ImagePullBackOff`.

**macOS / Linux:**
```bash
podman build --platform linux/amd64 \
  -t localhost/product-service:latest \
  -f src/Services/ProductService/Dockerfile src/Services/

podman build --platform linux/amd64 \
  -t localhost/order-service:latest \
  -f src/Services/OrderService/Dockerfile src/Services/

podman build --platform linux/amd64 \
  -t localhost/gateway:latest \
  -f src/Gateway/Dockerfile src/Gateway/
```

**Windows (PowerShell):**
```powershell
podman build --platform linux/amd64 `
  -t localhost/product-service:latest `
  -f src/Services/ProductService/Dockerfile src/Services/

podman build --platform linux/amd64 `
  -t localhost/order-service:latest `
  -f src/Services/OrderService/Dockerfile src/Services/

podman build --platform linux/amd64 `
  -t localhost/gateway:latest `
  -f src/Gateway/Dockerfile src/Gateway/
```

Verificar:
```bash
podman images | grep -E "product-service|order-service|gateway"
# localhost/product-service   latest   ...
# localhost/order-service     latest   ...
# localhost/gateway           latest   ...
```

#### Paso 4 — Cargar imágenes en Kind

> **⚠️ Por qué no funciona `kind load docker-image`:** Kind busca imágenes en el daemon de Docker. Podman es independiente; Kind no puede acceder directamente. Solución: exportar a `.tar` → cargar con `image-archive`.
>
> **⚠️ `--name microservices`** es el nombre Kind del cluster, **NO** el contexto kubectl (`kind-microservices`).

**macOS / Linux:**
```bash
podman save -o /tmp/product-service-latest.tar localhost/product-service:latest
podman save -o /tmp/order-service-latest.tar localhost/order-service:latest
podman save -o /tmp/gateway-latest.tar localhost/gateway:latest

KIND_EXPERIMENTAL_PROVIDER=podman kind load image-archive /tmp/product-service-latest.tar --name microservices
KIND_EXPERIMENTAL_PROVIDER=podman kind load image-archive /tmp/order-service-latest.tar --name microservices
KIND_EXPERIMENTAL_PROVIDER=podman kind load image-archive /tmp/gateway-latest.tar --name microservices
```

**Windows (PowerShell):**
```powershell
podman save -o "$env:TEMP\product-service-latest.tar" localhost/product-service:latest
podman save -o "$env:TEMP\order-service-latest.tar" localhost/order-service:latest
podman save -o "$env:TEMP\gateway-latest.tar" localhost/gateway:latest

$env:KIND_EXPERIMENTAL_PROVIDER="podman"
kind load image-archive "$env:TEMP\product-service-latest.tar" --name microservices
kind load image-archive "$env:TEMP\order-service-latest.tar" --name microservices
kind load image-archive "$env:TEMP\gateway-latest.tar" --name microservices
```

#### Paso 5 — Desplegar

> **⚠️ `LOCAL_IMAGE_PREFIX=localhost/` es OBLIGATORIO.** Sin él, `deploy.sh` usa imágenes sin prefijo → Kubernetes las busca en Docker Hub.

**macOS / Linux:**
```bash
# Desde la raíz del proyecto
LOCAL_IMAGE_PREFIX=localhost/ ./infrastructure/kubernetes/deploy.sh
```

**Windows (PowerShell / Git Bash / WSL):**
```bash
# Git Bash o WSL
LOCAL_IMAGE_PREFIX=localhost/ ./infrastructure/kubernetes/deploy.sh

# PowerShell puro
$env:LOCAL_IMAGE_PREFIX="localhost/"
bash ./infrastructure/kubernetes/deploy.sh
```

Resultado esperado:
```
=== Desplegando microservicios en Kubernetes ===
→ Esperando infraestructura...
pod/postgres condition met
pod/redis condition met
pod/rabbitmq condition met
→ Usando imágenes locales, prefijo: 'localhost/'
deployment.apps/product-service configured
deployment.apps/order-service configured
deployment.apps/gateway configured
=== Despliegue completado ===
```

#### Paso 6 — Verificar

```bash
# Todos los pods deben ser 1/1 Running
kubectl get pods -n microservices

# Resultado esperado:
# NAME                               READY   STATUS    RESTARTS
# gateway-xxx                        1/1     Running   0
# order-service-xxx                  1/1     Running   0
# product-service-xxx                1/1     Running   0
# postgres-xxx                       1/1     Running   0
# rabbitmq-xxx                       1/1     Running   0
# redis-xxx                          1/1     Running   0
```

#### Paso 7 — Acceder al Gateway

```bash
# macOS / Linux / Windows
kubectl port-forward svc/gateway 5010:80 -n microservices
```

En otra terminal:
```bash
# macOS / Linux
curl http://localhost:5010/health
curl http://localhost:5010/api/v1/Products

# Windows (PowerShell)
Invoke-RestMethod http://localhost:5010/health
Invoke-RestMethod http://localhost:5010/api/v1/Products
```

---

### Tabla de errores comunes

| Error | Causa | Solución |
|-------|-------|----------|
| `connection refused` en `kubectl get nodes` | Contexto stale (puerto antiguo) | `KIND_EXPERIMENTAL_PROVIDER=podman kind export kubeconfig --name microservices` |
| `ImagePullBackOff` — busca en Docker Hub | Imagen sin `localhost/` en el deployment | `LOCAL_IMAGE_PREFIX=localhost/ ./infrastructure/kubernetes/deploy.sh` |
| `kind load docker-image` → "not present locally" | Kind no accede al daemon de Podman | Usar `podman save` + `kind load image-archive` |
| `kind --name kind-microservices` → "no nodes found" | El nombre Kind es `microservices`, no `kind-microservices` | Usar `--name microservices` (sin prefijo `kind-`) |
| `podman compose: command not found` | podman-compose no instalado | `brew install podman-compose` (macOS) |
| Build falla en Apple Silicon (M1/M2/M3) | Arquitectura ARM vs AMD | Añadir `--platform linux/amd64` al build |

---

### Comandos de utilidad

```bash
# Ver clusters Kind activos
KIND_EXPERIMENTAL_PROVIDER=podman kind get clusters

# Ver contextos kubectl
kubectl config get-contexts

# Debug de un pod con problemas
kubectl describe pod -l app=product-service -n microservices
kubectl logs -l app=product-service -n microservices --tail=50

# Ver estado de rollout
kubectl rollout status deployment/product-service -n microservices

# Eliminar cluster y empezar de cero
KIND_EXPERIMENTAL_PROVIDER=podman kind delete cluster --name microservices
```
