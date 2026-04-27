# GoPuff — Copilot Instructions

## Project
GoPuff-style instant delivery system: availability lookup + order placement backed by
PostgreSQL and two Redis caches.

## Architecture

```
Client ──► API GW (auth, routing, rate-lim)
              ├──► GET  /availability  ──► AvailabilityService (port 8080)
              └──► POST /orders        ──► OrderService        (port 8081)

AvailabilityService ──► NearbyService (port 8082, internal)
OrderService        ──► NearbyService (port 8082, internal)

NearbyService   ──► redis-fc  (FC cache)     ──► PostgreSQL (fallback)
AvailabilityService ──► redis-inv (Inv cache) ──► PostgreSQL (fallback)
OrderService    ──► redis-inv (Inv cache)     ──► PostgreSQL (writes)
```

## Services

| Service | Port | Responsibility |
|---------|------|----------------|
| NearbyService | 8082 | Returns FC IDs within a configurable radius using Haversine in-memory |
| AvailabilityService | 8080 | Aggregates item stock across nearby FCs; cache-aside via Inventory Cache |
| OrderService | 8081 | Places orders with strong consistency (atomic conditional UPDATE) |
| Shared | — | EF Core models/DbContext, FcCacheService, InventoryCacheService, NearbyClient, Haversine |

## Key Design Decisions

- **Two Redis instances**: `redis-fc` (FC list cache, TTL 5 min) and `redis-inv` (inventory cache, TTL 30 s)
- **Geospatial**: FCs stored in `redis-fc` as a full list; NearbyService applies Haversine formula in memory — no PostGIS required
- **Fixed radius**: 30 miles default, configurable per request
- **Strong consistency**: `UPDATE inventories SET quantity = quantity - N WHERE quantity >= N` — atomic row-level update prevents overselling without explicit locks
- **Cache invalidation**: OrderService eagerly evicts affected `inv:itemId:fcId` keys after a successful order
- **Seed data**: 8 FCs across NYC, Chicago, LA; 10 items; varied inventory per FC (see seed/seed.sql and Shared/Data/DbSeeder.cs)

## Running Locally

```bash
# Docker Compose (recommended)
docker compose up --build

# Swagger UIs
open http://localhost:8080/swagger  # AvailabilityService
open http://localhost:8081/swagger  # OrderService
open http://localhost:8082/swagger  # NearbyService

# Check availability near Times Square
curl 'http://localhost:8080/availability?lat=40.758&lon=-73.9855&itemIds=1,2,3'

# Place an order
curl -X POST http://localhost:8081/orders \
  -H 'Content-Type: application/json' \
  -d '{"userId":"alice","deliveryLat":40.758,"deliveryLon":-73.9855,"items":[{"itemId":1,"quantity":2},{"itemId":3,"quantity":1}]}'
```

## Running on Minikube

```bash
cd GoPuff
chmod +x k8s/deploy-minikube.sh
./k8s/deploy-minikube.sh
```

## DB Schema

Tables: `fulfillment_centres`, `items`, `inventories`, `orders`, `order_items`
Column names are explicit snake_case (configured in `OnModelCreating`).
Schema is created by EF Core `EnsureCreated()` on service startup.
