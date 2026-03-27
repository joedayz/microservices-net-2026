#!/bin/bash
# ============================================================
# cleanup.sh — Elimina TODOS los recursos de la demo de microservicios
#
# Uso:
#   ./cleanup.sh              # Limpieza interactiva (pregunta qué limpiar)
#   ./cleanup.sh --all        # Limpia TODO (Azure + Local + Terraform)
#   ./cleanup.sh --azure      # Solo recursos Azure (AKS, ACR, Resource Group)
#   ./cleanup.sh --local      # Solo recursos locales (Kind, imágenes, namespace)
#   ./cleanup.sh --terraform  # Solo destruir con Terraform
#
# Variables de entorno opcionales:
#   RESOURCE_GROUP   — Nombre del resource group (default: rg-microservices)
#   ACR_NAME         — Nombre del ACR (default: myacrregistry)
#   AKS_NAME         — Nombre del cluster AKS (default: aks-microservices)
#   KIND_CLUSTER     — Nombre del cluster Kind (default: microservices)
# ============================================================

set -euo pipefail

# ── Colores ──
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# ── Variables por defecto ──
RESOURCE_GROUP="${RESOURCE_GROUP:-rg-microservices}"
ACR_NAME="${ACR_NAME:-myacrregistry}"
AKS_NAME="${AKS_NAME:-aks-microservices}"
KIND_CLUSTER="${KIND_CLUSTER:-microservices}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TERRAFORM_DIR="$SCRIPT_DIR/../terraform"

# ── Funciones auxiliares ──
info()    { echo -e "${CYAN}[INFO]${NC} $*"; }
success() { echo -e "${GREEN}[OK]${NC} $*"; }
warn()    { echo -e "${YELLOW}[WARN]${NC} $*"; }
error()   { echo -e "${RED}[ERROR]${NC} $*"; }

confirm() {
    local prompt="$1"
    read -rp "$(echo -e "${YELLOW}$prompt [y/N]: ${NC}")" response
    [[ "$response" =~ ^[Yy]$ ]]
}

# ── 1. Limpiar namespace de Kubernetes ──
cleanup_k8s_namespace() {
    info "Eliminando namespace 'microservices' de Kubernetes..."

    if kubectl get namespace microservices &>/dev/null; then
        kubectl delete namespace microservices --timeout=120s 2>/dev/null || {
            warn "Timeout eliminando namespace. Forzando eliminación de recursos..."
            kubectl delete all --all -n microservices --timeout=60s 2>/dev/null || true
            kubectl delete pvc --all -n microservices --timeout=60s 2>/dev/null || true
            kubectl delete configmap --all -n microservices --timeout=60s 2>/dev/null || true
            kubectl delete secret --all -n microservices --timeout=60s 2>/dev/null || true
            kubectl delete ingress --all -n microservices --timeout=60s 2>/dev/null || true
            kubectl delete namespace microservices --timeout=60s 2>/dev/null || true
        }
        success "Namespace 'microservices' eliminado."
    else
        warn "Namespace 'microservices' no existe. Saltando."
    fi
}

# ── 2. Eliminar NGINX Ingress Controller ──
cleanup_ingress_controller() {
    info "Eliminando NGINX Ingress Controller..."

    if kubectl get namespace ingress-nginx &>/dev/null; then
        kubectl delete namespace ingress-nginx --timeout=120s 2>/dev/null || {
            warn "No se pudo eliminar ingress-nginx namespace."
        }
        success "NGINX Ingress Controller eliminado."
    else
        warn "NGINX Ingress Controller no encontrado. Saltando."
    fi
}

# ── 3. Eliminar cluster AKS ──
cleanup_aks() {
    info "Eliminando cluster AKS '$AKS_NAME'..."

    if ! command -v az &>/dev/null; then
        error "Azure CLI (az) no está instalado. No se puede eliminar AKS."
        return 1
    fi

    if az aks show --resource-group "$RESOURCE_GROUP" --name "$AKS_NAME" &>/dev/null; then
        az aks delete \
            --resource-group "$RESOURCE_GROUP" \
            --name "$AKS_NAME" \
            --yes \
            --no-wait
        success "Eliminación de AKS '$AKS_NAME' iniciada (puede tardar varios minutos)."
    else
        warn "Cluster AKS '$AKS_NAME' no encontrado. Saltando."
    fi
}

