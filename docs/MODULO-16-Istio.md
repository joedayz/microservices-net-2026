# Módulo 16 – Observabilidad y Service Mesh con Istio

## 🧠 Teoría

### ¿Qué agrega Istio al taller?

Istio agrega una capa de red entre microservicios sin cambiar código de negocio:
- **Sidecars Envoy** por pod
- **Telemetría automática** (métricas, traces)
- **Seguridad de malla** (mTLS)
- **Control de tráfico** (retries, timeouts, split)

### Componentes clave

- **istiod**: plano de control de la malla
- **Envoy sidecar**: proxy inyectado en cada pod
- **Kiali**: topología y salud de servicios
- **Jaeger**: trazas distribuidas
- **Prometheus**: métricas de Istio y workloads

### mTLS en Istio

Con `PeerAuthentication` en `STRICT`, todo tráfico dentro del namespace viaja cifrado y autenticado entre sidecars.

---

## 🧪 Laboratorio 16

### Objetivo

1. Instalar Istio
2. Habilitar sidecar injection en `microservices`
3. Instalar add-ons (Kiali, Jaeger, Prometheus)
4. Validar observabilidad
5. Activar mTLS `STRICT`

### ¿Cómo explicarlo a los alumnos?

Usa esta narrativa durante el laboratorio:

1. **Antes de Istio:** los servicios se comunican directo pod a pod.
2. **Con Istio:** cada pod tiene un sidecar Envoy que intercepta tráfico.
3. **Observabilidad:** Istio exporta métricas y trazas sin tocar código de negocio.
4. **Seguridad:** con mTLS `STRICT`, todo tráfico interno va cifrado.

Pregunta guía para el alumno:
> "¿Qué cambió en mi aplicación?"

Respuesta esperada:
> "No cambió mi código; cambió la capa de red y observabilidad alrededor de mis servicios."

### Prerrequisitos

- Cluster Kubernetes operativo (Kind local o AKS)
- `kubectl` funcionando
- Workloads del módulo 12 desplegados (`gateway`, `product-service`, `order-service`)
- `istioctl` instalado

Verifica contexto antes de continuar:

```bash
kubectl config current-context
kubectl get nodes
kubectl get pods -n microservices
```

---

## 1) Instalar `istioctl`

### macOS

```bash
brew install istioctl
istioctl version
```

### Linux

```bash
curl -L https://istio.io/downloadIstio | sh -
cd istio-*/
sudo mv bin/istioctl /usr/local/bin/
istioctl version
```

### Windows (PowerShell)

```powershell
choco install istioctl -y
istioctl version
```

---

## 2) Instalar Istio en el cluster

```bash
istioctl install --set profile=demo -y
kubectl get pods -n istio-system
```

Resultado esperado: `istiod` en `Running`.

**Qué está pasando aquí:** se instala el plano de control (`istiod`) que empuja configuración a los sidecars Envoy.

---

## 3) Instalar add-ons de observabilidad

```bash
kubectl apply -f https://raw.githubusercontent.com/istio/istio/release-1.27/samples/addons/prometheus.yaml
kubectl apply -f https://raw.githubusercontent.com/istio/istio/release-1.27/samples/addons/jaeger.yaml
kubectl apply -f https://raw.githubusercontent.com/istio/istio/release-1.27/samples/addons/kiali.yaml

kubectl get pods -n istio-system
```

**Qué está pasando aquí:**
- `Prometheus` empieza a recolectar métricas de malla y workloads.
- `Jaeger` almacena trazas distribuidas.
- `Kiali` muestra visualmente el grafo de servicios.

---

## 4) Habilitar sidecar injection para `microservices`

```bash
kubectl label namespace microservices istio-injection=enabled --overwrite

# Reinicia deployments para que se inyecte el sidecar en pods nuevos
kubectl rollout restart deployment -n microservices

# Verifica que cada pod de app tenga 2/2 containers (app + istio-proxy)
kubectl get pods -n microservices
```

Validación rápida del sidecar:

```bash
kubectl get pods -n microservices -o jsonpath='{range .items[*]}{.metadata.name}{" => "}{range .spec.containers[*]}{.name}{" "}{end}{"\n"}{end}'
```

Debes ver `istio-proxy` en los pods de tus microservicios.

**Qué debe notar el alumno:**
- Antes: pods con 1 contenedor.
- Después: pods con 2 contenedores (`app` + `istio-proxy`).

