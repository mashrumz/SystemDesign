#!/usr/bin/env bash
# Usage: ./k8s/deploy-minikube.sh [--scale N]
# Builds both service images inside Minikube's Docker daemon and applies all manifests.

set -e

REPLICAS=1
while [[ "$#" -gt 0 ]]; do
  case $1 in
    --scale) REPLICAS="$2"; shift ;;
    *) echo "Unknown parameter: $1"; exit 1 ;;
  esac
  shift
done

echo "==> Pointing shell to Minikube's Docker daemon..."
eval "$(minikube docker-env)"

echo "==> Building WriteService image..."
docker build -t bitly-writeservice:latest -f WriteService/Dockerfile .

echo "==> Building ReadService image..."
docker build -t bitly-readservice:latest -f ReadService/Dockerfile .

echo "==> Applying Kubernetes manifests..."
kubectl apply -f k8s/postgres.yaml
kubectl apply -f k8s/redis.yaml
kubectl apply -f k8s/writeservice.yaml
kubectl apply -f k8s/readservice.yaml
kubectl apply -f k8s/hpa.yaml

echo "==> Waiting for postgres to be ready..."
kubectl rollout status deployment/postgres --timeout=120s

echo "==> Waiting for redis to be ready..."
kubectl rollout status deployment/redis --timeout=60s

echo "==> Waiting for redis-cache to be ready..."
kubectl rollout status deployment/redis-cache --timeout=60s

echo "==> Waiting for writeservice to be ready..."
kubectl rollout status deployment/writeservice --timeout=120s

echo "==> Waiting for readservice to be ready..."
kubectl rollout status deployment/readservice --timeout=120s

if [[ "$REPLICAS" -gt 1 ]]; then
  echo "==> Scaling to $REPLICAS replicas..."
  kubectl scale deployment writeservice --replicas="$REPLICAS"
  kubectl scale deployment readservice  --replicas="$REPLICAS"
fi

echo ""
echo "==> All services are up."
echo ""
MINIKUBE_IP=$(minikube ip)
echo "  WriteService: http://${MINIKUBE_IP}:30080"
echo "  ReadService:  http://${MINIKUBE_IP}:30081"
echo ""
echo "Example:"
echo "  curl -s -X POST http://${MINIKUBE_IP}:30080/urls \\"
echo "       -H 'Content-Type: application/json' \\"
echo "       -d '{\"long_url\":\"https://example.com\"}'"
