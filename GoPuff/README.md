# GoPuff — Instant Delivery System Design

An educational implementation of a GoPuff-style instant delivery platform.
The focus is on **availability checking** and **order placement** backed by
PostgreSQL and two dedicated Redis caches.

---

## High-Level Design

```
Client
  │
  ▼
API Gateway & Load Balancer
(auth · routing · rate-limiting)
  │
  ├─── GET  /availability ──► AvailabilityService (8080)
  │                                │                │
  │                        NearbyService (8082)   redis-inv
  │                                │                │
  │                           redis-fc           PostgreSQL
  │
  └─── POST /orders ────────► OrderService (8081)
                                     │                │
                             NearbyService (8082)  redis-inv
                                                      │
                                                  PostgreSQL
```

---

## Services

| Service | Port | Role |
|---------|------|------|
| **NearbyService** | 8082 | Returns FC IDs within a given radius using Haversine in memory |
| **AvailabilityService** | 8080 | Aggregates stock across nearby FCs; cache-aside via Inventory Cache |
| **OrderService** | 8081 | Places orders with strong consistency (atomic conditional UPDATE) |
| **Shared** | — | EF Core models/DbContext, cache services, HTTP client, Haversine util |

---

## Database Schema

```
fulfillment_centres   items
──────────────────    ──────────────────
id   PK               id   PK
name                  name  INDEX
lat  INDEX
lon  INDEX

inventories           orders
───────────────────   ──────────────────
item_id  PK, FK       id         PK
fc_id    PK, FK       user_id
quantity              delivery_lat
                      delivery_lon
                      created_at

order_items
────────────────────
order_id  PK, FK
item_id   PK, FK
fc_id     PK, FK
quantity
```

---

## Key Design Decisions

### Geospatial — Haversine in-memory, no PostGIS

All Fulfilment Centre records (id, name, lat, lon) are cached in **redis-fc** as a
single JSON list (`fcs:all`, TTL 5 min). NearbyService loads the list on startup and
reloads it on a cache miss. The Haversine distance formula is applied in-memory, so
no PostGIS extension is needed. With ~10 k FCs the in-memory scan is still
sub-millisecond; beyond that a PostGIS GiST index would be the natural upgrade path.

### Fixed 30-mile radius

A single configurable `radiusMiles` parameter (default **30 miles**) controls the
delivery radius. There is no travel-time constraint.

### Two Redis instances

| Instance | Key pattern | TTL | Purpose |
|----------|-------------|-----|---------|
| `redis-fc` (port 6379) | `fcs:all` | 5 min | Full FC list for in-memory Haversine |
| `redis-inv` (port 6380) | `inv:{itemId}:{fcId}` | 30 s | Per-FC inventory quantities (cache-aside) |

The two instances are kept separate so an FC topology change can invalidate `redis-fc`
without touching the much higher-churn inventory cache.

### Strong consistency for orders

```sql
UPDATE inventories
SET    quantity = quantity - N
WHERE  item_id = ? AND fc_id = ? AND quantity >= N
```

This single-statement UPDATE is atomic at the row level in PostgreSQL — two concurrent
requests for the last unit cannot both succeed. No explicit `SELECT FOR UPDATE` is
needed. The entire order (all line items) runs inside a single transaction so a
mid-order failure rolls back all previously claimed inventory.

After a successful commit, OrderService **eagerly invalidates** the affected
`inv:{itemId}:{fcId}` keys so the next availability read reflects real stock.

### Seed data

Reference data is seeded once by NearbyService on startup (`DbSeeder.SeedAsync`).
A corresponding SQL file is at [seed/seed.sql](seed/seed.sql) for manual use.

| City | FCs |
|------|-----|
| New York (Manhattan, Brooklyn, Queens, Bronx) | 4 |
| Chicago (Loop, Lincoln Park) | 2 |
| Los Angeles (Downtown, Santa Monica) | 2 |

10 items stocked across all FCs with intentionally varied quantities to make
availability and out-of-stock scenarios easy to test.

