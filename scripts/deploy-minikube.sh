#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OPENAI_API_KEY="${OPENAI_API_KEY:-}"
MEMORY_MB="${MEMORY_MB:-8192}"
CPUS="${CPUS:-4}"

require() {
  command -v "$1" >/dev/null 2>&1 || { echo "Required command not found: $1"; exit 1; }
}

require minikube
require kubectl
require docker

echo "==> Ensuring Minikube is running (${MEMORY_MB} MB / ${CPUS} CPUs)..."
if ! minikube status --format='{{.Host}}' 2>/dev/null | grep -q Running; then
  minikube start --memory="$MEMORY_MB" --cpus="$CPUS" --driver=docker
fi

echo "==> Using Minikube Docker daemon..."
eval "$(minikube docker-env)"

cd "$REPO_ROOT"

images=(
  "nexusai/api-gateway:local|infra/docker/Dockerfile.gateway"
  "nexusai/identity:local|infra/docker/Dockerfile.identity"
  "nexusai/context-service:local|infra/docker/Dockerfile.context"
  "nexusai/agent-service:local|infra/docker/Dockerfile.agent"
  "nexusai/mcp-gateway:local|infra/docker/Dockerfile.mcp-gateway"
  "nexusai/mcp-sql:local|infra/docker/Dockerfile.mcp-sql"
  "nexusai/mcp-files:local|infra/docker/Dockerfile.mcp-files"
  "nexusai/audit-service:local|infra/docker/Dockerfile.audit"
  "nexusai/notification-service:local|infra/docker/Dockerfile.notification"
)

for entry in "${images[@]}"; do
  name="${entry%%|*}"
  dockerfile="${entry##*|}"
  echo "==> Building ${name}..."
  docker build -t "$name" -f "$dockerfile" .
done

echo "==> Applying Kubernetes manifests..."
kubectl apply -k infra/minikube

if [[ -n "$OPENAI_API_KEY" ]]; then
  echo "==> Updating OpenAI secret..."
  kubectl -n nexusai create secret generic nexusai-secrets \
    --from-literal=mssql-sa-password='Your_strong_password123' \
    --from-literal=openai-api-key="$OPENAI_API_KEY" \
    --dry-run=client -o yaml | kubectl apply -f -
  kubectl -n nexusai rollout restart deployment/agent-service
else
  echo "WARNING: OPENAI_API_KEY not set — chat will not work until you update nexusai-secrets."
fi

echo "==> Waiting for infrastructure pods..."
kubectl -n nexusai wait --for=condition=ready pod -l app=redis --timeout=120s
kubectl -n nexusai wait --for=condition=ready pod -l app=rabbitmq --timeout=180s
kubectl -n nexusai wait --for=condition=ready pod -l app=keycloak --timeout=300s
kubectl -n nexusai wait --for=condition=ready pod -l app=sqlserver --timeout=300s

cat <<EOF

NexusAI is deployed on Minikube.

Next: start port forwards in a NEW terminal (required for browser access):
  ./scripts/port-forward-minikube.sh

Then open:
  API Gateway : http://localhost:5000
  Keycloak    : http://localhost:8080
  Jaeger UI   : http://localhost:16686

Watch pods: kubectl -n nexusai get pods -w
Frontend  : cd src/NexusAI.Web && cp .env .env.local && npm run dev
EOF
