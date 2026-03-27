# System Design Implementations

A collection of common system design problems implemented from scratch, each in its own folder.

## Projects

| Project | Description | Stack |
|---------|-------------|-------|
| [BitLy](./BitLy) | URL shortener with high read/write throughput | .NET 8, PostgreSQL, Redis, Docker, Kubernetes |

## Structure

Each folder is a self-contained implementation with its own:
- Source code
- `README.md` explaining the design decisions
- `docker-compose.yml` for local development
- Kubernetes manifests where applicable

## Getting Started

Clone the repo and navigate into any project folder:

```bash
git clone https://github.com/mashrumz/SystemDesign.git
cd SystemDesign/BitLy
docker compose up
```

Refer to each project's `README.md` for specific setup instructions.
