#!/usr/bin/env bash
set -euo pipefail

NAMESPACE="${1:-microservices}"

if ! command -v kubectl >/dev/null 2>&1; then
  echo "Error: kubectl no esta instalado o no esta en PATH." >&2
  exit 1
fi

declare -a PIDS=()

cleanup() {
  echo ""
  echo "Deteniendo port-forwards..."
  for pid in "${PIDS[@]:-}"; do
    if kill -0 "$pid" >/dev/null 2>&1; then
      kill "$pid" >/dev/null 2>&1 || true
    fi
  done
}

start_port_forward() {
  local service="$1"
  local local_port="$2"
  local remote_port="$3"

  echo "Iniciando: svc/${service} ${local_port}:${remote_port} (ns=${NAMESPACE})"
  kubectl -n "$NAMESPACE" port-forward "svc/${service}" "${local_port}:${remote_port}" &
  PIDS+=("$!")
}

trap cleanup EXIT INT TERM

start_port_forward "gateway" "5010" "80"
start_port_forward "product-service" "5001" "5001"
start_port_forward "order-service" "5003" "5003"

echo ""
echo "Port-forwards activos. Presiona Ctrl+C para detenerlos."

wait
