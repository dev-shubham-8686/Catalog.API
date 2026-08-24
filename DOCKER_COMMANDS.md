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
| Container Registry | `catalog_registry` | `registry:2` | `5001` (maps to container's `5000`) |

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
| Container Registry | `localhost:5001` |

---

## Container Registry

A self-hosted Docker registry (`registry:2`) for pushing/pulling this project's images locally,
without depending on Docker Hub or a cloud registry. It requires basic-auth login (no anonymous
push/pull) and persists images in the `registry_data` volume.

**Local-only design note:** this setup serves plain HTTP, not HTTPS. Docker refuses to talk to a
non-TLS registry unless the host is explicitly marked "insecure", which is fine for `localhost`
but is **not** how you'd run a shared/production registry — there, TLS is mandatory and normally
terminated by a reverse proxy (nginx/Traefik) with a real certificate (internal CA or Let's
Encrypt) in front of the registry container, which never talks plain HTTP itself. Auth (htpasswd)
is still enforced here so this isn't a wide-open registry, just an unencrypted one on localhost.

### One-time setup
```powershell
# Generates containers/registry/auth/htpasswd (gitignored — regenerate per machine/rotate as needed)
./scripts/setup-registry.ps1

# Tell Docker Desktop to trust this registry over plain HTTP:
# Settings → Docker Engine → add "localhost:5001" to "insecure-registries", then Apply & Restart:
#   { "insecure-registries": ["localhost:5001"] }
```

### Start the registry
```powershell
docker compose up -d registry
```

### Login, tag, push, pull
```powershell
docker login localhost:5001 -u registry-user -p RegistryP@ss1

# Tag an image already built by `docker compose build catalog_api`
docker tag store-catalog_api:latest localhost:5001/catalog-api:1.0.0

docker push localhost:5001/catalog-api:1.0.0

# From any machine that can reach this host and has logged in:
docker pull localhost:5001/catalog-api:1.0.0
```

### Bumping the version automatically
There's no auto-increment in Docker or the registry — you (or CI) decide the next tag. To avoid
tracking it by hand, `bump-and-push.ps1` looks up the highest existing semver tag for a repo,
bumps it, and pushes:
```powershell
# Bumps the patch version (1.0.1 -> 1.0.2) using store-catalog_api:latest
./scripts/bump-and-push.ps1

# Bumps the minor version instead (1.0.2 -> 1.1.0, patch resets to 0)
./scripts/bump-and-push.ps1 -Bump Minor

# -Bump Major resets minor and patch to 0 too
./scripts/bump-and-push.ps1 -Repo identity-api -Image store-identity_api:latest -Bump Major
```

### Inspect what's stored
`docker images` only shows your local cache — it has no idea what's actually sitting in the
registry. To see that, you query the registry's own HTTP API:
```powershell
# List repositories
curl -u registry-user:RegistryP@ss1 http://localhost:5001/v2/_catalog

# List tags for a repository
curl -u registry-user:RegistryP@ss1 http://localhost:5001/v2/catalog-api/tags/list

# Or list every repository:tag in one shot
./scripts/list-registry-images.ps1
```

### Garbage collection
Deleting a tag via the API only removes the manifest reference — the underlying layers stay on
disk until you run garbage collection:
```powershell
docker compose exec registry bin/registry garbage-collect /etc/docker/registry/config.yml
```

### Real-world best practices (applies beyond this local setup too)
- **Never push/deploy `:latest`** — tag with a git SHA or semver (`catalog-api:1.4.2`), so a
  running container's image is always traceable back to a commit.
- **Scan images before pushing** — Trivy or Grype in CI, fail the build on high/critical CVEs.
- **Least-privilege credentials** — separate push (CI-only) vs pull (runtime-only) accounts;
  production nodes should never hold push rights.
- **Retention/GC policy** — untagged manifests accumulate; schedule `garbage-collect` (or use a
  managed registry's built-in retention policy) so storage doesn't grow unbounded.
- **TLS always, off localhost** — see the design note above; `insecure-registries` is a local
  dev/learning shortcut, not something to carry into a shared environment.

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

### Register a user and get a JWT via Catalog API
Catalog API now hosts real ASP.NET Core Identity authentication (via the `Identity.Authentication`
class library) — register/login runs against the `AspNetUsers`/`AspNetRoles` tables in `catalog_db`,
not the `identity_api` stub. `identity_api` still exists as a standalone throwaway token-minting
stub and is unrelated to these endpoints.
```powershell
# Register
Invoke-RestMethod -Method Post -Uri http://localhost:5000/api/auth/register `
  -ContentType "application/json" `
  -Body '{"email":"admin@test.com","password":"P@ssw0rd123!"}'

# Login -> returns { accessToken, expiresAtUtc }
Invoke-RestMethod -Method Post -Uri http://localhost:5000/api/auth/login `
  -ContentType "application/json" `
  -Body '{"email":"admin@test.com","password":"P@ssw0rd123!"}'
```
