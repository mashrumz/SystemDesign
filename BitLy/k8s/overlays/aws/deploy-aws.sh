#!/usr/bin/env bash
# Usage: ./k8s/overlays/aws/deploy-aws.sh
# Pushes images to ECR and applies the AWS overlay to the current kubectl context (EKS).

set -e

AWS_REGION="${AWS_REGION:-eu-north-1}"
AWS_ACCOUNT=$(aws sts get-caller-identity --query Account --output text)
ECR_BASE="${AWS_ACCOUNT}.dkr.ecr.${AWS_REGION}.amazonaws.com"

echo "==> Logging in to ECR..."
aws ecr get-login-password --region "$AWS_REGION" | \
  docker login --username AWS --password-stdin "$ECR_BASE"

echo "==> Building images for linux/amd64 (EKS node architecture)..."
docker build --platform linux/amd64 -t "${ECR_BASE}/bitly/writeservice:latest" -f WriteService/Dockerfile .
docker build --platform linux/amd64 -t "${ECR_BASE}/bitly/readservice:latest"  -f ReadService/Dockerfile  .

echo "==> Pushing images to ECR..."
docker push "${ECR_BASE}/bitly/writeservice:latest"
docker push "${ECR_BASE}/bitly/readservice:latest"

echo "==> Creating namespace bitly (idempotent)..."
kubectl create namespace bitly --dry-run=client -o yaml | kubectl apply -f -

echo "==> Applying Kubernetes manifests (aws overlay)..."
kubectl apply -k k8s/overlays/aws/

echo "==> Waiting for writeservice to be ready..."
kubectl rollout status deployment/writeservice -n bitly --timeout=180s

echo "==> Waiting for readservice to be ready..."
kubectl rollout status deployment/readservice -n bitly --timeout=180s

echo ""
echo "==> All services are up."
echo ""
echo "Fetching LoadBalancer addresses (may take ~2 min to provision)..."
kubectl get svc -n bitly
