# Taller: Microservicios .NET + Azure + Terraform + Istio

## 📚 Estructura del Taller

Este taller está diseñado para aprender a construir microservicios empresariales usando .NET 10, Azure, Terraform e Istio.

### 🎯 Objetivos

- Comprender los fundamentos de arquitectura de microservicios
- Implementar microservicios con .NET 10 siguiendo mejores prácticas
- Integrar servicios con Azure (App Configuration, Key Vault, Service Bus, AKS)
- Automatizar infraestructura con Terraform
- Implementar observabilidad con Istio
- Crear pipelines CI/CD completos

### 📋 Módulos

- ✅ **Módulo 1**: Fundamentos de Microservicios - **COMPLETADO**
- ✅ **Módulo 2**: Principios y patrones de diseño (DDD, Hexagonal Architecture) - **COMPLETADO**
- ✅ **Módulo 3**: Buenas prácticas de diseño (Versionamiento, DTOs) - **COMPLETADO**
- ✅ **Módulo 4**: Persistencia de datos (PostgreSQL, MongoDB) - **COMPLETADO**
- ✅ **Módulo 5**: Performance y consultas (Redis, índices) - **COMPLETADO**
- ✅ **Módulo 6**: Configuración centralizada (Options Pattern, User Secrets, Feature Flags) - **COMPLETADO**
- ✅ **Módulo 7**: Integración (REST, gRPC, Service Bus) - **COMPLETADO**
- 📝 **Módulo 8**: Seguridad (Azure AD, OAuth2) - **DOCUMENTADO**
- ✅ **Módulo 9**: Comunicación (API Gateway, gRPC) - **COMPLETADO**
- ✅ **Módulo 10**: Serverless (Azure Functions, Service Bus trigger) - **COMPLETADO**
- 📝 **Módulo 11**: Alta disponibilidad (Polly, Circuit Breaker) - **DOCUMENTADO**
- 📝 **Módulo 12**: Balanceo de carga (AKS) - **DOCUMENTADO**
- 📝 **Módulo 13**: Azure Cloud (ACR, AKS) - **DOCUMENTADO**
- 📝 **Módulo 14**: DevOps (CI/CD Pipelines) - **DOCUMENTADO**
- 📝 **Módulo 15**: Terraform (IaC) - **DOCUMENTADO**
- 📝 **Módulo 16**: Observabilidad (Istio, Jaeger, Kiali, Prometheus) - **DOCUMENTADO**

### 🏗️ Estructura del Proyecto

```
microservices-net-2025/
├── src/
│   ├── Services/
│   │   ├── ProductService/          # Microservicio de Productos
│   │   ├── OrderService/            # Microservicio de Órdenes
│   │   └── UserService/             # Microservicio de Usuarios
│   ├── Gateway/                    # API Gateway (Ocelot/YARP)
│   └── Functions/                   # Azure Functions
├── infrastructure/
│   ├── terraform/                   # Scripts de Terraform
│   └── kubernetes/                  # Manifiestos de Kubernetes
├── docker/                          # Dockerfiles
├── .github/
│   └── workflows/                   # GitHub Actions
└── docs/                            # Documentación de módulos
```

### 🚀 Requisitos Previos

- .NET 10 SDK
- Docker Desktop (o Podman)
- Azure CLI
- Terraform
- kubectl
- Kind *(solo para Módulo 12 con Podman — `brew install kind`)*
- istioctl
- JetBrains Rider (recomendado) o Visual Studio Code / Visual Studio

### 💻 Instalación

#### .NET 10 SDK

**Windows:**
```bash
# Descargar e instalar desde:
# https://dotnet.microsoft.com/download/dotnet/10.0

# Verificar instalación
dotnet --version
# Debe mostrar: 10.x.x
```

**macOS:**
```bash
# Usando Homebrew
brew install --cask dotnet-sdk

# Verificar instalación
dotnet --version
# Debe mostrar: 10.x.x
```

**Linux (Ubuntu/Debian):**
```bash
# Agregar repositorio de Microsoft
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Instalar .NET 10 SDK
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0

# Verificar instalación
dotnet --version
# Debe mostrar: 10.x.x
```

**Descarga directa:**
- Visita: https://dotnet.microsoft.com/download/dotnet/10.0
- Selecciona tu sistema operativo
- Descarga e instala el SDK (no solo el Runtime)

#### JetBrains Rider

**Windows/macOS/Linux:**
```bash
# Descargar desde:
# https://www.jetbrains.com/rider/download/

# O usar JetBrains Toolbox (recomendado):
# https://www.jetbrains.com/toolbox-app/
```

