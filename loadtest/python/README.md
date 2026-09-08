# Python load test + real-time k8s dashboard

Two standalone scripts, both using [`rich`](https://github.com/Textualize/rich) for a live
terminal UI. Complementary to the k6 scripts one level up (`../mixed-workload.js`,
`../breakpoint-ramp.js`) — same idea, different tool, with a real-time view instead of an
end-of-run summary.

## Setup

```powershell
pip install -r requirements.txt
```

## `load_test.py` — real-time load test dashboard

Same mixed workload as `../mixed-workload.js` (50% cached item reads, 25% list reads, 15% health
checks, 7% logins, 3% writes), but with a live-updating dashboard instead of a final report:
requests/sec, success rate, latency (avg/p50/p95/p99), and a per-endpoint breakdown — all
refreshing ~4x/second while the test runs.

```powershell
python load_test.py --base-url http://localhost:5000 --concurrency 50 --duration 60
```

- `--base-url` — target API (docker-compose default `:5000`, or a `kubectl port-forward` address)
- `--concurrency` — number of concurrent workers continuously firing requests
- `--duration` — test length in seconds

It seeds one item and one user at startup (visible as "Seeding test data..."), then hammers the
mix above until time runs out. Ctrl+C stops early and still prints the summary so far.

### Running it *inside* the cluster (real numbers, not port-forward-throttled ones)

`kubectl port-forward` is a single-stream proxy — it caps throughput well below what the Service
can actually handle (measured: ~55 RPS through a port-forward vs. ~115 RPS running in-cluster
against the identical target). `k8s-job.yaml` runs `load_test.py` as a one-shot Job inside the
cluster, hitting `http://catalog-api` directly — same idea as `../k6-job.yaml` for the k6 script.

```powershell
# 1. Load the script into the cluster as a ConfigMap (MUST run this before applying the Job —
#    see the warning comment in k8s-job.yaml about ordering)
kubectl create configmap python-loadtest-script -n catalog `
  --from-file=load_test.py --from-file=requirements.txt `
  --dry-run=client -o yaml | kubectl apply -f -

# 2. Run it
kubectl apply -f k8s-job.yaml
kubectl logs -n catalog job/python-load-test -f

# 3. Re-run after a script change: delete the old Job first (Jobs are immutable), then repeat
#    step 1 (to refresh the ConfigMap) before step 2
kubectl delete job python-load-test -n catalog --ignore-not-found
```

Watch `kubectl get hpa -n catalog -w` or `k8s_dashboard.py` in another terminal while it runs —
this is what actually triggers the HPA to scale `catalog-api` up under real load.

## `k8s_dashboard.py` — real-time cluster view

Pods (status/ready/restarts/CPU/memory/age), Deployments + HorizontalPodAutoscaler status
(current vs. target CPU%, replica counts), and current ConfigMap values + Secret **key names**
(never values) — refreshed continuously by shelling out to `kubectl`.

```powershell
python k8s_dashboard.py --namespace catalog --interval 2
```

- Requires a reachable cluster: `kubectl get nodes` should work before running this.
- CPU/memory columns need `metrics-server` running in the cluster (`minikube addons enable
  metrics-server`, already part of `k8s/README.md`'s setup instructions) — without it they show `-`.
- `--duration N` stops it automatically after N seconds instead of running until Ctrl+C (mainly
  useful for scripting/demos).

**For interactive exploration (browsing logs, exec into a pod, sorting by CPU, etc.) use
[`k9s`](https://k9scli.io/) instead** — it's the full-featured standard tool for this:

```powershell
k9s -n catalog
```

`k8s_dashboard.py` is the lighter, no-extra-install, "just show me the numbers" alternative —
useful when you want something scriptable or to leave running unattended in a terminal.

## Watching both together

Run `k8s_dashboard.py` (or `k9s -n catalog`) in one terminal and `load_test.py` pointed at a
`kubectl port-forward`'d address in another — you'll see CPU climb on the dashboard as the load
test ramps up, and (given enough sustained load) the HPA's replica count increase live.
