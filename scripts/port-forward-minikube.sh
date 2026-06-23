#!/usr/bin/env bash
# Forward Minikube services to the same localhost ports used by local docker-compose dev.
set -euo pipefail

NAMESPACE="${NAMESPACE:-nexusai}"
PIDS=()

cleanup() {
  echo ""
  echo "Stopping port forwards..."
  for pid in "${PIDS[@]}"; do
    kill "$pid" 2>/dev/null || true
  done
}

trap cleanup EXIT INT TERM

require() {
  command -v "$1" >/dev/null 2>&1 || { echo "Required command not found: $1"; exit 1; }
}

require kubectl

if ! kubectl get namespace "$NAMESPACE" >/dev/null 2>&1; then
  echo "Namespace '$NAMESPACE' not found. Run ./scripts/deploy-minikube.sh first."
  exit 1
fi

forward() {
  local service=$1
  local local_port=$2
  local remote_port=$3
  kubectl port-forward -n "$NAMESPACE" "svc/${service}" "${local_port}:${remote_port}" >/dev/null 2>&1 &
  PIDS+=($!)
  echo "  localhost:${local_port} -> ${service}:${remote_port}"
}

echo "==> Starting kubectl port-forward (namespace: ${NAMESPACE})"
forward api-gateway 5000 8080
forward keycloak 8080 8080
forward jaeger 16686 16686
forward rabbitmq 15672 15672

cat <<EOF

Port forwards active (same ports as local dev):

  API Gateway : http://localhost:5000
  Keycloak    : http://localhost:8080
  Jaeger UI   : http://localhost:16686
  RabbitMQ UI : http://localhost:15672

Frontend: use src/NexusAI.Web/.env (or copy to .env.local) and npm run dev
Press Ctrl+C to stop all forwards.
EOF

wait
