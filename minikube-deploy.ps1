# Deploy NexusAI to Minikube (starts Minikube if needed, builds, applies, port-forwards).
# Usage: .\minikube-deploy.ps1
#        $env:OPENAI_API_KEY = "sk-..."; .\minikube-deploy.ps1

$ErrorActionPreference = "Stop"
$RepoRoot = $PSScriptRoot
& "$RepoRoot\scripts\deploy-minikube.ps1" @args
