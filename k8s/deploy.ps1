<#
.SYNOPSIS
    Deploys Catalog API to the local minikube Kubernetes cluster end to end: starts minikube if
    needed, builds and loads the app images, applies every manifest in k8s/ in the correct
    dependency order, waits for everything to become healthy, and prints how to reach it.

.PARAMETER SkipBuild
    Skip `docker build` + `minikube image load` - use this on a re-run when the images are
    already fresh (e.g. you only changed a YAML file, not app code).

.PARAMETER SkipMinikubeStart
    Skip the minikube start/metrics-server check - use this if you manage the cluster's
    lifecycle yourself and just want the manifests (re-)applied.

.EXAMPLE
    .\k8s\deploy.ps1

.EXAMPLE
    .\k8s\deploy.ps1 -SkipBuild
#>
[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$SkipMinikubeStart
)

$ErrorActionPreference = "Stop"

$k8sDir = $PSScriptRoot
$repoRoot = Split-Path -Parent $k8sDir

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Invoke-Checked {
    param([string]$Description, [scriptblock]$Command)
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed (exit code $LASTEXITCODE)."
    }
}

# --- 1. Prerequisites ------------------------------------------------------
Write-Step "Checking prerequisites"
foreach ($tool in @("kubectl", "minikube", "docker")) {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) {
        throw "'$tool' is not on PATH. Install it before running this script."
    }
}
Write-Host "kubectl, minikube, docker all found." -ForegroundColor Green

# --- 2. Ensure the cluster is up -------------------------------------------
if (-not $SkipMinikubeStart) {
    Write-Step "Checking minikube cluster status"
    & kubectl get nodes 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Cluster not reachable - starting minikube (can take a few minutes)..." -ForegroundColor Yellow
        Invoke-Checked "minikube start" { minikube start --driver=docker --cpus=4 --memory=6000 }
    } else {
        Write-Host "minikube already running." -ForegroundColor Green
    }

    Write-Step "Ensuring metrics-server is enabled (needed for HPA CPU/memory metrics)"
    minikube addons enable metrics-server | Out-Null
}

# --- 3. Build and load images -----------------------------------------------
if (-not $SkipBuild) {
    Write-Step "Building catalog-api:local"
    Invoke-Checked "docker build (catalog-api)" {
        docker build -t catalog-api:local -f "$repoRoot/containers/api/Dockerfile" $repoRoot
    }

    Write-Step "Building catalog-worker:local"
    Invoke-Checked "docker build (catalog-worker)" {
        docker build -t catalog-worker:local -f "$repoRoot/containers/worker/Dockerfile" $repoRoot
    }

    Write-Step "Loading images into minikube"
    minikube image load catalog-api:local
    minikube image load catalog-worker:local
} else {
    Write-Host "Skipping image build/load (-SkipBuild)." -ForegroundColor Yellow
}

# --- 4. Apply manifests, in dependency order --------------------------------
Write-Step "Applying namespace, config, and secrets"
Invoke-Checked "apply namespace" { kubectl apply -f "$k8sDir/namespace.yaml" }
Invoke-Checked "apply config/secret" { kubectl apply -f "$k8sDir/configmap.yaml" -f "$k8sDir/secret.yaml" }

Write-Step "Applying stateful dependencies (SQL Server, Redis, RabbitMQ)"
Invoke-Checked "apply dependencies" {
    kubectl apply -f "$k8sDir/sqlserver.yaml" -f "$k8sDir/redis.yaml" -f "$k8sDir/rabbitmq.yaml"
}

Write-Step "Waiting for SQL Server to become ready (first run can take a couple of minutes)"
Invoke-Checked "wait for sqlserver" {
    kubectl wait --for=condition=ready pod -l app=sqlserver -n catalog --timeout=300s
}

Write-Step "Applying catalog-api, catalog-worker, and ingress"
Invoke-Checked "apply app tier" {
    kubectl apply -f "$k8sDir/catalog-api.yaml" -f "$k8sDir/catalog-worker.yaml" -f "$k8sDir/ingress.yaml"
}

Write-Step "Waiting for catalog-api and catalog-worker rollouts to finish"
# `kubectl rollout status` follows the Deployment's own rollout state machine, unlike
# `kubectl wait -l ...` which snapshots matching pod names once and then errors on any pod
# that gets replaced by the controller before the wait completes.
Invoke-Checked "rollout status (catalog-api)" {
    kubectl rollout status deployment/catalog-api -n catalog --timeout=180s
}
Invoke-Checked "rollout status (catalog-worker)" {
    kubectl rollout status deployment/catalog-worker -n catalog --timeout=180s
}

# --- 5. Final status ---------------------------------------------------------
Write-Step "Deployment complete - current status"
kubectl get pods -n catalog
kubectl get hpa -n catalog
kubectl get svc -n catalog

Write-Host ""
Write-Host "To reach the API from your machine, run in another terminal and leave it running:" -ForegroundColor Cyan
Write-Host "  kubectl port-forward -n catalog svc/catalog-api 8080:80" -ForegroundColor White
Write-Host "Then, e.g.:" -ForegroundColor Cyan
Write-Host "  curl http://localhost:8080/api/items" -ForegroundColor White