**Alternativas:**
- **Visual Studio Code** con extensión C#: https://code.visualstudio.com/
- **Visual Studio 2022**: https://visualstudio.microsoft.com/
- **Rider** (recomendado para este taller): https://www.jetbrains.com/rider/

#### Docker Desktop (o Podman)

**Docker Desktop:**
- **Windows/macOS**: https://www.docker.com/products/docker-desktop
- **Linux**: https://docs.docker.com/engine/install/

**Podman (alternativa):**
- Ver instrucciones en [`docs/PODMAN-SETUP.md`](./docs/PODMAN-SETUP.md)

**Verificar instalación:**
```bash
docker --version
# o
podman --version
```

#### Otras herramientas (opcional para módulos avanzados)

- **Azure CLI**: https://docs.microsoft.com/cli/azure/install-azure-cli
- **Terraform**: https://www.terraform.io/downloads
- **kubectl**: https://kubernetes.io/docs/tasks/tools/
- **istioctl**: https://istio.io/latest/docs/setup/getting-started/#download

### 📖 Cómo usar este taller

1. Cada módulo tiene su propia carpeta con teoría y laboratorio
2. Los laboratorios están numerados secuencialmente
3. Sigue el orden de los módulos para una mejor comprensión
4. El proyecto final integra todos los conceptos aprendidos

### 🔧 Configuración Inicial

#### 1. Servicios de infraestructura local (docker-compose)

Levanta PostgreSQL, Redis y RabbitMQ para desarrollo:

**Docker:**
```bash
# Desde la raíz del proyecto
docker compose up -d

# Verificar
docker ps

# Ver logs
docker compose logs postgres
docker compose logs redis

# Detener
docker compose down
```

**Podman:**
```bash
# Desde la raíz del proyecto
podman compose up -d

# Verificar
podman ps

# Detener
podman compose down
```

#### 2. Ejecutar un servicio en local

```bash
cd src/Services/ProductService
dotnet restore
dotnet run
```

Accede a: http://localhost:5001/swagger

---

#### 3. Kubernetes local (Módulo 12)

Para desplegar en Kubernetes local hay dos opciones:

---

**Opción A — Docker Desktop (más simple)**

1. Activa Kubernetes: Docker Desktop → Settings → Kubernetes → Enable Kubernetes → Apply & Restart
2. Espera a que el indicador esté en verde

```bash
# Verificar contexto
kubectl config use-context docker-desktop
kubectl get nodes

# Build de imágenes
docker build --platform linux/amd64 -t product-service:latest \
  -f src/Services/ProductService/Dockerfile src/Services/
docker build --platform linux/amd64 -t order-service:latest \
  -f src/Services/OrderService/Dockerfile src/Services/
docker build --platform linux/amd64 -t gateway:latest \
  -f src/Gateway/Dockerfile src/Gateway/

# Desplegar (las imágenes ya están disponibles, no hay que cargarlas)
./infrastructure/kubernetes/deploy.sh

# Acceder (usar script unificado)
./infrastructure/kubernetes/port-forward-all.sh

# En otra terminal
curl http://localhost:5010/health
```

---

**Opción B — Podman + Kind**

> Ver guía completa en [`docs/PODMAN-SETUP.md`](./docs/PODMAN-SETUP.md)

```bash
# Prerrequisitos
brew install kind          # macOS

# 1. Crear cluster (SIEMPRE con KIND_EXPERIMENTAL_PROVIDER=podman)
KIND_EXPERIMENTAL_PROVIDER=podman kind create cluster --name microservices

# 2. Exportar kubeconfig (CRÍTICO — evita el error "connection refused")
KIND_EXPERIMENTAL_PROVIDER=podman kind export kubeconfig --name microservices
kubectl config use-context kind-microservices
kubectl get nodes

# 3. Build con prefijo localhost/ (Podman lo requiere)
podman build --platform linux/amd64 -t localhost/product-service:latest \
  -f src/Services/ProductService/Dockerfile src/Services/
podman build --platform linux/amd64 -t localhost/order-service:latest \
  -f src/Services/OrderService/Dockerfile src/Services/
podman build --platform linux/amd64 -t localhost/gateway:latest \
  -f src/Gateway/Dockerfile src/Gateway/

# 4. Cargar imágenes en Kind (export tar + load archive)
podman save -o /tmp/product-service-latest.tar localhost/product-service:latest
podman save -o /tmp/order-service-latest.tar localhost/order-service:latest
podman save -o /tmp/gateway-latest.tar localhost/gateway:latest

KIND_EXPERIMENTAL_PROVIDER=podman kind load image-archive /tmp/product-service-latest.tar --name microservices
KIND_EXPERIMENTAL_PROVIDER=podman kind load image-archive /tmp/order-service-latest.tar --name microservices
KIND_EXPERIMENTAL_PROVIDER=podman kind load image-archive /tmp/gateway-latest.tar --name microservices

# 5. Desplegar (prefijo localhost/ OBLIGATORIO)
LOCAL_IMAGE_PREFIX=localhost/ ./infrastructure/kubernetes/deploy.sh

# 6. Acceder (usar script unificado)
./infrastructure/kubernetes/port-forward-all.sh

# En otra terminal
curl http://localhost:5010/health
```

