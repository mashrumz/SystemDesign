# URL Shortener Microservices

An educational project demonstrating a scalable URL shortener built with microservices in C# / ASP.NET Core.

## Architecture

```
                +--------------------------------------------------+
                |                    Clients                       |
                +----------+-------------------+-------------------+
                           | POST /urls        | GET /{shortCode}
                           v                  v
               +--------------------+  +--------------------+
               |   Write Service    |  |   Read Service     |
               |   (port 8080)      |  |   (port 8081)      |
               +-----+------+-------+  +--------+-----------+
                     |      |                   |
          +----------+      +-------------------+
          |                 |                   |
          v                 v                   v
  +--------------+   +-----------+   +------------------+
  |  PostgreSQL  |   |   Redis   |   |   Redis Cache    |
  |  (storage)   |   | (counter) |   |  (url look-up)   |
  +--------------+   +-----------+   +------------------+
```

### Services

| Service | Responsibility |
|---------|---------------|
| **WriteService** | Accepts `POST /urls`, generates a short code via a global Redis counter, persists to PostgreSQL, and writes the mapping to the URL cache |
| **ReadService** | Accepts `GET /{shortCode}`, checks the URL cache first; on a miss falls back to PostgreSQL and populates the cache |
| **Shared** | Common EF Core models, `UrlShortenerDbContext`, `Base62Encoder`, `RedisCounter`, `UrlCacheService` |

### Infrastructure

| Component | Purpose | Config |
|-----------|---------|--------|
| **PostgreSQL** | Persistent storage for all short URL records | Port 5432 |
| **Redis (counter)** | Monotonically incrementing global ID — guarantees unique short codes across all WriteService replicas | Port 6379 |
| **Redis (cache)** | High-speed URL look-up cache for ReadService | Port 6380 · `allkeys-lru` · 256 MB |

### Caching Strategy

**Write-through** (WriteService): after persisting to PostgreSQL, the mapping is immediately written to the Redis cache so the very first read is already a cache hit.

**Cache-aside** (ReadService): on each request Redis is checked first. On a miss, PostgreSQL is queried and the result is written back to the cache for subsequent reads.

**Eviction policy**: `allkeys-lru` — the cache Redis instance evicts the least-recently-used key when it reaches the memory limit (256 MB). Popular short codes stay hot and stale ones are pruned automatically.

**TTL**: when a short URL has an `expiration_date`, the cache entry TTL is set to the remaining duration (`expiration_date - now`). Entries without an expiry date are kept indefinitely (subject only to LRU eviction).

## Features

- Base62 short-code generation backed by a Redis global counter
- Custom aliases
- Optional expiration dates with automatic TTL propagation to the cache
- In-memory URL cache capable of >10k reads/sec per replica
- Horizontal scaling of both services
- Kubernetes manifests with HPA

## API

### Write Service (port 8080)

**POST /urls**
```json
{
  "long_url": "https://example.com",
  "custom_alias": "my-alias",
  "expiration_date": "2026-12-31T23:59:59Z"
}
```
Response:
```json
{ "short_url": "http://localhost:8081/abc123" }
```

### Read Service (port 8081)

**GET /{shortCode}**
- `302 Found` — redirects to the original URL
- `410 Gone` — short code has expired
- `404 Not Found` — short code does not exist

## Local Development

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/get-started)

### Run with Docker Compose

```bash
docker compose up --build
```

Starts PostgreSQL, Redis (counter), Redis (cache), WriteService (`localhost:8080`), and ReadService (`localhost:8081`). The database schema is created automatically on first startup.

**Shorten a URL:**
```bash
curl -s -X POST http://localhost:8080/urls \
  -H "Content-Type: application/json" \
  -d '{"long_url":"https://example.com"}'
```

**Follow the redirect:**
```bash
curl -v http://localhost:8081/<shortCode>
```

**With expiration (TTL propagated to cache automatically):**
```bash
curl -s -X POST http://localhost:8080/urls \
  -H "Content-Type: application/json" \
  -d '{"long_url":"https://example.com","expiration_date":"2026-06-01T00:00:00Z"}'
```

### Run services manually

```bash
# Start infrastructure only
docker compose up -d postgres redis redis-cache

# Write Service
cd WriteService && dotnet run

# Read Service (separate terminal)
cd ReadService && dotnet run
```

### Troubleshooting

- **No cache logs** — ensure images were rebuilt after the latest code changes (`docker compose up --build`).
- **Port conflicts** — adjust port mappings in `docker-compose.yml`.
- **DB not ready** — both services retry the connection up to 10 times on startup.

## Kubernetes Deployment

The manifests use **Kustomize** to share a common base with environment-specific overlays:

```
k8s/
  base/                    # shared: Deployments, Services, HPA
  overlays/
    local/                 # Minikube: NodePort, imagePullPolicy:Never, hardcoded env, Postgres+Redis pods
    aws/                   # AWS EKS: LoadBalancer, imagePullPolicy:Always, ECR images, secretKeyRef
```

