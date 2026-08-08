# Manual Build & Deploy Guide (Per Service, No Docker Compose)

> This is the standalone/local-debugging complement to
> [deployment-guide.md](deployment-guide.md) (from-scratch Docker Compose/Kubernetes
> setup) and [rebuild-and-redeploy-guide.md](rebuild-and-redeploy-guide.md) (day-2
> rebuild workflow). Use **this** guide when you want to run one service (or one
> frontend app) directly with `dotnet run`/`npm run dev` against shared
> infrastructure — e.g. debugging a single service in an IDE with breakpoints,
> without rebuilding its Docker image every change. For running the whole platform,
> use the other two guides instead.

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Database setup](#2-database-setup)
3. [Service reference table](#3-service-reference-table)
4. [Running one backend service manually](#4-running-one-backend-service-manually)
5. [Building without Docker](#5-building-without-docker)
6. [Frontend apps (Admin / Storefront)](#6-frontend-apps-admin--storefront)
7. [Mobile apps (Customer / Staff)](#7-mobile-apps-customer--staff)

---

## 1. Prerequisites

| Tool | Notes |
|---|---|
| .NET 10 SDK | For `dotnet build`/`dotnet run` on any backend service or the Gateway |
| Node.js 22 | For Admin/Storefront (Vite) and the mobile Expo apps |
| SQL Server, reachable at `localhost,1433` | Either host-installed, or just the `sqlserver` container from Compose (`docker compose up -d sqlserver`) while every *app* process runs natively |
| `sqlcmd` | For applying migration `.sql` files directly (§2) |
| RabbitMQ / Kafka / Redis / Elasticsearch, as needed per service | Easiest to still run these via Compose (`docker compose up -d rabbitmq kafka redis elasticsearch`) even when running app processes manually — see §3 for which service needs which |

A common hybrid setup: run all infrastructure containers via Compose
(`docker compose up -d sqlserver rabbitmq kafka redis elasticsearch`), then run only
the *one service you're actively working on* with `dotnet run` against that shared
infrastructure — everything else can stay off, or keep running in Compose too.

## 2. Database setup

**Do not use `dotnet ef database update`** — despite what some services' own
`README.md` "Getting Started" sections say, this repo does not use EF Core
migrations. Every schema change is a hand-written, idempotent SQL script under
`src/Database/{Service}/*.sql`.

**Do not use `src/Database/RunAllScripts.ps1`** either —
[deployment-guide.md §2](deployment-guide.md#2-database-setup) documents that its
file list has drifted and is missing 16 real migration files that exist on disk.

Instead, mirror exactly what `deployment/docker/run-migrations.sh` does in Compose:

```powershell
cd d:\Documents\ERP\Sources\ERP.E-Commerce.Solution

# 1. Create the databases
sqlcmd -S localhost -U sa -P "<your SA password>" -C -i deployment\sql\init-databases.sql

# 2. Apply every service's migrations, in numeric filename order
Get-ChildItem "src\Database\*" -Directory | ForEach-Object {
  Get-ChildItem $_.FullName -Filter "*.sql" -Depth 0 | Sort-Object Name | ForEach-Object {
    Write-Host "Applying $($_.FullName)"
    sqlcmd -S localhost -U sa -P "<your SA password>" -C -i $_.FullName
  }
}
```

`-Depth 0` matters — it matches `run-migrations.sh`'s own `-maxdepth 1`, skipping
subfolders like `Functions/`/`Procedures/` that some services keep alongside their
numbered migrations. A failed script does not halt the loop (same behavior as the
Compose version) — check console output for errors.

## 3. Service reference table

Ports, project paths, and database names below are taken directly from
`docker-compose.yml` (the authoritative source — `launchSettings.json` is not: only
IdentityService has one, and it defaults to `5095`/`7077`, not the port below).

| Service | Port | Project (`dotnet run --project`) | Database | Connection string key | Needs |
|---|---|---|---|---|---|
| Gateway | 5000 | `src/Gateway/Ocelot.API/Ocelot.API.csproj` | — | — | RabbitMQ, Kafka |
| IdentityService | 5001 | `src/services/IdentityService/src/ERP.Identity.Api/ERP.Identity.API.csproj` | AuthDB | `ConnectionStrings__AuthDB` | Redis, Kafka |
| SaleOrderService | 5002 | `src/services/SaleOrderService/src/ERP.SO.Api/ERP.SO.Api.csproj` | SalesDB | `ConnectionStrings__SalesDb` | RabbitMQ, Redis, Kafka |
| PurchaseOrderService | 5003 | `src/services/PurchaseOrderService/src/ERP.PO.Api/ERP.PO.Api.csproj` | PurchasingDB | `ConnectionStrings__PurchasingDb` | RabbitMQ, Redis, Kafka |
| WareHouseService | 5004 | `src/services/WareHouseService/src/ERP.WH.API/ERP.WH.API.csproj` | WarehouseDB | `ConnectionStrings__WarehouseDB` | RabbitMQ, Redis, Kafka |
| AccountingService | 5005 | `src/services/AccountingService/src/ERP.ACC.API/ERP.ACC.API.csproj` | AccountingDB | `ConnectionStrings__AccountingDB` | RabbitMQ, Redis, Kafka |
| CRMService | 5006 | `src/services/CRMService/src/ERP.CRM.API/ERP.CRM.API.csproj` | CrmDB | `ConnectionStrings__CrmDB` | RabbitMQ, Redis, Kafka |
| HRMService | 5007 | `src/services/HRMService/src/ERP.HRM.API/ERP.HRM.API.csproj` | HrmDB | `ConnectionStrings__HrmDB` | Redis, Kafka |
| ProductService | 5008 | `src/services/ProductService/src/ERP.PRD.API/ERP.PRD.API.csproj` | ProductDB | `ConnectionStrings__ProductDB` | RabbitMQ, Redis, Kafka, Elasticsearch |
| PaymentService | 5009 | `src/services/PaymentService/src/ERP.PAY.API/ERP.PAY.API.csproj` | PaymentDB | `ConnectionStrings__PaymentDb` | RabbitMQ, Redis, Kafka |
| ShippingService | 5010 | `src/services/ShippingService/src/ERP.SHP.API/ERP.SHP.API.csproj` | ShippingDB | `ConnectionStrings__ShippingDb` | RabbitMQ, Redis, Kafka |
| POSService | 5011 | `src/services/POSService/src/ERP.POS.API/ERP.POS.API.csproj` | PosDB | `ConnectionStrings__PosDb` | RabbitMQ, Redis, Kafka |
| PromotionService | 5012 | `src/services/PromotionService/src/ERP.PRM.API/ERP.PRM.API.csproj` | PromotionDB | `ConnectionStrings__PromotionDb` | RabbitMQ, Redis, Kafka |
| NotificationService | 5013 | `src/services/NotificationService/src/ERP.NTF.API/ERP.NTF.API.csproj` | NotificationDB | `ConnectionStrings__NotificationDb` | RabbitMQ, Redis, Kafka |
| ReportingService | 5014 | `src/services/ReportingService/src/ERP.RPT.API/ERP.RPT.API.csproj` | ReportingDB | `ConnectionStrings__ReportingDb` | RabbitMQ, Redis, Kafka |
| SharedMasterDataService | 5015 | `src/services/SharedMasterDataService/src/ERP.MST.API/ERP.MST.API.csproj` | MasterDataDB | `ConnectionStrings__MasterDataDb` | Redis, Kafka |
| LoggingService | 5016 | `src/services/LoggingService/src/ERP.LOG.API/ERP.LOG.API.csproj` | — (Elasticsearch only) | — | Elasticsearch, Kafka |

> `LoggingService`/port 5016 is defined in `docker-compose.yml` and
> `deployment/k8s/deployments/` but is not yet listed in `CLAUDE.md`'s service
> table — a pre-existing docs gap, noted here rather than fixed, since it's outside
> this guide's scope.

## 4. Running one backend service manually

```powershell
$env:ASPNETCORE_URLS = "http://localhost:5001"       # use the port from §3
$env:ConnectionStrings__AuthDB = "Server=localhost,1433;Database=AuthDB;User Id=sa;Password=<your SA password>;TrustServerCertificate=True;Encrypt=False"
$env:Jwt__Key = "<same JWT_SIGNING_KEY value everything else uses>"
$env:KafkaSettings__BootstrapServers = "localhost:9092"

dotnet run --project src/services/IdentityService/src/ERP.Identity.Api/ERP.Identity.API.csproj
```

Adjust the connection-string env var name and value per service (§3's table), and
add `RabbitMQ__HostName=localhost` / `RabbitMQ__Port=5672` / `RabbitMQ__UserName=admin`
/ `RabbitMQ__Password=<...>` and/or `Elasticsearch__Uri=http://localhost:9200` for
services that need them. **Always set `ASPNETCORE_URLS` explicitly** — the checked-in
`launchSettings.json` (where it exists at all) does not default to the port CLAUDE.md
documents, and other services/the Gateway expect to find each one at its documented
port.

Testing login directly against a service this way still needs an `X-Tenant-Code`
header, since you're bypassing the Gateway's subdomain resolution — see
[deployment-guide.md §5.2](deployment-guide.md#52-identity-service--authentication-port-5001)
and [MultiTenantLogin.md](../business/Identity/MultiTenantLogin.md).

## 5. Building without Docker

The root `ERP.E-Commerce.Solution.sln` only lists 7 of the 64 `.csproj` files in this
repo (IdentityService's 4 projects, 2 of PurchaseOrderService's, plus
`src/Shared/ERP.Shared.csproj`) — **`dotnet build` on the solution will not build
everything.** Either build the project you're working on directly:

```powershell
dotnet build src/services/ProductService/src/ERP.PRD.API/ERP.PRD.API.csproj
```

or build every service's entry project in a loop:

```powershell
Get-ChildItem "src/services/*/src/*.Api*/*.csproj" | ForEach-Object { dotnet build $_.FullName }
dotnet build src/Gateway/Ocelot.API/Ocelot.API.csproj
```

## 6. Frontend apps (Admin / Storefront)

```powershell
cd src/frontend/admin      # or src/frontend/storefront
npm install
npm run dev                # iteration — Vite dev server (Admin: :5173, Storefront: :5174)
npm run build               # production build → dist/
npm run preview             # serve the dist/ build locally
```

Required env vars (copy `.env.example` to `.env` and fill in, or edit the
already-checked-in `.env` for Storefront):

| App | Var | Purpose |
|---|---|---|
| Admin | `VITE_API_URL` | Gateway base URL (`http://localhost:5000/api`) |
| Admin | `VITE_GOOGLE_CLIENT_ID`, `VITE_MSAL_CLIENT_ID`, `VITE_MSAL_AUTHORITY`, `VITE_FACEBOOK_APP_ID` | SSO provider client IDs (login page SSO buttons) |
| Storefront | `VITE_API_URL` | Gateway base URL (defaults to `/api`, relative — expects the Gateway on the same origin) |
| Storefront | `VITE_TENANT_CODE` | This deployment's fixed tenant — sent as the `X-Tenant-Code` header on every request (see §4/[MultiTenantLogin.md](../business/Identity/MultiTenantLogin.md)); Storefront doesn't rely on subdomain resolution |

Admin, by contrast, has **no** `VITE_TENANT_CODE` — it relies on the Gateway
resolving the tenant from whichever subdomain it's accessed through
(`Tenancy:BaseDomain` config, e.g. `acme.erp.local`). When testing Admin locally
without wildcard DNS set up, either add a `hosts` entry for a tenant subdomain
pointing at the Gateway, or manually add an `X-Tenant-Code` header via a browser
dev-tools override / proxy for local testing.

To deploy either app manually without Docker: `npm run build`, then serve the
resulting `dist/` folder with any static file server (or reuse the app's own
two-stage `Dockerfile` — build stage produces `dist/`, runtime stage is just nginx
serving it — by running only its `docker build`/`docker run` yourself).

## 7. Mobile apps (Customer / Staff)

```powershell
cd src/frontend/mobile/customer      # or src/frontend/mobile/staff
npm install
npx expo start                        # dev mode — scan the QR code with Expo Go
```

Neither app has an `eas.json`/EAS build profile configured yet, so there is
**no store-deployment (EAS Build/Submit) pipeline today** — `expo start` (dev mode
via Expo Go) is the only supported way to run them right now. Setting up EAS Build
for an actual app-store release is a separate, not-yet-done piece of work.
