param(
    [string]$OpenAiApiKey = $env:OPENAI_API_KEY,
    [int]$MemoryMb = 8192,
    [int]$Cpus = 4
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

function Require-Command($name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $name"
    }
}

Require-Command minikube
Require-Command kubectl
Require-Command docker

Write-Host "==> Ensuring Minikube is running ($MemoryMb MB / $Cpus CPUs)..."
$status = minikube status --format='{{.Host}}' 2>$null
if ($status -ne "Running") {
    minikube start --memory=$MemoryMb --cpus=$Cpus --driver=docker
}

Write-Host "==> Using Minikube Docker daemon..."
minikube docker-env | Invoke-Expression

$images = @(
    @{ Name = "nexusai/api-gateway:local"; Dockerfile = "infra/docker/Dockerfile.gateway" },
    @{ Name = "nexusai/identity:local"; Dockerfile = "infra/docker/Dockerfile.identity" },
    @{ Name = "nexusai/context-service:local"; Dockerfile = "infra/docker/Dockerfile.context" },
    @{ Name = "nexusai/agent-service:local"; Dockerfile = "infra/docker/Dockerfile.agent" },
    @{ Name = "nexusai/mcp-gateway:local"; Dockerfile = "infra/docker/Dockerfile.mcp-gateway" },
    @{ Name = "nexusai/mcp-sql:local"; Dockerfile = "infra/docker/Dockerfile.mcp-sql" },
    @{ Name = "nexusai/mcp-files:local"; Dockerfile = "infra/docker/Dockerfile.mcp-files" },
    @{ Name = "nexusai/audit-service:local"; Dockerfile = "infra/docker/Dockerfile.audit" },
    @{ Name = "nexusai/notification-service:local"; Dockerfile = "infra/docker/Dockerfile.notification" }
)

Push-Location $RepoRoot
try {
    foreach ($image in $images) {
        Write-Host "==> Building $($image.Name)..."
        docker build -t $image.Name -f $image.Dockerfile .
        if ($LASTEXITCODE -ne 0) { throw "Docker build failed for $($image.Name)" }
    }

    Write-Host "==> Applying Kubernetes manifests..."
    kubectl apply -k infra/minikube
    if ($LASTEXITCODE -ne 0) { throw "kubectl apply failed" }

    if ($OpenAiApiKey) {
        Write-Host "==> Updating OpenAI secret..."
        kubectl -n nexusai create secret generic nexusai-secrets `
            --from-literal=mssql-sa-password='Your_strong_password123' `
            --from-literal=openai-api-key=$OpenAiApiKey `
            --dry-run=client -o yaml | kubectl apply -f -
        kubectl -n nexusai rollout restart deployment/agent-service | Out-Null
    }
    else {
        Write-Warning "OPENAI_API_KEY not set — chat will not work until you update nexusai-secrets."
    }

    Write-Host "==> Waiting for infrastructure pods..."
    kubectl -n nexusai wait --for=condition=ready pod -l app=redis --timeout=120s
    kubectl -n nexusai wait --for=condition=ready pod -l app=rabbitmq --timeout=180s
    kubectl -n nexusai wait --for=condition=ready pod -l app=keycloak --timeout=300s
    kubectl -n nexusai wait --for=condition=ready pod -l app=sqlserver --timeout=300s

    Write-Host ""
    Write-Host "NexusAI is deployed on Minikube."
    Write-Host ""
    Write-Host "Next: start port forwards in a NEW terminal (required for browser access):"
    Write-Host "  .\scripts\port-forward-minikube.ps1"
    Write-Host ""
    Write-Host "Then open:"
    Write-Host "  API Gateway : http://localhost:5000"
    Write-Host "  Keycloak    : http://localhost:8080"
    Write-Host "  Jaeger UI   : http://localhost:16686"
    Write-Host ""
    Write-Host "Watch pods: kubectl -n nexusai get pods -w"
    Write-Host "Frontend  : cd src\NexusAI.Web; copy .env .env.local; npm run dev"
}
finally {
    Pop-Location
}
