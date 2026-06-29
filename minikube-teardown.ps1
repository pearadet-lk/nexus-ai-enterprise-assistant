# Teardown NexusAI on Minikube (stops port-forwards, removes cluster, stops Minikube).
# Usage: .\minikube-teardown.ps1
#        .\minikube-teardown.ps1 -KeepMinikube

$ErrorActionPreference = "Stop"
$RepoRoot = $PSScriptRoot
& "$RepoRoot\scripts\teardown-minikube.ps1" @args
