#!/usr/bin/env bash
# Forward Minikube services to localhost. Use -Background from deploy script, or run standalone.
set -euo pipefail

NAMESPACE="${NAMESPACE:-nexusai}"
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STATE_DIR="$REPO_ROOT/.minikube"
PID_FILE="$STATE_DIR/port-forward.pids"
BACKGROUND=false
STOP_ONLY=false

usage() {
  echo "Usage: $0 [--background | --stop]"
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --background) BACKGROUND=true; shift ;;
    --stop) STOP_ONLY=true; shift ;;
    -h|--help) usage ;;
    *) usage ;;
  esac
done

require() {
  command -v "$1" >/dev/null 2>&1 || { echo "Required command not found: $1"; exit 1; }
}

stop_port_forwards() {
  if [[ ! -f "$PID_FILE" ]]; then
    return 0
  fi

  echo "==> Stopping Minikube port-forwards..."
  while IFS= read -r line; do
    pid="${line%%:*}"
    kill "$pid" 2>/dev/null || true
  done < "$PID_FILE"
  rm -f "$PID_FILE"
}

start_background() {
  require kubectl

  if ! kubectl get namespace "$NAMESPACE" >/dev/null 2>&1; then
    echo "Namespace '$NAMESPACE' not found. Run ./scripts/deploy-minikube.sh first."
    exit 1
  fi

  stop_port_forwards
  mkdir -p "$STATE_DIR"
  : > "$PID_FILE"

  echo "==> Starting kubectl port-forward in background (namespace: ${NAMESPACE})"

  forward() {
    local service=$1
    local local_port=$2
    local remote_port=$3
    kubectl port-forward -n "$NAMESPACE" "svc/${service}" "${local_port}:${remote_port}" >/dev/null 2>&1 &
    echo "$!:${local_port}" >> "$PID_FILE"
    echo "  localhost:${local_port} -> ${service}:${remote_port}"
  }

  forward web 5173 80
  forward api-gateway 5000 8080
  forward keycloak 8080 8080
  forward jaeger 16686 16686
  forward rabbitmq 15672 15672

  sleep 2

  cat <<EOF

Port forwards running in background. PIDs saved to .minikube/port-forward.pids
  Web UI      : http://localhost:5173
  API Gateway : http://localhost:5000
  Keycloak    : http://localhost:8080
  Jaeger UI   : http://localhost:16686
  RabbitMQ UI : http://localhost:15672

Stop forwards: ./scripts/teardown-minikube.sh  (or ./scripts/port-forward-minikube.sh --stop)
EOF
}

if $STOP_ONLY; then
  stop_port_forwards
  exit 0
fi

if $BACKGROUND; then
  start_background
  exit 0
fi

# Foreground mode
require kubectl
PIDS=()

cleanup() {
  echo ""
  echo "Stopping port forwards..."
  for pid in "${PIDS[@]}"; do
    kill "$pid" 2>/dev/null || true
  done
}

trap cleanup EXIT INT TERM

if ! kubectl get namespace "$NAMESPACE" >/dev/null 2>&1; then
  echo "Namespace '$NAMESPACE' not found. Run ./scripts/deploy-minikube.sh first."
  exit 1
fi

forward_fg() {
  local service=$1
  local local_port=$2
  local remote_port=$3
  kubectl port-forward -n "$NAMESPACE" "svc/${service}" "${local_port}:${remote_port}" >/dev/null 2>&1 &
  PIDS+=($!)
  echo "  localhost:${local_port} -> ${service}:${remote_port}"
}

echo "==> Starting kubectl port-forward (namespace: ${NAMESPACE})"
forward_fg web 5173 80
forward_fg api-gateway 5000 8080
forward_fg keycloak 8080 8080
forward_fg jaeger 16686 16686
forward_fg rabbitmq 15672 15672

echo ""
echo "Port forwards active. Press Ctrl+C to stop."
echo ""

wait