---

## Running Locally

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

### Docker Compose (recommended)

```bash
# From the GoPuff/ directory
docker compose up --build
```

| Service | Swagger UI |
|---------|-----------|
| AvailabilityService | http://localhost:8080/swagger |
| OrderService | http://localhost:8081/swagger |
| NearbyService | http://localhost:8082/swagger |

### Example requests

```bash
# Check availability near Times Square, NYC — items 1, 2, 3
curl 'http://localhost:8080/availability?lat=40.758&lon=-73.9855&itemIds=1,2,3'

# Place an order
curl -X POST http://localhost:8081/orders \
  -H 'Content-Type: application/json' \
  -d '{
    "userId": "alice",
    "deliveryLat": 40.758,
    "deliveryLon": -73.9855,
    "items": [
      { "itemId": 1, "quantity": 2 },
      { "itemId": 3, "quantity": 1 }
    ]
  }'

# Check availability near LA Downtown — items 7, 8
curl 'http://localhost:8080/availability?lat=34.052&lon=-118.244&itemIds=7,8'

# Force empty radius (middle of the ocean) — expects empty response
curl 'http://localhost:8080/availability?lat=0&lon=0&itemIds=1'
```

---

## Running on Minikube

```bash
# From the GoPuff/ directory
./k8s/deploy-minikube.sh

# Scale to 3 replicas
./k8s/deploy-minikube.sh --scale 3
```

NodePorts exposed by Minikube:

| Service | NodePort |
|---------|----------|
| AvailabilityService | 30180 |
| OrderService | 30181 |
| NearbyService | cluster-internal (no NodePort) |

---

## Project Structure

```
GoPuff/
├── docker-compose.yml
├── GoPuff.slnx
├── Shared/                         # Class library shared by all services
│   ├── Data/
│   │   ├── GoPuffDbContext.cs       # EF Core DbContext
│   │   └── DbSeeder.cs             # Reference data seed (idempotent)
│   ├── Models/                     # EF Core entities
│   │   ├── FulfillmentCentre.cs
│   │   ├── Inventory.cs
│   │   ├── Item.cs
│   │   ├── Order.cs
│   │   └── OrderItem.cs
│   └── Utils/
│       ├── Haversine.cs            # Great-circle distance formula
│       ├── FcCacheService.cs       # redis-fc — full FC list cache
│       ├── InventoryCacheService.cs# redis-inv — per-(item,fc) qty cache
│       └── NearbyClient.cs         # Typed HTTP client for NearbyService
├── NearbyService/
│   ├── Controllers/NearbyController.cs
│   ├── appsettings.json
│   ├── Dockerfile
│   └── Program.cs
├── AvailabilityService/
│   ├── Controllers/AvailabilityController.cs
│   ├── DTOs/AvailabilityResponse.cs
│   ├── appsettings.json
│   ├── Dockerfile
│   └── Program.cs
├── OrderService/
│   ├── Controllers/OrdersController.cs
│   ├── DTOs/PlaceOrderRequest.cs
│   ├── DTOs/PlaceOrderResponse.cs
│   ├── appsettings.json
│   ├── Dockerfile
│   └── Program.cs
├── seed/
│   └── seed.sql                    # Manual seed script (idempotent)
└── k8s/
    ├── postgres.yaml
    ├── redis.yaml
    ├── nearbyservice.yaml
    ├── availabilityservice.yaml
    ├── orderservice.yaml
    └── deploy-minikube.sh
```

---

## Configuration Reference

### NearbyService

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionStrings:DefaultConnection` | `Host=localhost;...` | PostgreSQL connection |
| `Redis:FcCacheConnectionString` | `localhost:6379` | redis-fc connection |

### AvailabilityService & OrderService

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionStrings:DefaultConnection` | `Host=localhost;...` | PostgreSQL connection |
| `Redis:InvCacheConnectionString` | `localhost:6380` | redis-inv connection |
| `Services:NearbyServiceUrl` | `http://localhost:8082` | NearbyService base URL |
