param(
    [switch]$KeepMinikube
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $RepoRoot
try {
    Write-Host "==> Stopping port-forwards..."
    & "$PSScriptRoot/port-forward-minikube.ps1" -Stop

    Write-Host "==> Removing NexusAI from cluster..."
    kubectl delete -k infra/minikube --ignore-not-found=true
    if ($LASTEXITCODE -ne 0) { throw "kubectl delete failed" }

    if (-not $KeepMinikube) {
        Write-Host "==> Stopping Minikube..."
        minikube stop 2>$null
    }

    Write-Host ""
    Write-Host "NexusAI Minikube teardown complete."
    if ($KeepMinikube) {
        Write-Host "Minikube cluster is still running (used -KeepMinikube)."
    }
}
finally {
    Pop-Location
}