### Local (Minikube)

**Prerequisites:** [Minikube](https://minikube.sigs.k8s.io/docs/start/) and `kubectl`

```bash
minikube start
./k8s/deploy-minikube.sh           # build images + apply local overlay
./k8s/deploy-minikube.sh --scale 3 # scale to 3 replicas each
```

The script prints the service URLs when done:
- **WriteService**: `http://<minikube-ip>:30080`
- **ReadService**: `http://<minikube-ip>:30081`

**Test after deployment:**
```bash
MINIKUBE_IP=$(minikube ip)

curl -s -X POST "http://${MINIKUBE_IP}:30080/urls" \
     -H "Content-Type: application/json" \
     -d '{"long_url":"https://example.com"}'

curl -v "http://${MINIKUBE_IP}:30081/<shortCode>"
```

### AWS (EKS)

**Prerequisites:** `awscli`, `eksctl`, `kubectl`, `helm`, `docker`

Infra used on AWS:

| Component | AWS Service |
|-----------|-------------|
| Postgres | Amazon RDS (PostgreSQL 15) |
| Redis counter + cache | Amazon ElastiCache (Redis 7) |
| Container images | Amazon ECR |
| Kubernetes cluster | Amazon EKS |
| Service exposure | AWS Load Balancer (ALB) |

**One-time cluster setup** (only needed once):

```bash
# Create EKS cluster
eksctl create cluster --name bitly-cluster --region eu-north-1 \
  --nodegroup-name standard-workers --node-type t3.small \
  --nodes 2 --nodes-min 2 --nodes-max 6 --managed

# Create ECR repositories
aws ecr create-repository --repository-name bitly/writeservice
aws ecr create-repository --repository-name bitly/readservice

# Create RDS and ElastiCache (see full setup guide for commands)
# Then store credentials as a Kubernetes Secret:
kubectl create namespace bitly
kubectl create secret generic bitly-secrets -n bitly \
  --from-literal=postgres-connection="Host=<RDS_ENDPOINT>;Database=urlshortener;Username=postgres;Password=<PASSWORD>" \
  --from-literal=redis-counter-connection="<COUNTER_REDIS_ENDPOINT>:6379" \
  --from-literal=redis-cache-connection="<CACHE_REDIS_ENDPOINT>:6379"

# Install the AWS Load Balancer Controller
eksctl utils associate-iam-oidc-provider --region eu-north-1 --cluster bitly-cluster --approve

aws iam create-policy \
  --policy-name AWSLoadBalancerControllerIAMPolicy \
  --policy-document file://iam_policy.json

eksctl create iamserviceaccount \
  --cluster bitly-cluster \
  --namespace kube-system \
  --name aws-load-balancer-controller \
  --attach-policy-arn arn:aws:iam::<ACCOUNT_ID>:policy/AWSLoadBalancerControllerIAMPolicy \
  --override-existing-serviceaccounts \
  --approve

helm repo add eks https://aws.github.io/eks-charts
helm repo update
helm install aws-load-balancer-controller eks/aws-load-balancer-controller \
  -n kube-system \
  --set clusterName=bitly-cluster \
  --set serviceAccount.create=false \
  --set serviceAccount.name=aws-load-balancer-controller

# Install Metrics Server (required for HPA)
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml

# Fix: the default metrics-server manifest is missing the k8s-app label required by
# its Service selector. Patch the Deployment to add it, then wait for rollout:
kubectl patch deployment metrics-server -n kube-system --type='json' \
  -p='[{"op":"add","path":"/spec/template/metadata/labels/k8s-app","value":"metrics-server"}]'
kubectl rollout status deployment/metrics-server -n kube-system
```

**Deploy / redeploy** (run from `BitLy/` root):

```bash
export AWS_REGION=eu-north-1
./k8s/overlays/aws/deploy-aws.sh
```

The script builds and pushes images to ECR, then applies the AWS overlay. Get the public Load Balancer URLs (~2 min to provision):

```bash
kubectl get svc -n bitly
```

## Database Schema

Table `short_urls`:

| Column | Type | Notes |
|--------|------|-------|
| `id` | bigint | Primary key |
| `long_url` | varchar | Original URL |
| `short_code` | varchar | Unique base62 code |
| `custom_alias` | varchar | Optional, unique |
| `expiration_date` | timestamp | Nullable; drives cache TTL |
| `created_at` | timestamp | Auto-set |

## Scaling Notes

- **ReadService** is stateless and cache-backed — scale horizontally without coordination.
- **Redis cache** uses `allkeys-lru` so no manual key management is needed.
- **Redis counter** is a single instance; for multi-region, replace with range-based ID allocation per WriteService node.
- **WriteService** replicas are safe to run in parallel — the atomic Redis `INCR` guarantees unique IDs.
