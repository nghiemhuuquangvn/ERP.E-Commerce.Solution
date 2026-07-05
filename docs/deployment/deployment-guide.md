# ERP E-Commerce Solution — Deployment Guide

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Database Setup](#2-database-setup)
3. [Docker Compose — Quick Start](#3-docker-compose--quick-start)
4. [Kubernetes — Full Deployment](#4-kubernetes--full-deployment)
5. [Testing All APIs](#5-testing-all-apis)
6. [Testing Frontend Apps](#6-testing-frontend-apps)
7. [Messaging Health Checks](#7-messaging-health-checks)
8. [Troubleshooting](#8-troubleshooting)

---

## 1. Prerequisites

### Required Software

| Tool | Version | Install |
|------|---------|---------|
| Docker Desktop | 4.30+ | https://www.docker.com/products/docker-desktop |
| .NET SDK | 10.0 | https://dotnet.microsoft.com/download |
| SQL Server | 2022 | Running on host, port 1433 |
| kubectl | 1.30+ | Bundled with Docker Desktop |
| Node.js | 20 LTS | https://nodejs.org (for frontend dev only) |

### SQL Server Credentials

```
Server: localhost,1433  (or host.docker.internal,1433 from containers)
Username: sa
Password: !Edw@rd!123456!
```

### Hosts File Entries (Windows: C:\Windows\System32\drivers\etc\hosts)

```
127.0.0.1  erp.local
127.0.0.1  shop.erp.local
127.0.0.1  api.erp.local
127.0.0.1  rabbit.erp.local
127.0.0.1  kafka.erp.local
```

---

## 2. Database Setup

Run the SQL setup script from the repository root. It creates all 15 databases and the SuperAdmin user:

```powershell
cd d:\Documents\ERP\Sources\ERP.E-Commerce.Solution
.\src\Database\RunAllScripts.ps1
```

### Manual verification

```sql
-- Run in SSMS or Azure Data Studio
SELECT name FROM sys.databases
WHERE name IN (
  'AuthDB','SalesDB','PurchasingDB','WarehouseDB','AccountingDB',
  'CrmDB','HrmDB','MasterDataDB','ProductDB','PaymentDB',
  'ShippingDB','PosDB','PromotionDB','NotificationDB','ReportingDB'
)
ORDER BY name;
-- Expected: 15 rows
```

### SuperAdmin account

```
Email:    superadmin@system.local
Password: SuperAdmin@123
```

---

## 3. Docker Compose — Quick Start

### 3.1 Build and start all services

```powershell
cd d:\Documents\ERP\Sources\ERP.E-Commerce.Solution

# First run — builds all images (~10-15 min)
docker compose up --build -d

# Subsequent runs (no code changes)
docker compose up -d
```

### 3.2 Check all containers are healthy

```powershell
docker compose ps
```

Expected: all services show `Up` or `Up (healthy)`. Infrastructure services (RabbitMQ, Kafka, Redis, Elasticsearch) must be healthy before backend services finish starting.

### 3.3 View logs

```powershell
# All services
docker compose logs -f

# Single service
docker compose logs -f salesorderservice

# Last 50 lines
docker compose logs --tail=50 identityservice
```

### 3.4 Service URLs (Docker Compose)

| Service | URL |
|---------|-----|
| API Gateway | http://api.erp.local or http://localhost:5000 |
| Admin SPA | http://erp.local or http://localhost:3000 |
| Storefront | http://shop.erp.local or http://localhost:3001 |
| RabbitMQ UI | http://rabbit.erp.local or http://localhost:15672 (admin / Admin123) |
| Kafka UI | http://kafka.erp.local or http://localhost:8080 |
| Identity API | http://localhost:5001 |
| Sales Order API | http://localhost:5002 |
| Purchase Order API | http://localhost:5003 |
| Warehouse API | http://localhost:5004 |
| Accounting API | http://localhost:5005 |
| CRM API | http://localhost:5006 |
| HRM API | http://localhost:5007 |
| Master Data API | http://localhost:5015 |
| Product API | http://localhost:5008 |
| Payment API | http://localhost:5009 |
| Shipping API | http://localhost:5010 |
| POS API | http://localhost:5011 |
| Promotion API | http://localhost:5012 |
| Notification API | http://localhost:5013 |
| Reporting API | http://localhost:5014 |

### 3.5 Stop and clean up

```powershell
# Stop without removing volumes
docker compose down

# Stop and remove all volumes (full reset)
docker compose down -v
```

---

## 4. Kubernetes — Full Deployment

### 4.1 Enable Kubernetes in Docker Desktop

1. Open Docker Desktop → Settings → Kubernetes
2. Check **Enable Kubernetes**
3. Click **Apply & Restart**
4. Wait for the green Kubernetes indicator

Verify:
```powershell
kubectl cluster-info
kubectl get nodes
```

### 4.2 Build and tag images

Build each service image locally (Docker Desktop Kubernetes shares the Docker image cache):

```powershell
cd d:\Documents\ERP\Sources\ERP.E-Commerce.Solution

# Build all images with the shared Dockerfile
$services = @(
  @{path="src/Gateway/Ocelot.API"; proj="Ocelot.API.csproj"; tag="erp/gateway"},
  @{path="src/services/IdentityService/src/ERP.Identity.Api"; proj="ERP.Identity.API.csproj"; tag="erp/identity-service"},
  @{path="src/services/SaleOrderService/src/ERP.SO.Api"; proj="ERP.SO.Api.csproj"; tag="erp/salesorder-service"},
  @{path="src/services/PurchaseOrderService/src/ERP.PO.Api"; proj="ERP.PO.Api.csproj"; tag="erp/purchaseorder-service"},
  @{path="src/services/WareHouseService/src/ERP.WH.API"; proj="ERP.WH.API.csproj"; tag="erp/warehouse-service"},
  @{path="src/services/AccountingService/src/ERP.ACC.API"; proj="ERP.ACC.API.csproj"; tag="erp/accounting-service"},
  @{path="src/services/CRMService/src/ERP.CRM.API"; proj="ERP.CRM.API.csproj"; tag="erp/crm-service"},
  @{path="src/services/HRMService/src/ERP.HRM.API"; proj="ERP.HRM.API.csproj"; tag="erp/hrm-service"},
  @{path="src/services/SharedMasterDataService/src/ERP.MST.API"; proj="ERP.MST.API.csproj"; tag="erp/masterdata-service"},
  @{path="src/services/ProductService/src/ERP.PRD.API"; proj="ERP.PRD.API.csproj"; tag="erp/product-service"},
  @{path="src/services/PaymentService/src/ERP.PAY.API"; proj="ERP.PAY.API.csproj"; tag="erp/payment-service"},
  @{path="src/services/ShippingService/src/ERP.SHP.API"; proj="ERP.SHP.API.csproj"; tag="erp/shipping-service"},
  @{path="src/services/POSService/src/ERP.POS.API"; proj="ERP.POS.API.csproj"; tag="erp/pos-service"},
  @{path="src/services/PromotionService/src/ERP.PRM.API"; proj="ERP.PRM.API.csproj"; tag="erp/promotion-service"},
  @{path="src/services/NotificationService/src/ERP.NTF.API"; proj="ERP.NTF.API.csproj"; tag="erp/notification-service"},
  @{path="src/services/ReportingService/src/ERP.RPT.API"; proj="ERP.RPT.API.csproj"; tag="erp/reporting-service"}
)

foreach ($svc in $services) {
  Write-Host "Building $($svc.tag)..." -ForegroundColor Cyan
  docker build `
    --build-arg SERVICE_PATH=$($svc.path) `
    --build-arg PROJECT_FILE=$($svc.proj) `
    --build-arg ASSEMBLY_NAME=$($svc.proj -replace '\.csproj','') `
    -t "$($svc.tag):latest" `
    -f Dockerfile .
}

# Build frontend images
docker build -t erp/admin:latest src/frontend/admin/
docker build -t erp/storefront:latest src/frontend/storefront/
```

### 4.3 Deploy to Kubernetes

Apply manifests in order:

```powershell
cd d:\Documents\ERP\Sources\ERP.E-Commerce.Solution\deployment\k8s

# 1. Namespace
kubectl apply -f namespace.yaml

# 2. Config + Secrets
kubectl apply -f configmap.yaml
kubectl apply -f secrets.yaml

# 3. Infrastructure (order matters — wait for each to be ready)
kubectl apply -f infrastructure/redis.yaml
kubectl apply -f infrastructure/rabbitmq.yaml
kubectl apply -f infrastructure/kafka.yaml
kubectl apply -f infrastructure/elasticsearch.yaml

# Wait for infrastructure to be ready (takes 2-4 min)
kubectl wait --for=condition=ready pod -l app=redis -n erp --timeout=120s
kubectl wait --for=condition=ready pod -l app=rabbitmq -n erp --timeout=180s
kubectl wait --for=condition=ready pod -l app=kafka -n erp --timeout=180s
kubectl wait --for=condition=ready pod -l app=elasticsearch -n erp --timeout=240s

# 4. All backend services and frontend
kubectl apply -f deployments/

# 5. Ingress (requires nginx ingress controller — see step 4.4)
kubectl apply -f ingress.yaml
```

### 4.4 Install nginx Ingress Controller (once per cluster)

```powershell
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.11.0/deploy/static/provider/cloud/deploy.yaml

kubectl wait --namespace ingress-nginx `
  --for=condition=ready pod `
  --selector=app.kubernetes.io/component=controller `
  --timeout=120s
```

### 4.5 Verify deployment

```powershell
# All pods in erp namespace
kubectl get pods -n erp

# Services (note NodePort assignments)
kubectl get svc -n erp

# Ingress
kubectl get ingress -n erp
```

Expected: all pods `Running` with `READY 1/1` or `2/2`.

### 4.6 Kubernetes URLs

With ingress (add hosts file entries first):

| URL | Service |
|-----|---------|
| http://api.erp.local | API Gateway |
| http://erp.local | Admin SPA |
| http://shop.erp.local | Storefront |
| http://rabbit.erp.local | RabbitMQ Management UI |
| http://kafka.erp.local | Kafka UI |

Without ingress (NodePort direct):

| URL | Service |
|-----|---------|
| http://localhost:30500 | API Gateway |
| http://localhost:30300 | Admin SPA |
| http://localhost:30301 | Storefront |
| http://localhost:30672 | RabbitMQ Management UI |
| http://localhost:30808 | Kafka UI |

### 4.7 Scale services

```powershell
# Scale up a service
kubectl scale deployment salesorder-service --replicas=3 -n erp

# Scale down
kubectl scale deployment reporting-service --replicas=1 -n erp
```

### 4.8 Update a service image

```powershell
# 1. Rebuild the image
docker build --build-arg SERVICE_PATH=src/services/SaleOrderService/src/ERP.SO.Api `
             --build-arg PROJECT_FILE=ERP.SO.Api.csproj `
             --build-arg ASSEMBLY_NAME=ERP.SO.Api `
             -t erp/salesorder-service:latest -f Dockerfile .

# 2. Restart the deployment (pulls new local image)
kubectl rollout restart deployment/salesorder-service -n erp

# 3. Watch rollout
kubectl rollout status deployment/salesorder-service -n erp
```

---

## 5. Testing All APIs

### 5.1 Health checks

All services expose `/health`. Verify all are alive:

```powershell
$ports = 5001,5002,5003,5004,5005,5006,5007,5008,5009,5010,5011,5012,5013,5014,5015

foreach ($port in $ports) {
  try {
    $r = Invoke-WebRequest -Uri "http://localhost:$port/health" -UseBasicParsing -TimeoutSec 3
    Write-Host "Port $port : $($r.StatusCode) OK" -ForegroundColor Green
  } catch {
    Write-Host "Port $port : FAILED - $($_.Exception.Message)" -ForegroundColor Red
  }
}
```

### 5.2 Identity Service — Authentication (Port 5001)

**Register a user:**
```http
POST http://localhost:5001/api/auth/register
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "Test@1234",
  "firstName": "Test",
  "lastName": "User"
}
```

**Login and get JWT:**
```http
POST http://localhost:5001/api/auth/login
Content-Type: application/json

{
  "email": "test@example.com",
  "password": "Test@1234"
}
```
→ Save the `accessToken` from the response for subsequent calls.

**Login as SuperAdmin:**
```http
POST http://localhost:5001/api/auth/login
Content-Type: application/json

{
  "email": "superadmin@system.local",
  "password": "SuperAdmin@123"
}
```

### 5.3 Product Service — Catalog (Port 5008)

**Create a category:**
```http
POST http://localhost:5008/api/categories
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Electronics",
  "description": "Electronic products"
}
```

**Create a product:**
```http
POST http://localhost:5008/api/products
Authorization: Bearer {token}
Content-Type: application/json

{
  "name": "Laptop Pro 15",
  "sku": "LAP-PRO-15",
  "categoryId": "{categoryId}",
  "basePrice": 1299.99,
  "currency": "USD",
  "description": "High-performance laptop"
}
```

**Search products (Elasticsearch):**
```http
GET http://localhost:5008/api/products/search?q=laptop&page=1&pageSize=10
```

### 5.4 Sales Order Service — Order Flow (Port 5002)

**Create an order:**
```http
POST http://localhost:5002/api/orders
Authorization: Bearer {token}
Content-Type: application/json

{
  "customerId": "{userId}",
  "items": [
    {
      "productId": "{productId}",
      "quantity": 2,
      "unitPrice": 1299.99
    }
  ],
  "shippingAddress": {
    "street": "123 Main St",
    "city": "Ho Chi Minh City",
    "country": "VN"
  }
}
```

**Get order:**
```http
GET http://localhost:5002/api/orders/{orderId}
Authorization: Bearer {token}
```

**Approve order:**
```http
PUT http://localhost:5002/api/orders/{orderId}/approve
Authorization: Bearer {token}
```

### 5.5 Via API Gateway (Port 5000)

The Ocelot gateway proxies all services. Use it instead of direct ports in production:

```http
GET http://localhost:5000/identity/api/auth/profile
GET http://localhost:5000/products/api/products
GET http://localhost:5000/orders/api/orders
```

### 5.6 Swagger UI

Each service exposes Swagger in Development. For Production builds in Docker, use direct HTTP:

```
http://localhost:5001/swagger
http://localhost:5002/swagger
http://localhost:5008/swagger
... (same pattern for all ports 5001–5015)
```

### 5.7 Quick API test script (PowerShell)

```powershell
# 1. Login
$login = Invoke-RestMethod -Uri "http://localhost:5001/api/auth/login" `
  -Method POST -ContentType "application/json" `
  -Body '{"email":"superadmin@system.local","password":"SuperAdmin@123"}'

$token = $login.accessToken
$headers = @{ Authorization = "Bearer $token" }

# 2. Create product category
$cat = Invoke-RestMethod -Uri "http://localhost:5008/api/categories" `
  -Method POST -ContentType "application/json" -Headers $headers `
  -Body '{"name":"Test Category","description":"Test"}'

Write-Host "Category ID: $($cat.id)"

# 3. Get categories (Redis cache hit on second call)
Invoke-RestMethod -Uri "http://localhost:5008/api/categories" -Headers $headers
Invoke-RestMethod -Uri "http://localhost:5008/api/categories" -Headers $headers  # cached

Write-Host "All basic tests passed!" -ForegroundColor Green
```

---

## 6. Testing Frontend Apps

### 6.1 Admin SPA (React + TypeScript)

URL: http://erp.local (Docker Compose / K8s with hosts) or http://localhost:3000

1. Open browser → http://localhost:3000
2. Login with: superadmin@system.local / SuperAdmin@123
3. Test pages:
   - Dashboard → should load charts and KPI widgets
   - Master Data → Departments, Positions, Countries
   - Products → Categories, Products list
   - Orders → Sales Orders list
   - Users → User management

### 6.2 Storefront (Vue 3 + TypeScript)

URL: http://shop.erp.local or http://localhost:3001

1. Open browser → http://localhost:3001
2. Browse product catalog
3. Register a customer account
4. Add items to cart
5. Checkout flow

### 6.3 Frontend development mode (without Docker)

```powershell
# Admin (React)
cd src/frontend/admin
npm install
npm run dev
# → http://localhost:5173

# Storefront (Vue)
cd src/frontend/storefront
npm install
npm run dev
# → http://localhost:5174
```

---

## 7. Messaging Health Checks

### 7.1 RabbitMQ Management UI

URL: http://localhost:15672 (Docker) or http://rabbit.erp.local (with hosts)  
Credentials: admin / Admin123

Check:
- **Queues** tab → `notification-service.sales-order-events`, `reporting-service.sales-order-events` should appear after first message
- **Exchanges** tab → `sales_order_events` exchange should exist

### 7.2 Kafka UI

URL: http://localhost:8080 (Docker) or http://kafka.erp.local

Check:
- **Topics** → `identity-topic`, `purchasing-topic`, `payment-topic`, `warehouse-topic`, `hrm-topic`, `mst-topic` should appear after first publish
- **Consumer Groups** → `notification-service-events-consumer`, `masterdata-service-identity-consumer`

### 7.3 Redis inspection

```powershell
# Connect to Redis CLI in Docker
docker exec -it erp-redis redis-cli

# List all cache keys
KEYS *

# Get a cached value (example: sales order)
GET "so:order:some-guid"

# Check key TTL (seconds remaining)
TTL "so:order:some-guid"
```

For Kubernetes:
```powershell
kubectl exec -it -n erp redis-0 -- redis-cli KEYS "*"
```

### 7.4 Verify event flow (end-to-end)

1. Register a new user via `POST /api/auth/register` on Identity (port 5001)
2. Check Kafka UI → `identity-topic` → should have a `UserRegisteredDomainEvent` message
3. Check SharedMasterDataService logs → should show "Processing UserCreated event, creating Employee record"
4. Check NotificationService logs → should show "Handling UserCreated notification"

```powershell
docker compose logs masterdata-service --tail=20
docker compose logs notification-service --tail=20
```

---

## 8. Troubleshooting

### Service fails to start — "Connection refused" to SQL Server

Symptom: Service exits immediately, logs show `SqlException: A network-related error`.

Fix:
1. Confirm SQL Server is running on host: `Test-NetConnection -ComputerName localhost -Port 1433`
2. Confirm `host.docker.internal` resolves inside container: `docker exec -it erp-identity-service ping host.docker.internal`
3. Ensure SQL Server accepts TCP connections (SQL Server Configuration Manager → TCP/IP → Enabled)
4. Confirm the database exists: run `RunAllScripts.ps1`

### RabbitMQ consumer not receiving messages

Symptom: Orders created but no events flowing.

Fix:
1. Check RabbitMQ UI → Connections tab → backend service should have an active connection
2. Confirm exchange `sales_order_events` exists with type `topic`
3. Check queue bindings — queue must be bound with routing key `sales_order.#`
4. Look for errors: `docker compose logs salesorderservice | Select-String "RabbitMQ"`

### Redis cache not working — still hitting DB every request

Symptom: Response time not improving on repeated GET requests.

Fix:
1. Verify Redis container is healthy: `docker exec erp-redis redis-cli ping` → should return `PONG`
2. Check service has correct connection string: `Cache__Type=Redis` and `ConnectionStrings__Redis=redis:6379`
3. Service appsettings must have `Cache:Type = "Redis"` — Docker env vars override appsettings
4. Check service logs for StackExchange.Redis errors

### Kafka consumer group lag

Symptom: Events published but handlers not executing.

Fix:
1. Kafka UI → Consumer Groups → check lag per partition
2. If lag is growing, consumer may be crashing: `docker compose logs notificationservice | Select-String "Exception"`
3. Restart consumer: `docker compose restart notificationservice`

### Build fails for a service

```powershell
# Build a single service to see detailed errors
docker build `
  --build-arg SERVICE_PATH=src/services/SaleOrderService/src/ERP.SO.Api `
  --build-arg PROJECT_FILE=ERP.SO.Api.csproj `
  --build-arg ASSEMBLY_NAME=ERP.SO.Api `
  --progress=plain `
  -t erp/salesorder-service:latest -f Dockerfile . 2>&1
```

### Full reset (Docker Compose)

```powershell
# Stop everything and remove all containers + volumes
docker compose down -v

# Remove all erp images to force full rebuild
docker images "erp/*" -q | ForEach-Object { docker rmi $_ -f }

# Start fresh
docker compose up --build -d
```

### Full reset (Kubernetes)

```powershell
# Delete entire namespace and all resources within it
kubectl delete namespace erp

# Re-apply everything
cd deployment/k8s
kubectl apply -f namespace.yaml
kubectl apply -f configmap.yaml
kubectl apply -f secrets.yaml
kubectl apply -f infrastructure/
kubectl apply -f deployments/
kubectl apply -f ingress.yaml
```
