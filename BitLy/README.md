# URL Shortener Microservices

This is an educational project demonstrating a URL shortener system using microservices architecture in C# with ASP.NET Core.

## Architecture

- **Write Service**: Handles URL shortening requests (POST /urls)
- **Read Service**: Handles URL redirection (GET /{shortCode})
- **Shared Library**: Contains common models, data context, and utilities
- **Database**: PostgreSQL for persistence
- **Cache**: Redis for unique global counter

## Features

- Shorten long URLs to short codes using base62 encoding
- Custom aliases support
- Expiration dates for shortened URLs
- High availability and scalability design

## APIs

### Write Service (Port 8080)

**POST /urls**
```json
{
  "long_url": "https://example.com",
  "custom_alias": "optional",
  "expiration_date": "2024-12-31T23:59:59Z"
}
```
Response:
```json
{
  "short_url": "https://short.ly/abc123"
}
```

### Read Service (Port 8081)

**GET /{shortCode}**
- Redirects to the original URL (302 redirect)
- Returns 410 Gone if expired or not found

## Local Development

### Prerequisites

- **.NET 8.0 SDK**: Download and install from [Microsoft's official site](https://dotnet.microsoft.com/download/dotnet/8.0). For macOS ARM64 (M1/M2), ensure you download the ARM64 version.
- **Docker and Docker Compose**: Install from [Docker's site](https://www.docker.com/get-started).

### Option 1: Run with Docker Compose (Recommended)

This is the easiest way as it handles all dependencies automatically.

1. **Clone the repository**:
   ```bash
   git clone <repository-url>
   cd BitLy
   ```

2. **Start all services**:
   ```bash
   docker-compose up --build
   ```
   This will build the images, start PostgreSQL, Redis, WriteService (port 8080), and ReadService (port 8081).

3. **Initialize the database** (first run only):
   - The services will create the database schema automatically using EF Core migrations.
   - If needed, you can run migrations manually by connecting to the PostgreSQL container.

4. **Test the services**:
   - **Shorten a URL**:
     ```bash
     curl -X POST http://localhost:8080/urls \
       -H "Content-Type: application/json" \
       -d '{"long_url":"https://google.com"}'
     ```
     Response: `{"shortUrl":"https://short.ly/abc123"}`

   - **Access the shortened URL**:
     Open `http://localhost:8081/abc123` in your browser - it should redirect to Google.

### Option 2: Run Services Manually

If you prefer to run services directly (requires .NET SDK):

1. **Start dependencies with Docker**:
   ```bash
   docker-compose up -d postgres redis
   ```

2. **Run database migrations**:
   ```bash
   cd Shared
   dotnet ef database update
   cd ..
   ```

3. **Run Write Service**:
   ```bash
   cd WriteService
   dotnet run
   ```
   Runs on http://localhost:5000 (or configured port).

4. **Run Read Service** (in another terminal):
   ```bash
   cd ReadService
   dotnet run
   ```
   Runs on http://localhost:5001 (or configured port).

5. **Test as above**, adjusting ports if needed.

### Troubleshooting

- **.NET installation issues**: Ensure you're using the ARM64 version for Apple Silicon Macs.
- **Port conflicts**: If ports 8080/8081 are in use, modify `docker-compose.yml`.
- **Database connection**: Check PostgreSQL logs with `docker-compose logs postgres`.
- **Redis connection**: Ensure Redis is running on port 6379.

## Docker Deployment

1. Build and run: `docker-compose up --build`

## Kubernetes Deployment (Minikube)

### Prerequisites
- [Minikube](https://minikube.sigs.k8s.io/docs/start/) and `kubectl` installed

### Quick start

```bash
# 1. Start Minikube (first time only)
minikube start

# 2. Build images into Minikube's Docker daemon and deploy everything
./k8s/deploy-minikube.sh

# 3. Scale out write and read services to 3 replicas
./k8s/deploy-minikube.sh --scale 3
```

The script prints the Minikube IP and NodePort URLs when it finishes:
- **WriteService**: `http://<minikube-ip>:30080`
- **ReadService**:  `http://<minikube-ip>:30081`

Get the IP at any time with `minikube ip`.

### Manual steps (what the script does)

```bash
# Point your shell to Minikube's Docker daemon so images are available in-cluster
eval $(minikube docker-env)

# Build images
docker build -t bitly-writeservice:latest -f WriteService/Dockerfile .
docker build -t bitly-readservice:latest  -f ReadService/Dockerfile  .

# Apply all manifests
kubectl apply -f k8s/

# Scale horizontally
kubectl scale deployment writeservice --replicas=3
kubectl scale deployment readservice  --replicas=3

# Verify
kubectl get pods
kubectl get services
```

### Test after deployment

```bash
MINIKUBE_IP=$(minikube ip)

# Shorten a URL
curl -s -X POST "http://${MINIKUBE_IP}:30080/urls" \
     -H "Content-Type: application/json" \
     -d '{"long_url":"https://example.com"}'

# Follow the redirect
curl -v "http://${MINIKUBE_IP}:30081/<shortCode>"
```

## AWS Deployment

For production deployment on AWS:

1. Use Amazon EKS for Kubernetes
2. Use Amazon RDS for PostgreSQL
3. Use Amazon ElastiCache for Redis
4. Use API Gateway or ALB for routing

## API Gateway

For production, consider using:
- Kong (free and open-source)
- NGINX
- AWS API Gateway
- Azure API Management

## Database Schema

The `short_urls` table contains:
- id (bigint, primary key)
- long_url (varchar)
- short_code (varchar, unique)
- custom_alias (varchar, unique, nullable)
- expiration_date (timestamp, nullable)
- created_at (timestamp)

## Scaling Considerations

- Horizontal scaling of services
- Database read replicas
- Redis clustering
- CDN for static assets
- Rate limiting and caching layers

## Contributing

This is an educational project. Feel free to contribute improvements!
