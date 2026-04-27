#!/usr/bin/env bash
# Usage: ./k8s/deploy-minikube.sh [--scale N]
set -e

REPLICAS=1
while [[ "$#" -gt 0 ]]; do
  case $1 in
    --scale) REPLICAS="$2"; shift ;;
    *) echo "Unknown parameter: $1"; exit 1 ;;
  esac
  shift
done

echo "==> Pointing shell to Minikube Docker daemon..."
eval "$(minikube docker-env)"

echo "==> Building images..."
docker build -t gopuff-nearbyservice:latest       -f NearbyService/Dockerfile       .
docker build -t gopuff-availabilityservice:latest -f AvailabilityService/Dockerfile .
docker build -t gopuff-orderservice:latest        -f OrderService/Dockerfile        .

echo "==> Creating namespace gopuff (idempotent)..."
kubectl create namespace gopuff --dry-run=client -o yaml | kubectl apply -f -

echo "==> Applying manifests..."
kubectl apply -n gopuff -f k8s/postgres.yaml
kubectl apply -n gopuff -f k8s/redis.yaml
kubectl apply -n gopuff -f k8s/nearbyservice.yaml
kubectl apply -n gopuff -f k8s/availabilityservice.yaml
kubectl apply -n gopuff -f k8s/orderservice.yaml

echo "==> Waiting for rollouts..."
kubectl rollout status deployment/postgres             -n gopuff --timeout=120s
kubectl rollout status deployment/redis-fc             -n gopuff --timeout=60s
kubectl rollout status deployment/redis-inv            -n gopuff --timeout=60s
kubectl rollout status deployment/nearbyservice        -n gopuff --timeout=120s
kubectl rollout status deployment/availabilityservice  -n gopuff --timeout=120s
kubectl rollout status deployment/orderservice         -n gopuff --timeout=120s

if [[ "$REPLICAS" -gt 1 ]]; then
  echo "==> Scaling to $REPLICAS replicas..."
  kubectl scale deployment nearbyservice        -n gopuff --replicas="$REPLICAS"
  kubectl scale deployment availabilityservice  -n gopuff --replicas="$REPLICAS"
  kubectl scale deployment orderservice         -n gopuff --replicas="$REPLICAS"
fi

echo ""
echo "==> All services are up."
MINIKUBE_IP=$(minikube ip)
echo "  AvailabilityService : http://${MINIKUBE_IP}:30180/swagger"
echo "  OrderService        : http://${MINIKUBE_IP}:30181/swagger"
echo "  NearbyService       : cluster-internal only (no NodePort)"
echo ""
echo "Example requests:"
echo "  # Check availability near Times Square, NYC"
echo "  curl 'http://${MINIKUBE_IP}:30180/availability?lat=40.758&lon=-73.9855&itemIds=1,2,3'"
echo ""
echo "  # Place an order"
echo "  curl -X POST http://${MINIKUBE_IP}:30181/orders \\"
echo "    -H 'Content-Type: application/json' \\"
echo "    -d '{\"userId\":\"user-1\",\"deliveryLat\":40.758,\"deliveryLon\":-73.9855,\"items\":[{\"itemId\":1,\"quantity\":2}]}'"
