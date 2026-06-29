#!/usr/bin/env bash
# Deploy NexusAI to Minikube (starts Minikube if needed, builds, applies, port-forwards).
# Usage: ./minikube-deploy.sh
#        OPENAI_API_KEY=sk-... ./minikube-deploy.sh

set -euo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec "$REPO_ROOT/scripts/deploy-minikube.sh" "$@"
