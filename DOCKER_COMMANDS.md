# Docker Compose – Command Reference

All commands are run from the **repo root** (`Catalog.API/`) where `docker-compose.yml` lives.

---

## Stack Overview

| Service | Container | Image / Build | Port(s) |
|---|---|---|---|
| SQL Server 2022 | `catalog_db` | `mssql/server:2022-latest` | `1433` |
| RabbitMQ | `catalog_esb` | `rabbitmq:3-management-alpine` | `5672`, `15672` |
| Redis | `catalog_cache` | `redis:alpine` | `6379` |
| Identity API | `identity_api` | build: `containers/identity/Dockerfile` | `5105` |
| Catalog API | `catalog_api` | build: `containers/api/Dockerfile` | `5000` |

---

## Quick Start

```powershell
# 1. Build images and start every service in detached mode
docker compose up --build -d

# 2. Tail logs for all services
docker compose logs -f

# 3. Stop all services (containers removed, volumes kept)
docker compose down
```

---

## Build Commands

```powershell
# Build all service images (no cache – forces fresh pull of base layers)
docker compose build --no-cache

# Build a single service image
docker compose build catalog_api
docker compose build identity_api

# Build + start in one step
docker compose up --build -d
```

---

## Start / Stop / Restart

```powershell
# Start all services (uses cached images)
docker compose up -d

# Start specific services only
docker compose up -d catalog_db catalog_cache catalog_esb

# Stop all running containers (images and volumes are preserved)
docker compose stop

# Stop and remove containers (volumes are preserved)
docker compose down

# Stop, remove containers AND named volumes (full clean slate)
docker compose down -v

# Restart a single service
docker compose restart catalog_api
docker compose restart identity_api
```

---

## Logs

```powershell
# Stream logs for all services
docker compose logs -f

# Stream logs for a specific service
docker compose logs -f catalog_api
docker compose logs -f identity_api
docker compose logs -f catalog_db
docker compose logs -f catalog_esb
docker compose logs -f catalog_cache

# Show last 50 lines then stream
docker compose logs --tail=50 -f catalog_api
```

---

## Health & Status

```powershell
# Show container status (State, Health, Ports)
docker compose ps

# Inspect health details of a container
docker inspect --format "{{json .State.Health}}" catalog_db
docker inspect --format "{{json .State.Health}}" catalog_esb
docker inspect --format "{{json .State.Health}}" catalog_cache
```

---

## Shell / Exec

```powershell
# Open an interactive shell inside a running container
docker compose exec catalog_api sh
docker compose exec catalog_db bash
docker compose exec catalog_cache sh

# Run a one-off SQL query against catalog_db
docker compose exec catalog_db /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U sa -P "P@ssw0rd" -Q "SELECT name FROM sys.databases" -No

# Ping Redis
docker compose exec catalog_cache redis-cli ping

# Check RabbitMQ node status
docker compose exec catalog_esb rabbitmq-diagnostics status
```

---

## Scale

```powershell
# Scale catalog_api to 3 replicas (remove 'container_name' first if set)
docker compose up -d --scale catalog_api=3
```

---

## Cleanup

```powershell
# Remove stopped containers
docker compose rm -f

# Remove all unused images, containers, networks, build cache
docker system prune -f

# Remove unused images including tagged ones
docker system prune -a -f

# Remove named volumes (WARNING: deletes all database/cache data)
docker volume rm catalog_api_mssql_data
docker volume rm catalog_api_rabbitmq_data
docker volume rm catalog_api_redis_data
```

---

## Environment Files

| File | Used by |
|---|---|
| `containers/db/db.env` | `catalog_db` – SQL Server SA password & EULA |
| `containers/api/api.env` | `catalog_api` – ASP.NET Core URLs & environment |
| `containers/identity/identity.env` | `identity_api` – ASP.NET Core URLs & environment |

---

## Service URLs (host machine)

| Service | URL |
|---|---|
| Catalog API Swagger | http://localhost:5000/swagger |
| Identity API Swagger | http://localhost:5105/swagger |
| RabbitMQ Management UI | http://localhost:15672 (guest / guest) |
| SQL Server | `Server=localhost,1433` |
| Redis | `localhost:6379` |

---

## Common Workflows

### Full rebuild from scratch
```powershell
docker compose down -v
docker compose up --build -d
docker compose logs -f catalog_api
```

### Rebuild only the Catalog API after a code change
```powershell
docker compose up --build -d catalog_api
docker compose logs -f catalog_api
```

### Rebuild only the Identity API after a code change
```powershell
docker compose up --build -d identity_api
docker compose logs -f identity_api
```

### Start infrastructure only (DB + Cache + ESB) for local API debugging
```powershell
docker compose up -d catalog_db catalog_cache catalog_esb
```

### Generate a JWT token via Identity API
```powershell
# Replace values as needed
Invoke-RestMethod -Method Post -Uri http://localhost:5105/token `
  -ContentType "application/json" `
  -Body '{"email":"admin@test.com","userId":"1","customClaims":{"admin":"true"}}'
```