Ese es el indicador más claro de que el service mesh está activo.

---

## 5) Generar tráfico para poblar métricas y trazas

Puedes usar el script unificado del módulo 12 para port-forward local:

```bash
./infrastructure/kubernetes/port-forward-all.sh
```

En otra terminal:

```bash
for i in {1..50}; do
  curl -s http://localhost:5010/api/v1/Products >/dev/null
  curl -s http://localhost:5010/api/v1/Orders >/dev/null
done
echo "Traffic generated"
```

**Qué está pasando aquí:** sin tráfico, Kiali/Jaeger casi no muestran datos. Este paso "alimenta" la observabilidad.

---

## 6) Abrir Kiali, Jaeger y Prometheus

### Opción A (recomendada): `istioctl dashboard`

```bash
istioctl dashboard kiali
istioctl dashboard jaeger
istioctl dashboard prometheus
```

### Opción B: `kubectl port-forward`

```bash
kubectl -n istio-system port-forward svc/kiali 20001:20001
kubectl -n istio-system port-forward svc/jaeger 16686:16686
kubectl -n istio-system port-forward svc/prometheus 9090:9090
```

URLs:
- Kiali: `http://localhost:20001`
- Jaeger: `http://localhost:16686`
- Prometheus: `http://localhost:9090`

**Qué debe validar el alumno en cada herramienta:**
- **Kiali:** que exista comunicación `gateway -> product-service` y `gateway -> order-service`.
- **Jaeger:** que una request del Gateway genere spans entre servicios.
- **Prometheus:** que existan métricas de request rate, latencia y errores.

---

## 7) Activar mTLS `STRICT`

Usa el manifiesto incluido en el repo:

```bash
kubectl apply -f infrastructure/istio/peer-authentication-strict.yaml
kubectl apply -f infrastructure/istio/destination-rule-mtls.yaml
```

Verificación (si `istioctl` soporta el comando en tu versión):

```bash
istioctl authn tls-check -n microservices
```

> Si tu versión ya no incluye `authn tls-check`, valida en Kiali que las aristas del grafo muestren candado mTLS.

**Qué está pasando aquí:**
- `PeerAuthentication` obliga tráfico cifrado dentro del namespace.
- `DestinationRule` define cómo los clientes hablan TLS dentro de la malla.

Si esto falla, normalmente verás `503` por políticas TLS inconsistentes.

---

## Troubleshooting rápido

1. **Pods no tienen sidecar**
```bash
kubectl get ns microservices --show-labels
kubectl rollout restart deployment -n microservices
```

2. **Kiali/Jaeger no abren**
```bash
kubectl get pods -n istio-system
kubectl logs -n istio-system deploy/kiali
```

3. **Errores 503 tras mTLS strict**
```bash
kubectl get peerauthentication,destinationrule -n microservices
kubectl describe peerauthentication default -n microservices
```

4. **Ver contexto activo para evitar mezclar Kind/AKS**
```bash
kubectl config current-context
```

---

## Cleanup del módulo 16

```bash
# Quitar políticas de mTLS del módulo
kubectl delete -f infrastructure/istio/peer-authentication-strict.yaml --ignore-not-found
kubectl delete -f infrastructure/istio/destination-rule-mtls.yaml --ignore-not-found

# Quitar label de inyección del namespace (opcional)
kubectl label namespace microservices istio-injection-

# Desinstalar Istio (si deseas limpiar todo)
istioctl uninstall -y
kubectl delete namespace istio-system --ignore-not-found
```

---

## ✅ Checklist de aprendizaje (para alumnos)

- [ ] Entiendo qué es un sidecar y por qué no requiere cambiar código de negocio.
- [ ] Puedo demostrar que `istio-proxy` fue inyectado en los pods.
- [ ] Puedo generar tráfico y verlo reflejado en Kiali/Jaeger/Prometheus.
- [ ] Puedo explicar qué resuelve mTLS `STRICT`.
- [ ] Sé limpiar el laboratorio completo para evitar costos/ruido.

---

## 📎 Referencias

- [Istio Documentation](https://istio.io/latest/docs/)
- [Istio Addons](https://istio.io/latest/docs/ops/integrations/)
- [Kiali](https://kiali.io/)
- [Jaeger](https://www.jaegertracing.io/)
- [Prometheus](https://prometheus.io/docs/introduction/overview/)

