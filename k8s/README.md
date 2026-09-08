# Kubernetes manifests (self-contained demo)

Plain YAML, `kubectl apply`-able to an empty local cluster — no Helm/Kustomize. Everything the app
needs (SQL Server, Redis, RabbitMQ) runs in-cluster so this works standalone on `kind`/`minikube`.

**This is a demo/local topology, not a production one.** See "Production notes" below for what to
change before running this for real.

These manifests have been deployed and verified end-to-end on a real local cluster (minikube):
register/login, item create/update, outbox → RabbitMQ → `catalog-worker` event handling, and Redis
cache population/invalidation all confirmed working across separate pods.

## Prerequisites

- A local cluster: `kind create cluster` or `minikube start`.
- **Metrics Server**, required for the CPU-based HorizontalPodAutoscalers to work:
  `minikube addons enable metrics-server`, or for `kind`, install the
  [metrics-server manifests](https://github.com/kubernetes-sigs/metrics-server) (with
  `--kubelet-insecure-tls` since kind's kubelets don't have valid serving certs).
- An ingress controller if you want `ingress.yaml` to do anything, e.g.
  `minikube addons enable ingress`, or install `ingress-nginx` on `kind`.

## Build and load the images

The cluster needs the same images built from `containers/api/Dockerfile` and
`containers/worker/Dockerfile`, tagged to match `catalog-api.yaml`/`catalog-worker.yaml`:

```powershell
docker build -t catalog-api:local -f containers/api/Dockerfile .
docker build -t catalog-worker:local -f containers/worker/Dockerfile .

# kind:
kind load docker-image catalog-api:local
kind load docker-image catalog-worker:local

# minikube:
minikube image load catalog-api:local
minikube image load catalog-worker:local
```

For a real (non-local) cluster, push these to a registry instead and update the `image:` fields in
`catalog-api.yaml`/`catalog-worker.yaml` to the registry path.

## Apply

```powershell
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml -f k8s/secret.yaml
kubectl apply -f k8s/sqlserver.yaml -f k8s/redis.yaml -f k8s/rabbitmq.yaml
kubectl wait --for=condition=ready pod -l app=sqlserver -n catalog --timeout=300s
kubectl apply -f k8s/catalog-api.yaml -f k8s/catalog-worker.yaml -f k8s/ingress.yaml

kubectl get pods -n catalog -w
```

`catalog-api`/`catalog-worker` run their own EF Core migrations at startup (same
`Database:AutoMigrate`-gated logic as local/Docker Compose runs), so no separate migration step is
needed once SQL Server is ready.

## Verify

```powershell
kubectl get pods -n catalog
kubectl get hpa -n catalog
```

## Accessing the API from your machine

On a real cloud cluster, a `LoadBalancer` Service gets you a real, always-on external IP for free.
On minikube with the `docker` driver (Windows/Mac), the cluster's network is sealed inside one
Docker container — there's no way around needing *some* bridging process to reach it from your
host. Pick one:

**`kubectl port-forward` (recommended for local dev)** — start it once in a terminal and leave it
running; call the API from anywhere (browser, Postman, curl, your own app) as long as that
terminal stays open:

```powershell
kubectl port-forward -n catalog svc/catalog-api 8080:80

# from another terminal / Postman / browser, while the above keeps running:
curl http://localhost:8080/api/items
curl -X POST http://localhost:8080/api/auth/register -H "Content-Type: application/json" -d '{"email":"k8s@test.com","password":"P@ssw0rd123!"}'
```

If it ever stops working, the tunnel process died — just re-run the `port-forward` command.

**`minikube service` (equivalent alternative)** — minikube manages the tunnel for you instead:

```powershell
minikube service catalog-api -n catalog --url
```

**Ingress (`catalog.local`, closer to a real deployment)** — `ingress.yaml` is already applied,
but needs an ingress controller and a hosts-file entry to actually work:

```powershell
minikube addons enable ingress
# then add to your hosts file (C:\Windows\System32\drivers\etc\hosts, needs admin):
#   <minikube ip>  catalog.local        (get the IP via `minikube ip`)
curl http://catalog.local/api/items
```

There's also always the in-cluster DNS name (`http://catalog-api.catalog.svc.cluster.local`) —
that one only works for *other pods* inside the cluster (e.g. if you added a service that needed
to call `catalog-api`), not from your machine.

## Production notes (what this demo intentionally simplifies)

- **SQL Server/RabbitMQ run as single-replica Deployments with a PVC, not `StatefulSet`s or
  managed services.** Real production should use a managed database (Azure SQL, RDS, etc.) and a
  managed or properly-clustered message broker — losing the single SQL Server or RabbitMQ pod here
  takes down the whole stack.
- **Secrets are plaintext in a committed YAML file.** Use a real secret manager (Sealed Secrets,
  External Secrets Operator, Vault, cloud KMS) instead.
- **HPA scales on CPU only.** For `catalog-worker` specifically, scaling on RabbitMQ queue depth
  (e.g. via [KEDA](https://keda.sh)'s RabbitMQ scaler) is a better signal than CPU — a backlog of
  unprocessed messages doesn't necessarily show up as high CPU. Documented here as the next step,
  not implemented, to avoid pulling in a new cluster-level dependency for this pass.
- **No TLS.** `ingress.yaml` serves plain HTTP; add `cert-manager` + a real `tls:` block before
  exposing this outside a local cluster.
- **First-boot migration race.** On a completely fresh database, `catalog-api`'s 2 replicas and
  `catalog-worker`'s 2 replicas all run `Database.Migrate()` at startup (gated by
  `Database:AutoMigrate`), and on the very first deploy they can race to `CREATE DATABASE`
  simultaneously — one pod crashes, Kubernetes restarts it, and it succeeds once another replica
  has already created the database. This was observed during verification and is self-healing
  (one restart, no data loss), but the standard real-world fix is to run migrations as a one-shot
  Kubernetes `Job` (or `initContainer`) before any app replicas start, rather than having every
  replica race to migrate — not implemented here to avoid adding a migration-only entrypoint to the
  app for this pass.