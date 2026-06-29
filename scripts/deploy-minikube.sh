#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OPENAI_API_KEY="${OPENAI_API_KEY:-}"
MEMORY_MB="${MEMORY_MB:-8192}"
CPUS="${CPUS:-4}"

APP_DEPLOYMENTS=(
  api-gateway identity context-service agent-service mcp-gateway
  mcp-sql mcp-files mcp-jira audit-service notification-service web
)

require() {
  command -v "$1" >/dev/null 2>&1 || { echo "Required command not found: $1"; exit 1; }
}

use_minikube_docker() {
  echo "==> Using Minikube Docker daemon..."
  eval "$(minikube docker-env)"
  if [[ "${MINIKUBE_ACTIVE_DOCKERD:-}" != "minikube" ]]; then
    echo "Failed to switch to Minikube Docker."
    exit 1
  fi
}

verify_images() {
  local missing=()
  for image in "$@"; do
    if ! docker image inspect "$image" >/dev/null 2>&1; then
      missing+=("$image")
    fi
  done
  if ((${#missing[@]} > 0)); then
    echo "Missing images in Minikube Docker: ${missing[*]}"
    exit 1
  fi
}

wait_pods() {
  local label=$1
  local timeout=$2
  echo "    waiting for ${label} (${timeout})..."
  if ! kubectl -n nexusai wait --for=condition=ready pod -l "$label" --timeout="$timeout"; then
    echo ""
    echo "Pods not ready. Current status:"
    kubectl -n nexusai get pods
    echo "Check: kubectl describe pod -n nexusai -l ${label}"
    exit 1
  fi
}

wait_deployment() {
  local name=$1
  local timeout=$2
  echo "    waiting for deployment/${name} (${timeout})..."
  if ! kubectl -n nexusai rollout status "deployment/${name}" --timeout="$timeout"; then
    echo ""
    echo "Deployment not ready. Current status:"
    kubectl -n nexusai get pods
    echo "Check: kubectl describe deployment/${name} -n nexusai"
    exit 1
  fi
}

require minikube
require kubectl
require docker

echo "==> Ensuring Minikube is running (${MEMORY_MB} MB / ${CPUS} CPUs)..."
if ! minikube status --format='{{.Host}}' 2>/dev/null | grep -q Running; then
  minikube start --memory="$MEMORY_MB" --cpus="$CPUS" --driver=docker
fi

use_minikube_docker

cd "$REPO_ROOT"

build_image() {
  local name=$1
  local dockerfile=$2
  shift 2
  echo "==> Building ${name}..."
  docker build -t "$name" -f "$dockerfile" "$@" .
}

build_image nexusai/api-gateway:local infra/docker/Dockerfile.gateway
build_image nexusai/identity:local infra/docker/Dockerfile.identity
build_image nexusai/context-service:local infra/docker/Dockerfile.context
build_image nexusai/agent-service:local infra/docker/Dockerfile.agent
build_image nexusai/mcp-gateway:local infra/docker/Dockerfile.mcp-gateway
build_image nexusai/mcp-sql:local infra/docker/Dockerfile.mcp-sql
build_image nexusai/mcp-files:local infra/docker/Dockerfile.mcp-files
build_image nexusai/mcp-jira:local infra/docker/Dockerfile.mcp-jira
build_image nexusai/audit-service:local infra/docker/Dockerfile.audit
build_image nexusai/notification-service:local infra/docker/Dockerfile.notification
build_image nexusai/web:local infra/docker/Dockerfile.web \
  --build-arg VITE_API_BASE_URL=http://localhost:5000 \
  --build-arg VITE_KEYCLOAK_URL=http://localhost:8080 \
  --build-arg VITE_KEYCLOAK_REALM=nexusai \
  --build-arg VITE_KEYCLOAK_CLIENT_ID=nexusai-web

verify_images \
  nexusai/api-gateway:local nexusai/identity:local nexusai/context-service:local \
  nexusai/agent-service:local nexusai/mcp-gateway:local nexusai/mcp-sql:local \
  nexusai/mcp-files:local nexusai/mcp-jira:local nexusai/audit-service:local \
  nexusai/notification-service:local nexusai/web:local

echo "==> Applying Kubernetes manifests..."
kubectl apply -k infra/minikube

echo "==> Restarting app deployments to pick up local images..."
for deployment in "${APP_DEPLOYMENTS[@]}"; do
  kubectl -n nexusai rollout restart "deployment/${deployment}" >/dev/null
done

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

echo "==> Waiting for pods (first deploy can take 10+ minutes while SQL Server image downloads)..."
wait_pods "app=redis" "120s"
wait_pods "app=rabbitmq" "300s"
wait_pods "app=keycloak" "300s"
wait_pods "app=sqlserver" "900s"
for deployment in "${APP_DEPLOYMENTS[@]}"; do
  wait_deployment "$deployment" "300s"
done

echo "==> Starting port-forwards in background..."
chmod +x scripts/port-forward-minikube.sh scripts/teardown-minikube.sh
"$REPO_ROOT/scripts/port-forward-minikube.sh" --background

cat <<EOF

NexusAI is deployed on Minikube (single command — includes frontend + port-forwards).

Open: http://localhost:5173  (demo / demo)

Teardown: ./scripts/teardown-minikube.sh
Watch pods: kubectl -n nexusai get pods -w
EOF