> **Empezar desde cero (limpiar cluster existente):**
> ```bash
> KIND_EXPERIMENTAL_PROVIDER=podman kind delete cluster --name microservices
> # Luego repetir los pasos del 1 al 6
> ```

---

### 🚀 Estado del Proyecto

**Completado:**
- ✅ Estructura base del proyecto
- ✅ ProductService con arquitectura hexagonal completa
- ✅ Integración con PostgreSQL y Entity Framework Core
- ✅ Redis caching implementado
- ✅ Versionamiento de API (v1 y v2)
- ✅ Swagger/OpenAPI configurado
- ✅ Configuración centralizada (Options Pattern, User Secrets, Feature Flags)
- ✅ Dockerfile para containerización
- ✅ Documentación completa de todos los módulos
- ✅ API Gateway (YARP) con routing a ProductService y OrderService

**En progreso:**
- ⏳ UserService
- ⏳ Azure Service Bus integration
- ⏳ Terraform scripts
- ⏳ CI/CD pipelines
- ⏳ Despliegue en AKS
- ⏳ Istio y observabilidad

### 📚 Documentación

Cada módulo tiene su propia documentación completa con **guías paso a paso** en `/docs`:

**Módulos Implementados (con código completo y pasos detallados):**
- 📖 [`MODULO-01-Fundamentos.md`](./docs/MODULO-01-Fundamentos.md) - Teoría y Lab 1 paso a paso
- 📖 [`MODULO-02-Arquitectura-Hexagonal.md`](./docs/MODULO-02-Arquitectura-Hexagonal.md) - DDD y arquitectura paso a paso
- 📖 [`MODULO-03-Versionamiento-API.md`](./docs/MODULO-03-Versionamiento-API.md) - Versionamiento y Swagger paso a paso
- 📖 [`MODULO-04-Persistencia-Datos.md`](./docs/MODULO-04-Persistencia-Datos.md) - PostgreSQL y EF Core paso a paso
- 📖 [`MODULO-05-Redis-Cache.md`](./docs/MODULO-05-Redis-Cache.md) - Caching distribuido paso a paso
- 📖 [`MODULO-06-Configuracion-Centralizada.md`](./docs/MODULO-06-Configuracion-Centralizada.md) - Options Pattern, User Secrets y Feature Flags paso a paso
- 📖 [`MODULO-07-Integracion.md`](./docs/MODULO-07-Integracion.md) - REST, gRPC, RabbitMQ, OrderService paso a paso
- 📖 [`MODULO-08-Seguridad.md`](./docs/MODULO-08-Seguridad.md) - Azure AD, JWT Bearer paso a paso
- 📖 [`MODULO-09-API-Gateway.md`](./docs/MODULO-09-API-Gateway.md) - API Gateway con YARP paso a paso

**Módulos Documentados (con teoría y guías de implementación):**
- 📝 Módulos 10-16 - Documentación de módulos avanzados (Azure, Terraform, Istio)
- 📖 [`PROYECTO-FINAL.md`](./docs/PROYECTO-FINAL.md) - Guía del proyecto integrador
- 📖 [`GUIA-PASO-A-PASO.md`](./docs/GUIA-PASO-A-PASO.md) - Índice general y guía de uso

### 🎯 Empezar el Taller

1. **Lee la [Guía Paso a Paso](./docs/GUIA-PASO-A-PASO.md)** para entender la estructura
2. **Sigue los módulos en orden** (1 → 2 → 3 → 4 → 5 → 6)
3. **Cada módulo incluye:**
   - 🧠 Teoría del concepto
   - 🧪 Laboratorio con pasos numerados
   - ✅ Checklist de verificación
   - 🐛 Solución de problemas
4. **Completa el proyecto final** integrando todos los conceptos

### 📝 Licencia

Este proyecto es parte de un taller educativo.

