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
Desplegar los microservicios en Kubernetes (AKS o local con minikube/kind):
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
- Probes con `rabbitmq-diagnostics check_port_connectivity`

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
./infrastructure/kubernetes/deploy.sh myacrregistry

# Despliegue local (sin ACR, para minikube/kind)
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
# 1. Crear cluster AKS
az aks create \
  --resource-group rg-microservices \
  --name aks-microservices \
  --node-count 2 \
  --enable-managed-identity \
  --attach-acr myacrregistry \
  --generate-ssh-keys

# 2. Obtener credenciales
az aks get-credentials --resource-group rg-microservices --name aks-microservices

# 3. Verificar conexión
kubectl get nodes

# 4. Desplegar (con imágenes del ACR)
./infrastructure/kubernetes/deploy.sh myacrregistry

# 5. Obtener IP externa del Gateway
kubectl get svc gateway -n microservices
# EXTERNAL-IP: 20.xxx.xxx.xxx

# 6. Probar
curl http://20.xxx.xxx.xxx/api/v1/Products | jq
curl http://20.xxx.xxx.xxx/health | jq
```

### Opción B — Local con minikube

```bash
# 1. Iniciar minikube
minikube start --memory=4096 --cpus=2

# 2. Usar el Docker daemon de minikube para builds locales
eval $(minikube docker-env)

# 3. Build de imágenes locales
docker build -t product-service:latest \
  -f src/Services/ProductService/Dockerfile src/Services/
docker build -t order-service:latest \
  -f src/Services/OrderService/Dockerfile src/Services/
docker build -t gateway:latest \
  -f src/Gateway/Dockerfile src/Gateway/

# 4. Desplegar (sin ACR = usa imágenes locales)
./infrastructure/kubernetes/deploy.sh

# 5. Acceder via minikube
minikube service gateway -n microservices
# o port-forward:
kubectl port-forward svc/gateway 5010:80 -n microservices
curl http://localhost:5010/health | jq
```

### Verificar estado del cluster

```bash
# Ver todos los recursos
kubectl get all -n microservices

# Ver pods con estado
kubectl get pods -n microservices -o wide

# Ver logs de un servicio
kubectl logs -l app=product-service -n microservices --tail=50

# Describir un pod (para debug)
kubectl describe pod -l app=product-service -n microservices

# Health checks
kubectl port-forward svc/product-service 5001:5001 -n microservices
curl http://localhost:5001/health | jq

# Escalar manualmente
kubectl scale deployment product-service --replicas=3 -n microservices
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
- [minikube](https://minikube.sigs.k8s.io/docs/start/)