# ── 4. Eliminar ACR ──
cleanup_acr() {
    info "Eliminando Azure Container Registry '$ACR_NAME'..."

    if az acr show --name "$ACR_NAME" --resource-group "$RESOURCE_GROUP" &>/dev/null; then
        az acr delete \
            --name "$ACR_NAME" \
            --resource-group "$RESOURCE_GROUP" \
            --yes
        success "ACR '$ACR_NAME' eliminado."
    else
        warn "ACR '$ACR_NAME' no encontrado. Saltando."
    fi
}

# ── 5. Eliminar Resource Group completo (elimina TODO dentro) ──
cleanup_resource_group() {
    info "Eliminando Resource Group '$RESOURCE_GROUP' y TODOS sus recursos..."

    if az group show --name "$RESOURCE_GROUP" &>/dev/null; then
        az group delete \
            --name "$RESOURCE_GROUP" \
            --yes \
            --no-wait
        success "Eliminación del Resource Group '$RESOURCE_GROUP' iniciada."
        info "Esto eliminará: AKS, ACR, Service Bus, IPs públicas, discos, etc."
        info "La eliminación completa puede tardar 5-10 minutos."
    else
        warn "Resource Group '$RESOURCE_GROUP' no encontrado. Saltando."
    fi
}

# ── 6. Limpiar kubectl context de AKS ──
cleanup_kubectl_context() {
    info "Limpiando contexto kubectl de AKS..."

    local ctx="$AKS_NAME"
    if kubectl config get-contexts "$ctx" &>/dev/null; then
        kubectl config delete-context "$ctx" 2>/dev/null || true
        success "Contexto kubectl '$ctx' eliminado."
    else
        warn "Contexto kubectl '$ctx' no encontrado. Saltando."
    fi

    # Eliminar cluster y user de kubeconfig
    kubectl config delete-cluster "$ctx" 2>/dev/null || true
    kubectl config delete-user "clusterUser_${RESOURCE_GROUP}_${AKS_NAME}" 2>/dev/null || true
}

# ── 7. Eliminar cluster Kind (local) ──
cleanup_kind() {
    info "Eliminando cluster Kind '$KIND_CLUSTER'..."

    if ! command -v kind &>/dev/null; then
        warn "Kind no está instalado. Saltando."
        return 0
    fi

    if kind get clusters 2>/dev/null | grep -q "^${KIND_CLUSTER}$"; then
        KIND_EXPERIMENTAL_PROVIDER=podman kind delete cluster --name "$KIND_CLUSTER" 2>/dev/null \
            || kind delete cluster --name "$KIND_CLUSTER" 2>/dev/null \
            || warn "No se pudo eliminar cluster Kind."
        success "Cluster Kind '$KIND_CLUSTER' eliminado."
    else
        warn "Cluster Kind '$KIND_CLUSTER' no encontrado. Saltando."
    fi
}

# ── 8. Eliminar imágenes Docker/Podman locales ──
cleanup_local_images() {
    info "Eliminando imágenes Docker/Podman de la demo..."

    local images=(
        "product-service:latest"
        "order-service:latest"
        "gateway:latest"
        "${ACR_NAME}.azurecr.io/product-service:latest"
        "${ACR_NAME}.azurecr.io/order-service:latest"
        "${ACR_NAME}.azurecr.io/gateway:latest"
        "localhost/product-service:latest"
        "localhost/order-service:latest"
        "localhost/gateway:latest"
    )

    local runtime="docker"
    if ! command -v docker &>/dev/null; then
        if command -v podman &>/dev/null; then
            runtime="podman"
        else
            warn "Ni Docker ni Podman encontrados. Saltando limpieza de imágenes."
            return 0
        fi
    fi

    for img in "${images[@]}"; do
        if $runtime image inspect "$img" &>/dev/null; then
            $runtime rmi "$img" 2>/dev/null || true
            success "Imagen '$img' eliminada."
        fi
    done
}

