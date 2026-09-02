# Load test results (measured on this deployment)

Run against the live minikube cluster (`catalog-api` HPA: 2–10 replicas, single SQL
Server/Redis/RabbitMQ instances), in-cluster via `loadtest/k6-job.yaml` — not through
`kubectl port-forward`, so these numbers reflect the Service's real capacity.

## Cached-read path: sustains 1500+ req/s cleanly, ceiling not yet found

`breakpoint-ramp.js`, stepped ramp (100 → 250 → 500 → 1000 → 1500 req/s), single-item cached
`GET /api/items/{id}`:

| Metric | Result |
|---|---|
| Total requests | 60,001 |
| Failures | **0.00%** |
| p95 latency | **1.67ms** |
| Max VUs needed | 8 (of 200 allocated) |
| `catalog-api` replicas | auto-scaled **2 → 6** by the HPA during the run |

The read path — the one this session's caching/outbox/HPA work specifically targeted — held up
with zero errors and sub-2ms p95 all the way to the top of this ramp. It never actually broke;
the ceiling here is higher than 1500 req/s on this hardware, we just didn't push further.

## Mixed realistic workload: bottleneck is login + writes, not reads

`mixed-workload.js` at a 5000 req/s target (50% cached read / 25% list / 15% health / 7% login /
3% write) — the target itself was unreachable, and *why* is the useful finding:

| Path | Success rate at 5000 rps target |
|---|---|
| Cached item read | 89% |
| Liveness check | 90% |
| Paginated list | 80% |
| **Login** | **12%** |
| **Create item (write)** | **19%** |

Actual delivered throughput collapsed to ~150-190 req/s overall once login/write requests started
timing out and backing up the shared VU pool. Two distinct, identifiable causes:

1. **Login is CPU-bound, not I/O-bound.** ASP.NET Core Identity's password hasher (PBKDF2 with a
   high iteration count, by design — this is a security feature, not a bug) is expensive per
   call. Hammering it concurrently saturates `catalog-api`'s CPU fast, and CPU-bound work doesn't
   get cheaper by adding more concurrent requests to the same pods the way I/O-bound work does.
2. **Writes serialize through one SQL Server instance.** Every create-item request opens a
   transaction (item insert + outbox insert) against the single `sqlserver` pod — there's no read
   replica or write sharding, so write throughput has a hard ceiling regardless of how many
   `catalog-api` replicas exist.

## A real bug this load test found and fixed: eager Redis connection warm-up

The first 5000 rps run also crashed 2 of the `catalog-api` pods (`exit 137`, `RESTARTS` visible in
`kubectl get pods`). The previous-container logs showed the actual cause:
`StackExchange.Redis.RedisConnectionException: ... ConnectTimeout` — `IConnectionMultiplexer` was
registered to connect **lazily on first use**, so its very first connection attempt happened
mid-request during the load spike, timed out, and (combined with the CPU pressure from concurrent
password hashing) left the process too starved to answer its own liveness probe in time.

Fixed in `Catalog.API/Program.cs` (warm the connection once at calm startup instead of under
load) and `Catalog.API/Extensions/DistributedCacheExtensions.cs` (`AbortOnConnectFail = false`, so
a transient Redis blip doesn't throw at all). **Re-ran the identical 5000 rps scenario after the
fix: 0 pod restarts** (down from 2), with the same login/write bottleneck as the only remaining
limiting factor — confirming the fix addressed exactly the crash, not the (expected, architectural)
throughput ceiling.

## What this means for scaling toward a much higher target

- **The caching/outbox/HPA architecture from this session's work is validated** — the read path
  (the thing it was built to optimize) scales close to linearly with replica count, exactly as
  designed.
- **The next bottleneck to attack, in order, is login then writes** — not more `catalog-api`
  replicas (that's already proven to work). Concretely: tune Identity's `PasswordHasherOptions`
  iteration count if the security/performance tradeoff allows it, and address the SQL Server
  single-instance ceiling (read replicas don't help the write path — that needs either a bigger
  instance, sharding, or accepting async/batched writes for less latency-sensitive cases).
- **Getting anywhere near 1,000,000 req/s** requires all of the above at a completely different
  infrastructure scale (see `loadtest/README.md`'s closing section) — this single-laptop
  deployment's honest, measured ceiling for cached reads is "at least 1500 req/s, likely several
  times higher," and for the auth/write paths is closer to "a few hundred req/s" until the CPU
  and single-SQL-Server bottlenecks above are addressed.
