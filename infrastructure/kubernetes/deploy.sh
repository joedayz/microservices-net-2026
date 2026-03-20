#!/bin/bash
# ============================================================
# deploy.sh — Despliega todos los recursos en Kubernetes
# Uso: ./deploy.sh [ACR_NAME]
#
# Si se proporciona ACR_NAME, reemplaza los placeholders de imagen.
# Si no, usa imágenes locales (para desarrollo con Docker Desktop/Kind).
# ============================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ACR_NAME="${1:-}"

echo "=== Desplegando microservicios en Kubernetes ==="

# 1. Namespace
echo "→ Creando namespace..."
kubectl apply -f "$SCRIPT_DIR/namespace.yaml"

# 2. Infraestructura
echo "→ Desplegando PostgreSQL..."
kubectl apply -f "$SCRIPT_DIR/postgres.yaml"

echo "→ Desplegando Redis..."
kubectl apply -f "$SCRIPT_DIR/redis.yaml"

echo "→ Desplegando RabbitMQ..."
kubectl apply -f "$SCRIPT_DIR/rabbitmq.yaml"

# Esperar a que la infraestructura esté lista
echo "→ Esperando infraestructura..."
kubectl wait --for=condition=ready pod -l app=postgres -n microservices --timeout=120s
kubectl wait --for=condition=ready pod -l app=redis -n microservices --timeout=60s
kubectl wait --for=condition=ready pod -l app=rabbitmq -n microservices --timeout=120s

# 3. Microservicios
if [ -n "$ACR_NAME" ]; then
    echo "→ Usando ACR: $ACR_NAME.azurecr.io"
    # Reemplazar placeholder con nombre real del ACR
    for f in product-service.yaml order-service.yaml gateway.yaml; do
        sed "s|\${ACR_NAME}|$ACR_NAME|g" "$SCRIPT_DIR/$f" | kubectl apply -f -
    done
else
    echo "→ Usando imágenes locales (sin ACR)"
    for f in product-service.yaml order-service.yaml gateway.yaml; do
        sed 's|\${ACR_NAME}.azurecr.io/||g' "$SCRIPT_DIR/$f" | kubectl apply -f -
    done
fi

# 4. Ingress
echo "→ Configurando Ingress..."
kubectl apply -f "$SCRIPT_DIR/ingress.yaml"

# 5. Estado
echo ""
echo "=== Despliegue completado ==="
echo "→ Verificar estado:"
echo "  kubectl get all -n microservices"
echo "  kubectl get ingress -n microservices"
echo ""
echo "→ Health checks:"
echo "  kubectl port-forward svc/gateway 5010:80 -n microservices"
echo "  curl http://localhost:5010/health"
