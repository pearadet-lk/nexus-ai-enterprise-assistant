param(
    [string]$Namespace = "nexusai",
    [switch]$Background,
    [switch]$Stop
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$StateDir = Join-Path $RepoRoot ".minikube"
$PidFile = Join-Path $StateDir "port-forward.pids"

function Require-Command($name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $name"
    }
}

function Stop-MinikubePortForwards {
    if (-not (Test-Path $PidFile)) {
        return
    }

    Write-Host "==> Stopping Minikube port-forwards..."
    Get-Content $PidFile | ForEach-Object {
        if ($_ -match '^(\d+):') {
            $procId = [int]$Matches[1]
            Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
        }
    }
    Remove-Item $PidFile -Force -ErrorAction SilentlyContinue
}

function Get-MinikubeForwards {
    return @(
        @{ Service = "web"; LocalPort = 5173; RemotePort = 80 },
        @{ Service = "api-gateway"; LocalPort = 5000; RemotePort = 8080 },
        @{ Service = "keycloak"; LocalPort = 8080; RemotePort = 8080 },
        @{ Service = "jaeger"; LocalPort = 16686; RemotePort = 16686 },
        @{ Service = "rabbitmq"; LocalPort = 15672; RemotePort = 15672 }
    )
}

function Start-MinikubePortForwardsBackground {
    param([string]$Namespace)

    Require-Command kubectl

    kubectl get namespace $Namespace 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Namespace '$Namespace' not found. Run .\scripts\deploy-minikube.ps1 first."
    }

    Stop-MinikubePortForwards
    New-Item -ItemType Directory -Force -Path $StateDir | Out-Null

    Write-Host "==> Starting kubectl port-forward in background (namespace: $Namespace)"

    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($forward in Get-MinikubeForwards) {
        $arguments = @(
            "port-forward",
            "-n", $Namespace,
            "svc/$($forward.Service)",
            "$($forward.LocalPort):$($forward.RemotePort)"
        )

        $process = Start-Process -FilePath "kubectl" -ArgumentList $arguments -PassThru -WindowStyle Hidden
        $lines.Add("$($process.Id):$($forward.LocalPort)") | Out-Null
        Write-Host "  localhost:$($forward.LocalPort) -> $($forward.Service):$($forward.RemotePort)"
    }

    $lines | Set-Content $PidFile
    Start-Sleep -Seconds 2
}

if ($Stop) {
    Stop-MinikubePortForwards
    exit 0
}

if ($Background) {
    Start-MinikubePortForwardsBackground -Namespace $Namespace
    Write-Host ""
    Write-Host "Port forwards running in background. PIDs saved to .minikube/port-forward.pids"
    Write-Host "  Web UI      : http://localhost:5173"
    Write-Host "  API Gateway : http://localhost:5000"
    Write-Host "  Keycloak    : http://localhost:8080"
    Write-Host "  Jaeger UI   : http://localhost:16686"
    Write-Host "  RabbitMQ UI : http://localhost:15672"
    Write-Host ""
    Write-Host "Stop forwards: .\scripts\teardown-minikube.ps1  (or .\scripts\port-forward-minikube.ps1 -Stop)"
    exit 0
}

# Foreground mode (manual debugging)
Require-Command kubectl

kubectl get namespace $Namespace 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Namespace '$Namespace' not found. Run .\scripts\deploy-minikube.ps1 first."
}

$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()

Write-Host "==> Starting kubectl port-forward (namespace: $Namespace)"

foreach ($forward in Get-MinikubeForwards) {
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
Write-Host "Port forwards active. Press Ctrl+C to stop."
Write-Host ""

try {
    while ($true) {
        foreach ($process in $processes) {
            if ($process.HasExited -and $process.ExitCode -ne 0) {
                throw "Port forward exited unexpectedly (PID $($process.Id))."
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
