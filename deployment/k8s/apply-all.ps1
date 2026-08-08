#!/usr/bin/env pwsh
# Apply all Kubernetes manifests in the correct order.
# Run from the repo root: .\deployment\k8s\apply-all.ps1

$ErrorActionPreference = "Stop"
$k8sDir = $PSScriptRoot

Write-Host "=== ERP Kubernetes Deploy ===" -ForegroundColor Cyan

# Namespace
kubectl apply -f "$k8sDir\namespace.yaml"

# Config + Secrets
kubectl apply -f "$k8sDir\configmap.yaml"
kubectl apply -f "$k8sDir\secrets.yaml"

# Infrastructure
Write-Host "`nDeploying infrastructure..." -ForegroundColor Yellow
kubectl apply -f "$k8sDir\infrastructure\redis.yaml"
kubectl apply -f "$k8sDir\infrastructure\rabbitmq.yaml"
kubectl apply -f "$k8sDir\infrastructure\kafka.yaml"
kubectl apply -f "$k8sDir\infrastructure\elasticsearch.yaml"
kubectl apply -f "$k8sDir\infrastructure\azurite.yaml"

Write-Host "Waiting for Redis..." -ForegroundColor Gray
kubectl wait --for=condition=ready pod -l app=redis -n erp --timeout=120s

Write-Host "Waiting for RabbitMQ..." -ForegroundColor Gray
kubectl wait --for=condition=ready pod -l app=rabbitmq -n erp --timeout=180s

Write-Host "Waiting for Kafka..." -ForegroundColor Gray
kubectl wait --for=condition=ready pod -l app=kafka -n erp --timeout=180s

Write-Host "Waiting for Elasticsearch..." -ForegroundColor Gray
kubectl wait --for=condition=ready pod -l app=elasticsearch -n erp --timeout=240s

# Backend services + frontend
Write-Host "`nDeploying services and frontend..." -ForegroundColor Yellow
kubectl apply -f "$k8sDir\deployments\"

# Ingress
kubectl apply -f "$k8sDir\ingress.yaml"

Write-Host "`n=== Deploy complete ===" -ForegroundColor Green
Write-Host "Checking pod status..." -ForegroundColor Cyan
kubectl get pods -n erp
