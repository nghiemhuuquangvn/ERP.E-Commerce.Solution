---
name: verify
description: Build/launch/drive recipe for this repo (ERP.E-Commerce.Solution) — per-service .NET microservices + frontend apps, all wired through docker-compose.yml at repo root.
---

# Verifying changes in this repo

The whole stack (14+ backend services, Gateway, Admin/Storefront, infra) is normally
already running via `docker-compose.yml` at the repo root — check first with
`docker ps` before starting anything. Containers are named `erp-<service>` (e.g.
`erp-product-service`, `erp-gateway`, `erp-admin`).

## Rebuilding one service after a code change

```bash
cd <repo-root>
docker compose build <compose-service-name>   # e.g. productservice, identityservice, gateway, admin, loggingservice
docker compose up -d <compose-service-name>   # recreates the container with the new image
docker logs erp-<service-name> --tail 40      # check clean startup, no exceptions
```
Compose service names (left column of `docker-compose.yml`) don't always match the
container name 1:1 — e.g. compose service `masterdataservice` → container
`erp-masterdata-service`, `productservice` → `erp-product-service`. Grep
`docker-compose.yml` for the block if unsure.

Each service's own `.sln` also builds standalone for a fast compile-only check
(no container rebuild needed just to catch a C# error):
```bash
cd src/services/<Service>/<maybe src/>  # find the .sln
dotnet build <Service>.sln
```
`src/Shared/*.csproj` has no top-level solution; build `src/Shared/ERP.Shared.csproj`
directly to check shared-library changes compile.

## Gotcha: testing a backend service's Kafka/logging pipeline from the HOST (outside Docker)

`docker-compose.yml`'s `kafka` service sets
`KAFKA_ADVERTISED_LISTENERS: "PLAINTEXT://kafka:9092"` — only the Docker-network
hostname `kafka` is advertised, no host-reachable listener. A `dotnet run`'d service
on the host can open the initial bootstrap connection to `localhost:9092` (port is
mapped), but every actual produce/consume connection gets redirected to `kafka:9092`,
which fails to resolve from the host (`No such host is known`). **Fix: build and run
the service in Docker** (`docker compose build/up`) rather than `dotnet run` on the
host, whenever the change touches Kafka producers/consumers. Elasticsearch doesn't
have this problem — `http://localhost:9200` works fine from the host too, since ES
has no analogous "advertised" redirect.

## Frontend (Admin / Storefront)

`docker compose build admin` runs a real `npm ci && tsc -b && vite build` inside the
container from a clean `node_modules` — this is a better TypeScript check than
running `npx tsc -b` against the host's `node_modules`, which has been observed
stale/incomplete (missing type declarations for `react-i18next`, `jwt-decode`, etc.
across nearly every existing file, unrelated to any particular change). If `tsc -b`
against the host fails with `Cannot find module` errors on files you didn't touch,
that's the host install, not your change — verify with the Docker build instead.

After rebuilding, confirm new UI strings/permission keys actually made it into the
bundle without needing a browser:
```bash
docker exec erp-admin sh -c "grep -o '<new-permission-or-string>' /usr/share/nginx/html/assets/*.js"
```
No browser-automation tool is available in this environment — this bundle-content
grep is a substitute for a screenshot, not equivalent to it. Say so explicitly in
any verification report; don't claim a visual PASS you didn't observe.

## LoggingService (centralized log management) — quick end-to-end check

```bash
curl -s "http://localhost:5016/api/v1/logs?serviceName=<Name>"                 # direct, bypasses Gateway
curl -s -X POST http://localhost:5016/api/v1/logs/client -d '[{"serviceName":"X","level":"Error","kind":"Exception","message":"..."}]'
curl -s "http://localhost:5000/logs/api/v1/logs?serviceName=<Name>"           # through Gateway — needs Bearer token, else 401 (expected)
curl -s -X POST http://localhost:5000/logs/api/v1/logs/client -d '...'        # through Gateway — unauthenticated by design
```
Any service that calls `AddSerilogLogging("<Name>")` ships ALL its own Serilog
output (including framework/EF/Hosting.Lifetime noise) through Kafka into
Elasticsearch automatically — hitting a service's own `/health` endpoint after a
fresh restart and then querying `serviceName=<Name>` is enough to prove the
Serilog → Kafka → consumer → Elasticsearch pipeline is wired correctly for that
service, without needing to manufacture a real unhandled exception.
