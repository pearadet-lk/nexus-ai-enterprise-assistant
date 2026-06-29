param(
    [string]$OpenAiApiKey = $env:OPENAI_API_KEY,
    [int]$MemoryMb = 8192,
    [int]$Cpus = 4
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

$AppDeployments = @(
    "api-gateway", "identity", "context-service", "agent-service", "mcp-gateway",
    "mcp-sql", "mcp-files", "mcp-jira", "audit-service", "notification-service", "web"
)

$Images = @(
    @{ Name = "nexusai/api-gateway:local"; Dockerfile = "infra/docker/Dockerfile.gateway"; BuildArgs = @() },
    @{ Name = "nexusai/identity:local"; Dockerfile = "infra/docker/Dockerfile.identity"; BuildArgs = @() },
    @{ Name = "nexusai/context-service:local"; Dockerfile = "infra/docker/Dockerfile.context"; BuildArgs = @() },
    @{ Name = "nexusai/agent-service:local"; Dockerfile = "infra/docker/Dockerfile.agent"; BuildArgs = @() },
    @{ Name = "nexusai/mcp-gateway:local"; Dockerfile = "infra/docker/Dockerfile.mcp-gateway"; BuildArgs = @() },
    @{ Name = "nexusai/mcp-sql:local"; Dockerfile = "infra/docker/Dockerfile.mcp-sql"; BuildArgs = @() },
    @{ Name = "nexusai/mcp-files:local"; Dockerfile = "infra/docker/Dockerfile.mcp-files"; BuildArgs = @() },
    @{ Name = "nexusai/mcp-jira:local"; Dockerfile = "infra/docker/Dockerfile.mcp-jira"; BuildArgs = @() },
    @{ Name = "nexusai/audit-service:local"; Dockerfile = "infra/docker/Dockerfile.audit"; BuildArgs = @() },
    @{ Name = "nexusai/notification-service:local"; Dockerfile = "infra/docker/Dockerfile.notification"; BuildArgs = @() },
    @{
        Name = "nexusai/web:local"
        Dockerfile = "infra/docker/Dockerfile.web"
        BuildArgs = @(
            "VITE_API_BASE_URL=http://localhost:5000",
            "VITE_KEYCLOAK_URL=http://localhost:8080",
            "VITE_KEYCLOAK_REALM=nexusai",
            "VITE_KEYCLOAK_CLIENT_ID=nexusai-web"
        )
    }
)

function Require-Command($name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $name"
    }
}

function Use-MinikubeDocker {
    Write-Host "==> Using Minikube Docker daemon..."
    minikube docker-env --shell powershell | Invoke-Expression
    if ($env:MINIKUBE_ACTIVE_DOCKERD -ne "minikube") {
        throw "Failed to switch to Minikube Docker. Run: minikube docker-env --shell powershell | Invoke-Expression"
    }
}

function Test-MinikubeImages {
    param([array]$ExpectedImages)

    $missing = @()
    foreach ($image in $ExpectedImages) {
        docker image inspect $image.Name 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            $missing += $image.Name
        }
    }

    if ($missing.Count -gt 0) {
        throw "Missing images in Minikube Docker: $($missing -join ', ')"
    }
}

function Wait-NexusaiPods {
    param(
        [string]$Label,
        [string]$Timeout
    )

    Write-Host "    waiting for $Label ($Timeout)..."
    kubectl -n nexusai wait --for=condition=ready pod -l $Label --timeout=$Timeout
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Pods not ready. Current status:"
        kubectl -n nexusai get pods
        throw "Timed out waiting for pods with label '$Label'. Check: kubectl describe pod -n nexusai -l $Label"
    }
}

function Wait-DeploymentRollout {
    param(
        [string]$Name,
        [string]$Timeout
    )

    Write-Host "    waiting for deployment/$Name ($Timeout)..."
    kubectl -n nexusai rollout status "deployment/$Name" --timeout=$Timeout
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Deployment not ready. Current status:"
        kubectl -n nexusai get pods
        throw "Timed out waiting for deployment '$Name'. Check: kubectl describe deployment/$Name -n nexusai"
    }
}

function Test-LocalPortsAvailable {
    param([int[]]$Ports)

    $blocked = @()
    foreach ($port in $Ports) {
        if (netstat -ano | Select-String -Pattern ":$port\s+.*LISTENING" -Quiet) {
            $blocked += $port
        }
    }

    if ($blocked.Count -gt 0) {
        throw "Port(s) already in use: $($blocked -join ', '). Stop Docker full stack or local dev servers before deploying Minikube."
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

Use-MinikubeDocker

Push-Location $RepoRoot
try {
    foreach ($image in $Images) {
        Write-Host "==> Building $($image.Name)..."
        $buildArgs = @("build", "-t", $image.Name, "-f", $image.Dockerfile)
        foreach ($arg in $image.BuildArgs) {
            $buildArgs += @("--build-arg", $arg)
        }
        $buildArgs += "."
        & docker @buildArgs
        if ($LASTEXITCODE -ne 0) { throw "Docker build failed for $($image.Name)" }
    }

    Test-MinikubeImages -ExpectedImages $Images

    Write-Host "==> Applying Kubernetes manifests..."
    kubectl apply -k infra/minikube
    if ($LASTEXITCODE -ne 0) { throw "kubectl apply failed" }

    Write-Host "==> Restarting app deployments to pick up local images..."
    foreach ($deployment in $AppDeployments) {
        kubectl -n nexusai rollout restart "deployment/$deployment" | Out-Null
    }

    if ($OpenAiApiKey) {
        Write-Host "==> Updating OpenAI secret..."
        kubectl -n nexusai create secret generic nexusai-secrets `
            --from-literal=mssql-sa-password='Your_strong_password123' `
            --from-literal=openai-api-key=$OpenAiApiKey `
            --dry-run=client -o yaml | kubectl apply -f -
        kubectl -n nexusai rollout restart deployment/agent-service | Out-Null
    }
    else {
        Write-Warning "OPENAI_API_KEY not set - chat will not work until you update nexusai-secrets."
    }

    Write-Host "==> Waiting for pods (first deploy can take 10+ minutes while SQL Server image downloads)..."
    Wait-NexusaiPods -Label "app=redis" -Timeout "120s"
    Wait-NexusaiPods -Label "app=rabbitmq" -Timeout "300s"
    Wait-NexusaiPods -Label "app=keycloak" -Timeout "300s"
    Wait-NexusaiPods -Label "app=sqlserver" -Timeout "900s"
    foreach ($deployment in $AppDeployments) {
        Wait-DeploymentRollout -Name $deployment -Timeout "300s"
    }

    Test-LocalPortsAvailable -Ports @(5173, 5000, 8080, 16686, 15672)

    Write-Host "==> Starting port-forwards in background..."
    & "$PSScriptRoot/port-forward-minikube.ps1" -Background

    Write-Host ""
    Write-Host "NexusAI is deployed on Minikube (includes frontend + port-forwards)."
    Write-Host ""
    Write-Host "Open: http://localhost:5173  (demo / demo)"
    Write-Host ""
    Write-Host "Teardown: .\scripts\teardown-minikube.ps1"
    Write-Host "Watch pods: kubectl -n nexusai get pods -w"
}
finally {
    Pop-Location
}