# ── 9. Destruir infraestructura con Terraform ──
cleanup_terraform() {
    info "Destruyendo infraestructura con Terraform..."

    if ! command -v terraform &>/dev/null; then
        warn "Terraform no está instalado. Saltando."
        return 0
    fi

    if [[ ! -d "$TERRAFORM_DIR" ]]; then
        warn "Directorio Terraform no encontrado en '$TERRAFORM_DIR'. Saltando."
        return 0
    fi

    if [[ ! -f "$TERRAFORM_DIR/.terraform/terraform.tfstate" ]] && \
       [[ ! -f "$TERRAFORM_DIR/terraform.tfstate" ]] && \
       [[ ! -d "$TERRAFORM_DIR/.terraform" ]]; then
        warn "Terraform no inicializado (sin .terraform/ ni tfstate). Saltando."
        return 0
    fi

    pushd "$TERRAFORM_DIR" > /dev/null

    if [[ -f "terraform.tfstate" ]] || [[ -f ".terraform/terraform.tfstate" ]]; then
        terraform destroy -auto-approve
        success "Terraform destroy completado."
    else
        warn "No hay estado de Terraform. Saltando destroy."
    fi

    # Limpiar archivos generados por Terraform
    if confirm "¿Eliminar archivos de estado y caché de Terraform?"; then
        rm -rf .terraform/ .terraform.lock.hcl terraform.tfstate terraform.tfstate.backup 2>/dev/null || true
        success "Archivos de Terraform limpiados."
    fi

    popd > /dev/null
}

# ── Resumen de lo que se va a limpiar ──
show_summary() {
    echo ""
    echo -e "${CYAN}╔══════════════════════════════════════════════════════════╗${NC}"
    echo -e "${CYAN}║       LIMPIEZA DE DEMO - Microservicios .NET 2025      ║${NC}"
    echo -e "${CYAN}╠══════════════════════════════════════════════════════════╣${NC}"
    echo -e "${CYAN}║${NC} Recursos Azure:                                        ${CYAN}║${NC}"
    echo -e "${CYAN}║${NC}   • Resource Group:  ${YELLOW}$RESOURCE_GROUP${NC}"
    echo -e "${CYAN}║${NC}   • AKS Cluster:     ${YELLOW}$AKS_NAME${NC}"
    echo -e "${CYAN}║${NC}   • ACR Registry:    ${YELLOW}$ACR_NAME${NC}"
    echo -e "${CYAN}║${NC}   • Service Bus, IPs, discos asociados               ${CYAN}║${NC}"
    echo -e "${CYAN}║${NC} Recursos locales:                                     ${CYAN}║${NC}"
    echo -e "${CYAN}║${NC}   • Namespace K8s:   ${YELLOW}microservices${NC}"
    echo -e "${CYAN}║${NC}   • Kind cluster:    ${YELLOW}$KIND_CLUSTER${NC}"
    echo -e "${CYAN}║${NC}   • Imágenes Docker/Podman de la demo                ${CYAN}║${NC}"
    echo -e "${CYAN}║${NC}   • Contexto kubectl de AKS                          ${CYAN}║${NC}"
    echo -e "${CYAN}╚══════════════════════════════════════════════════════════╝${NC}"
    echo ""
}

