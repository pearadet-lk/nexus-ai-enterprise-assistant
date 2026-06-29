#!/usr/bin/env bash
# Teardown NexusAI on Minikube (stops port-forwards, removes cluster, stops Minikube).
# Usage: ./minikube-teardown.sh
#        ./minikube-teardown.sh --keep-minikube

set -euo pipefail
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec "$REPO_ROOT/scripts/teardown-minikube.sh" "$@"
