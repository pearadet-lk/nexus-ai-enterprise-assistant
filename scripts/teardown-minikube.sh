#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
KEEP_MINIKUBE=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --keep-minikube) KEEP_MINIKUBE=true; shift ;;
    -h|--help)
      echo "Usage: $0 [--keep-minikube]"
      exit 0
      ;;
    *) echo "Unknown option: $1"; exit 1 ;;
  esac
done

cd "$REPO_ROOT"

echo "==> Stopping port-forwards..."
"$REPO_ROOT/scripts/port-forward-minikube.sh" --stop

echo "==> Removing NexusAI from cluster..."
kubectl delete -k infra/minikube --ignore-not-found=true

if ! $KEEP_MINIKUBE; then
  echo "==> Stopping Minikube..."
  minikube stop 2>/dev/null || true
fi

echo ""
echo "NexusAI Minikube teardown complete."
if $KEEP_MINIKUBE; then
  echo "Minikube cluster is still running (used --keep-minikube)."
fi