# ── Modo interactivo ──
interactive_cleanup() {
    show_summary

    echo -e "${RED}⚠  ADVERTENCIA: Esta acción es IRREVERSIBLE.${NC}"
    echo ""

    # Kubernetes namespace
    if confirm "¿Eliminar namespace 'microservices' de Kubernetes?"; then
        cleanup_k8s_namespace
        cleanup_ingress_controller
    fi

    # Azure
    if command -v az &>/dev/null && az account show &>/dev/null 2>&1; then
        echo ""
        if confirm "¿Eliminar TODOS los recursos Azure (Resource Group '$RESOURCE_GROUP')?"; then
            cleanup_resource_group
            cleanup_kubectl_context
        fi
    else
        warn "Azure CLI no disponible o no autenticado. Saltando limpieza Azure."
    fi

    # Terraform
    if [[ -d "$TERRAFORM_DIR/.terraform" ]] || [[ -f "$TERRAFORM_DIR/terraform.tfstate" ]]; then
        echo ""
        if confirm "¿Destruir infraestructura con Terraform?"; then
            cleanup_terraform
        fi
    fi

    # Kind
    if command -v kind &>/dev/null; then
        echo ""
        if confirm "¿Eliminar cluster Kind '$KIND_CLUSTER'?"; then
            cleanup_kind
        fi
    fi

    # Imágenes locales
    echo ""
    if confirm "¿Eliminar imágenes Docker/Podman de la demo?"; then
        cleanup_local_images
    fi
}

# ── Limpieza completa Azure ──
azure_cleanup() {
    show_summary
    echo -e "${RED}⚠  Eliminando TODOS los recursos Azure...${NC}"

    if ! command -v az &>/dev/null; then
        error "Azure CLI (az) no está instalado."
        exit 1
    fi

    if ! az account show &>/dev/null 2>&1; then
        error "No estás autenticado en Azure. Ejecuta: az login"
        exit 1
    fi

    cleanup_k8s_namespace
    cleanup_ingress_controller
    cleanup_resource_group
    cleanup_kubectl_context
}

# ── Limpieza local ──
local_cleanup() {
    info "Limpiando recursos locales..."
    cleanup_k8s_namespace
    cleanup_ingress_controller
    cleanup_kind
    cleanup_local_images
}

# ── Limpieza total ──
all_cleanup() {
    show_summary
    echo -e "${RED}⚠  MODO COMPLETO: Eliminando TODO (Azure + Local + Terraform)${NC}"
    echo ""

    if ! confirm "¿Estás SEGURO de que quieres eliminar TODOS los recursos?"; then
        info "Cancelado."
        exit 0
    fi

    # Kubernetes
    cleanup_k8s_namespace
    cleanup_ingress_controller

    # Terraform (antes de borrar RG por si tiene estado)
    cleanup_terraform

    # Azure
    if command -v az &>/dev/null && az account show &>/dev/null 2>&1; then
        cleanup_resource_group
        cleanup_kubectl_context
    else
        warn "Azure CLI no disponible. Saltando limpieza Azure."
    fi

    # Local
    cleanup_kind
    cleanup_local_images

    echo ""
    success "=========================================="
    success "  ¡Limpieza completa finalizada!"
    success "=========================================="
    echo ""
    info "Verifica que no queden recursos huérfanos:"
    echo "  az group list -o table"
    echo "  kubectl config get-contexts"
    echo "  kind get clusters"
}

# ── Main ──
case "${1:-}" in
    --all)
        all_cleanup
        ;;
    --azure)
        azure_cleanup
        ;;
    --local)
        local_cleanup
        ;;
    --terraform)
        cleanup_terraform
        ;;
    --help|-h)
        echo "Uso: $0 [--all|--azure|--local|--terraform|--help]"
        echo ""
        echo "  (sin args)    Modo interactivo — pregunta qué limpiar"
        echo "  --all         Limpia TODO (Azure + Local + Terraform)"
        echo "  --azure       Solo recursos Azure (Resource Group completo)"
        echo "  --local       Solo recursos locales (Kind, imágenes, namespace)"
        echo "  --terraform   Solo destruir con Terraform"
        echo ""
        echo "Variables de entorno:"
        echo "  RESOURCE_GROUP   (default: rg-microservices)"
        echo "  ACR_NAME         (default: myacrregistry)"
        echo "  AKS_NAME         (default: aks-microservices)"
        echo "  KIND_CLUSTER     (default: microservices)"
        ;;
    *)
        interactive_cleanup
        ;;
esac
