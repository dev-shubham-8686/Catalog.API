# Load testing

Two k6 scripts:
- **`mixed-workload.js`** — realistic traffic mix: 50% cached single-item reads, 25% paginated
  list reads, 15% liveness checks, 7% logins, 3% writes (item create, which exercises the outbox
  + RabbitMQ publish path too). Runs at a single target rate for a sustained period.
- **`breakpoint-ramp.js`** — cached single-item reads only, stepping up through fixed rate
  plateaus (100 → 250 → 500 → 1000 → 1500 req/s) to isolate exactly how far the read path alone
  scales, without auth/write CPU cost muddying the result.

**Already run once against this deployment — see [RESULTS.md](RESULTS.md) for the actual measured
numbers**: the cached-read path (this session's caching/outbox/HPA work) sustained 1500 req/s with
0% failures and 1.67ms p95 latency, while login/writes became the real bottleneck at high
concurrency (CPU-bound password hashing and a single SQL Server instance).

## About the "1 million requests/sec" target

That's not a reachable number against this setup, and it's worth being upfront about why rather
than quietly running something smaller: 1M req/sec is roughly the scale of a handful of the
busiest endpoints at the largest tech companies, sustained across hundreds of servers, multiple
database read replicas, and CDN/edge caching in front of the origin entirely. This project runs
on one laptop, with **one** SQL Server instance, **one** Redis instance, and **one** RabbitMQ
instance behind 2 `catalog-api` replicas — the realistic ceiling here is measured in low
thousands of requests/sec at best for the cached hot-path reads, and far less for anything that
touches SQL Server or RabbitMQ.

This script still targets a genuinely useful goal: **find the actual breaking point of this
specific deployment**, report it honestly, and show what would need to change (more replicas,
connection pool tuning, read replicas, caching more aggressively, horizontal scaling of every
tier) to move that ceiling — which is the real path toward a number like 1M rps in production,
achieved with proper distributed infrastructure and distributed load generation (k6 Cloud, or
many k6 instances / a `k6-operator` fleet), not a single script on a single machine.

## Run against the k8s cluster (recommended — see why below)

`kubectl port-forward` is a single-stream proxy through the API server; it will bottleneck load
tests far below what the Service can really do. Running k6 **inside** the cluster as a Job,
talking to `catalog-api` directly via its ClusterIP Service, gives real numbers.

```powershell
# Load both scripts into the cluster as a ConfigMap (re-run after editing either script)
kubectl create configmap k6-script -n catalog --from-file=loadtest/mixed-workload.js --from-file=loadtest/breakpoint-ramp.js --dry-run=client -o yaml | kubectl apply -f -

# Run the test (k6-job.yaml runs mixed-workload.js by default — edit its `args` to point at
# breakpoint-ramp.js instead if you want the stepped read-only ramp)
kubectl apply -f loadtest/k6-job.yaml
kubectl logs -n catalog job/k6-load-test -f

# Re-run after a script change: delete the old Job first (Jobs are immutable)
kubectl delete job k6-load-test -n catalog --ignore-not-found
```

Tune the run via the Job's env vars (`TARGET_RPS`, `MAX_VUS`, `RAMP_DURATION`,
`SUSTAIN_DURATION` for `mixed-workload.js`; `STEP_DURATION`, `MAX_VUS` for `breakpoint-ramp.js`)
— edit `loadtest/k6-job.yaml` directly, or override with
`kubectl set env job/k6-load-test -n catalog TARGET_RPS=10000` before the next run. Watch
`kubectl get hpa -n catalog -w` in another terminal during the run to see the HPAs react.

## Run locally against port-forward (quick sanity check only)

Docker Desktop on Windows/Mac doesn't support `--network host` the way Linux does — use
`host.docker.internal` to reach a port-forward running on the host instead:

```powershell
kubectl port-forward -n catalog svc/catalog-api 8080:80

docker run --rm -e BASE_URL=http://host.docker.internal:8080 -v "${PWD}/loadtest:/scripts" grafana/k6:latest run /scripts/mixed-workload.js
```

Expect noticeably lower numbers than the in-cluster run — this measures the port-forward tunnel
as much as the API.

## Reading the results

k6's end-of-run summary reports `http_reqs` (total) and the rate in `iterations/s` /
`http_req_rate` — that rate *is* your measured requests/sec ceiling for this deployment. Also
check:
- `http_req_failed` — rising failure rate as load increases means you've found the actual
  breaking point (connection pool exhaustion, SQL Server CPU saturation, RabbitMQ backpressure).
- `get_item_cached_duration` vs `create_item_duration` (custom Trends in the script) — the gap
  between cached-read and DB-write latency shows exactly how much caching is buying you.
- `kubectl top pods -n catalog` during the run — see which tier (API, SQL Server, Redis,
  RabbitMQ) saturates first; that's your next scaling target.

## What it would actually take to approach 1M req/sec

Roughly, in order of what breaks first as you scale this specific architecture up:
1. **SQL Server becomes the bottleneck almost immediately** — a single instance has a hard
   ceiling. Real scale needs read replicas for the cached-read path (which mostly shouldn't hit
   SQL Server at all if caching is working) and eventually sharding or a managed elastic-scale
   database for the write path.
2. **RabbitMQ single instance** — a real deployment needs a clustered broker (or a managed
   service) once publish/consume throughput outgrows one node.
3. **`catalog-api`/`catalog-worker` replica count** — the HPAs here cap at 10 replicas on CPU;
   production scale needs a much higher ceiling and ideally queue-depth-based scaling for the
   worker (see the KEDA note in `k8s/README.md`).
4. **A single load generator can't produce 1M rps either** — k6 itself would need to run as a
   distributed fleet (k6 Cloud, or a `k6-operator` deployment spreading load across many pods)
   to generate that much traffic in the first place, independent of whether the backend could
   absorb it.
