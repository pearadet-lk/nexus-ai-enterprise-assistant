param(
    [string]$Namespace = "nexusai"
)

$ErrorActionPreference = "Stop"

function Require-Command($name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $name"
    }
}

Require-Command kubectl

kubectl get namespace $Namespace 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Namespace '$Namespace' not found. Run .\scripts\deploy-minikube.ps1 first."
}

$forwards = @(
    @{ Service = "api-gateway"; LocalPort = 5000; RemotePort = 8080 },
    @{ Service = "keycloak"; LocalPort = 8080; RemotePort = 8080 },
    @{ Service = "jaeger"; LocalPort = 16686; RemotePort = 16686 },
    @{ Service = "rabbitmq"; LocalPort = 15672; RemotePort = 15672 }
)

$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()

Write-Host "==> Starting kubectl port-forward (namespace: $Namespace)"

foreach ($forward in $forwards) {
    $arguments = @(
        "port-forward",
        "-n", $Namespace,
        "svc/$($forward.Service)",
        "$($forward.LocalPort):$($forward.RemotePort)"
    )

    $process = Start-Process -FilePath "kubectl" -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $processes.Add($process) | Out-Null
    Write-Host "  localhost:$($forward.LocalPort) -> $($forward.Service):$($forward.RemotePort)"
}

Write-Host ""
Write-Host "Port forwards active (same ports as local dev):"
Write-Host ""
Write-Host "  API Gateway : http://localhost:5000"
Write-Host "  Keycloak    : http://localhost:8080"
Write-Host "  Jaeger UI   : http://localhost:16686"
Write-Host "  RabbitMQ UI : http://localhost:15672"
Write-Host ""
Write-Host "Frontend: use src/NexusAI.Web/.env (or copy to .env.local) and npm run dev"
Write-Host "Press Ctrl+C to stop all forwards."
Write-Host ""

try {
    while ($true) {
        foreach ($process in $processes) {
            if ($process.HasExited -and $process.ExitCode -ne 0) {
                throw "Port forward for a service exited unexpectedly (PID $($process.Id))."
            }
        }
        Start-Sleep -Seconds 1
    }
}
finally {
    Write-Host ""
    Write-Host "Stopping port forwards..."
    foreach ($process in $processes) {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
}
