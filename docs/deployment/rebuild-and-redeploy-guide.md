# Rebuild & Redeploy Guide (Day-2 Operations)

This guide covers the recurring workflow: **you changed code (or added a new
service) and need to get it running again in Docker Compose and/or
Kubernetes.** For a from-scratch environment setup, see
[deployment-guide.md](deployment-guide.md) instead. To run one service or
frontend app directly without Docker (e.g. for IDE debugging), see
[manual-build-deploy-guide.md](manual-build-deploy-guide.md) instead. For the
common case — **only 1-2 services changed, deploy just those, not all 19** —
see [per-service-deploy-guide.md](per-service-deploy-guide.md) instead, which
has the full compose-service/K8s-Deployment/K8s-Service name mapping table and
a decision table for "what did you change → what actually needs rebuilding."

## Table of Contents

1. [Pick your target](#1-pick-your-target)
2. [Docker Compose — rebuild after code changes](#2-docker-compose--rebuild-after-code-changes)
3. [Kubernetes — rebuild after code changes](#3-kubernetes--rebuild-after-code-changes)
4. [Checklist: adding a brand-new service](#4-checklist-adding-a-brand-new-service)
5. [Pitfalls that will cost you an hour if you don't know them](#5-pitfalls-that-will-cost-you-an-hour-if-you-dont-know-them)
6. [Command cheat-sheet](#6-command-cheat-sheet)

---

## 1. Pick your target

Don't run the full Docker Compose stack and the full Kubernetes stack at the
same time on one dev machine. Both stand up their own Kafka, RabbitMQ,
Elasticsearch, and Redis — running both doubles CPU/memory load and causes
probe timeouts and flapping pods that look like real bugs but are just
resource contention. Pick one:

- Iterating quickly on one or two services, want the fastest inner loop →
  **Docker Compose**.
- Testing multi-replica behavior, rollouts, ingress, or anything
  Kubernetes-specific → **Kubernetes**, and run `docker compose down` first.

---

## 2. Docker Compose — rebuild after code changes

### 2.1 One or two services changed

```powershell
cd d:\Documents\ERP\Sources\ERP.E-Commerce.Solution
docker compose build salesorderservice
docker compose up -d salesorderservice
docker compose logs -f salesorderservice   # confirm it started cleanly
```

`docker compose up -d <service>` only recreates the containers whose image
actually changed — it won't restart unrelated services.

### 2.2 Many services changed (e.g. a shared library like `src/Shared`)

Building all ~19 images in one parallel `docker compose build` call can
exhaust Docker BuildKit and fail with `rpc error: ... EOF`. Build in batches
of 4-5 instead:

```powershell
docker compose build identityservice salesorderservice purchaseorderservice warehouseservice loggingservice
docker compose build accountingservice crmservice hrmservice masterdataservice productservice
docker compose build paymentservice shippingservice posservice promotionservice notificationservice
docker compose build reportingservice gateway admin storefront
```

Then bring everything up:

```powershell
docker compose up -d
```

### 2.3 Verify

```powershell
docker compose ps                     # everything should be "Up" with no restart loops
$ports = 5001,5002,5003,5004,5005,5006,5007,5008,5009,5010,5011,5012,5013,5014,5015,5016
foreach ($p in $ports) {
  try { (Invoke-WebRequest "http://localhost:$p/health" -TimeoutSec 3).StatusCode }
  catch { "port $p FAILED" }
}
```

### 2.4 Roll back

```powershell
docker compose down            # keep volumes/data
git checkout <previous-commit> -- <path>   # or just fix the code
docker compose build <service> && docker compose up -d <service>
```

---

## 3. Kubernetes — rebuild after code changes

Docker Desktop's Kubernetes shares the same local image cache as Docker
Compose, so you build the image the same way — the difference is Kubernetes
won't notice a rebuilt `:latest` image on its own.

### 3.1 Rebuild the image

```powershell
docker build --build-arg SERVICE_PATH=src/services/SaleOrderService/src/ERP.SO.Api `
             --build-arg PROJECT_FILE=ERP.SO.Api.csproj `
             --build-arg ASSEMBLY_NAME=ERP.SO.Api `
             -t erp/salesorder-service:latest -f Dockerfile .
```

(Or reuse the batched `docker compose build <service>` from section 2 — it
produces the exact same `erp/<service>:latest` image tag that the K8s
manifests reference.)

### 3.2 Force the rollout to pick up the new image

Every deployment manifest sets `imagePullPolicy: IfNotPresent` — since the
tag (`:latest`) didn't change, Kubernetes will happily keep running the old
image forever unless you tell it to restart:

```powershell
kubectl rollout restart deployment/salesorder-service -n erp
kubectl rollout status deployment/salesorder-service -n erp
```

### 3.3 Verify

```powershell
kubectl get pods -n erp -l app=salesorder-service
kubectl logs -n erp -l app=salesorder-service --tail=50
```

### 3.4 Roll back

```powershell
kubectl rollout undo deployment/salesorder-service -n erp
kubectl rollout status deployment/salesorder-service -n erp
```

---

## 4. Checklist: adding a brand-new service

When a new service (like `LoggingService`) is added to the codebase, it needs
changes in *five* separate places before it's actually deployable — missing
any one of them is easy to miss because nothing fails loudly until you try to
run it:

1. **`docker-compose.yml`** — add the service block (`build.args`, `ports`,
   `environment`, `depends_on`, `networks`).
2. **`Dockerfile` build-arg entry** in
   [deployment-guide.md §4.2](deployment-guide.md#42-build-and-tag-images) —
   so the documented manual K8s image-build loop covers it too.
3. **`deployment/k8s/deployments/<name>.yaml`** — a `Deployment` + `Service`,
   following the pattern of an existing simple service (e.g.
   `notification.yaml`). Match the health check path, port, and every env var
   the service's `appsettings.Docker.json` / `DependencyInjection.cs` actually
   reads (see pitfall #3 below — don't just copy the appsettings.json section
   name).
4. **`deployment/k8s/secrets.yaml` + `secrets.yaml.example`** — only if the
   new service needs its own DB connection string or credential.
5. **`ocelot.json`** (Gateway) — only if the service should be reachable
   through the API Gateway at `/servicename/...`. Internal-only services
   (consumer-only, like a log sink) don't need this.

Ingress (`deployment/k8s/ingress.yaml`) almost never needs a new entry —
individual backend services aren't given their own hostname; they're reached
through the Gateway or the cluster-internal `<service-name>.erp.svc` DNS name.

---

## 5. Pitfalls that will cost you an hour if you don't know them

These are all things that surfaced during real redeploys of this repo — each
looked like a mysterious infra failure before the real cause was found.

**YAML needs a space after `:`.** `key:"value"` (no space) is a parse error;
`key: "value"` is fine. A single missing space in `secrets.yaml` blocked the
entire `kubectl apply` chain.

**A StatefulSet's headless Service needs `publishNotReadyAddresses: true` if
the pod resolves its own Service DNS name at startup.** Kafka's advertised
listener is `kafka:9092` — but Kubernetes only publishes a headless Service's
DNS record for pods that are already `Ready`. A single-replica Kafka pod can
never become Ready without first resolving `kafka:9092` to itself → deadlock.
Set `publishNotReadyAddresses: true` on the Service so the DNS name resolves
during startup, before the pod is marked ready.

**Exec-based liveness/readiness probes are expensive — prefer `tcpSocket` or
`httpGet` under CPU constraints.** `rabbitmq-diagnostics ping` and
`kafka-broker-api-versions` each spin up a full BEAM/JVM process just to
answer "are you alive?". Under load (e.g. Compose and K8s running
simultaneously) these routinely exceed a 10s timeout even though the broker
itself is healthy and accepting connections on its port — causing the
kubelet to kill and restart an otherwise-fine pod in a loop. A `tcpSocket`
check on the broker's actual port is nearly free and answers the same
question.

**An env var name must match the config *key the C# code actually reads*,
not just the `appsettings.json` section name.** `RabbitMQ__Password` only
overrides `configuration["RabbitMQ:Password"]`. If a service's
`DependencyInjection.cs` reads `configuration["RabbitMQ:Password"]` but its
K8s manifest sets `RabbitMQSettings__Password`, the app silently falls back
to whatever hardcoded default the code has (often `"guest"`) — no error, no
crash at the config layer, just a mysterious `ACCESS_REFUSED` at the first
RabbitMQ connection attempt. Always grep the service's
`DependencyInjection.cs` / `Program.cs` for the literal
`configuration["Section:Key"]` calls before naming a manifest's env vars —
don't assume the appsettings.json section name is what the code binds to.

**Kubernetes Service names must match what `ocelot.json` expects — they are
not automatically the same as the Deployment name.** `ocelot.json`'s
`DownstreamHostAndPorts` entries use the Docker Compose service names
(`identityservice`, `productservice`, `salesorderservice`, ... — no hyphens),
because that's what Docker Compose's internal DNS resolves. Kubernetes
Deployments in this repo are conventionally named with hyphens
(`identity-service`), and it's easy to give the paired `Service` object the
same hyphenated name "for consistency" — but the Service name *is* the DNS
name other pods use to reach it. If the Service is named `identity-service`
while `ocelot.json` asks for `identityservice`, the Gateway fails every
downstream call with `HttpRequestException: Name or service not known
(identityservice:5001)`, which surfaces to users as every API call returning
`502 Bad Gateway` — static pages (the Admin/Storefront SPA shells) still
load fine, so this looks like "the frontend is broken" rather than a DNS
mismatch. Keep the Deployment name hyphenated (matches this repo's existing
convention and every `kubectl` command in this guide) but name the paired
**Service** object without hyphens, matching `ocelot.json` and
`docker-compose.yml` exactly. `deployment/k8s/deployments/*.yaml` already
follows this convention — if you add a new service, copy an existing file's
`Service` block rather than renaming it to match the Deployment.

**Every deployment manifest that calls other infrastructure (Kafka, Redis,
RabbitMQ, Elasticsearch) needs `envFrom: configMapRef: name: erp-config`.**
It's easy to hand-write a new Deployment's `env:` block with just the
service's own port and DB connection string and forget the shared
`envFrom` — the container starts fine and passes its own health check (which
doesn't touch Kafka/Redis), so nothing fails loudly. The symptom instead is
the container's logs slowly filling with `rdkafka` connection-refused
retries against `localhost:9092` (the appsettings.json default) because it
never received `KafkaSettings__BootstrapServers=kafka:9092` from the
configmap. Always include `envFrom: - configMapRef: { name: erp-config }` on
every service Deployment, even ones (like the Gateway) that don't have their
own database.

**A new/changed Ocelot route lives in the Gateway's own image — rebuilding the
downstream service does nothing for it.** `ocelot.json` is baked into
`erp/gateway:latest` at build time like any other source file, not read from a
mounted configmap. Adding a route (e.g. this repo's `/api/v1/product-attributes`) and
redeploying only the downstream service (ProductService) leaves the Gateway serving
its old routing table — the downstream service works fine when hit directly on its
own port, but every call through the Gateway 404s, which looks exactly like a typo in
the route path rather than a stale image. Rebuild and roll the Gateway too:
`docker build --build-arg SERVICE_PATH=src/Gateway/Ocelot.API --build-arg
PROJECT_FILE=Ocelot.API.csproj --build-arg ASSEMBLY_NAME=Ocelot.API -t
erp/gateway:latest -f Dockerfile .` then `kubectl rollout restart
deployment/gateway -n erp`. This bit a real rollout in this repo (2026-08-07): the
first post-deploy smoke test of a new attribute endpoint 404'd through the Gateway
until this was caught.

**A StatefulSet's PersistentVolumeClaim outlives credential changes.**
`RABBITMQ_DEFAULT_USER`/`RABBITMQ_DEFAULT_PASS` (and similarly Kafka's
`CLUSTER_ID`) only take effect the *first* time the pod boots against an
empty data directory. If the PVC already has data from an earlier deploy with
different credentials, changing the env var or the k8s Secret does nothing —
the broker keeps its originally-seeded user/password. In local dev, delete
the PVC (`kubectl delete pvc <name> -n erp`) alongside the StatefulSet to
force a clean re-seed. Never do this against a PVC holding real data.

---

## 6. Command cheat-sheet

```powershell
# ---- Docker Compose ----
docker compose build <service>              # rebuild one image
docker compose up -d <service>               # recreate just that container
docker compose up -d                         # recreate everything with new images
docker compose logs -f <service>             # tail logs
docker compose ps                            # status of all containers
docker compose down                          # stop + remove containers (keeps volumes)

# ---- Kubernetes ----
docker build --build-arg SERVICE_PATH=... --build-arg PROJECT_FILE=... `
             --build-arg ASSEMBLY_NAME=... -t erp/<service>:latest -f Dockerfile .
kubectl apply -f deployment/k8s/deployments/<name>.yaml   # apply manifest changes
kubectl rollout restart deployment/<name> -n erp          # pick up a rebuilt :latest image
kubectl rollout status deployment/<name> -n erp           # watch the rollout
kubectl rollout undo deployment/<name> -n erp              # roll back
kubectl get pods -n erp                                    # overall pod health
kubectl logs -n erp -l app=<name> --tail=50                # tail logs
kubectl describe pod <pod-name> -n erp                      # events (probe failures, scheduling issues)

# ---- Never run both stacks at once ----
docker compose down       # before switching to Kubernetes testing
```
