# Complete Architecture Reference Guide

**Last Updated:** 2026-06-13  
**Version:** 2.0 (All 14 services documented)  
**Audience:** Architects, Lead Developers, Platform Engineers

---

## Quick Navigation

> **Note (2026-07-27 docs cleanup):** this file was renamed from
> `ARCHITECTURE_REFERENCE.md` to serve as this folder's index (matching
> `docs/frontend/README.md`'s role). The Quick Navigation links below were
> fixed to point at the real, current filenames — the rest of this document
> is a design-level reference dated 2026-06-13 (pre-dates
> `SharedMasterDataService`, so it says "14 services" throughout) and may
> drift from what's actually wired in code in places, same caveat as
> `dependency-map.md`/`integration-map.md`.

| Topic | File | Purpose |
|-------|------|---------|
| **Domain Organization** | domain-map.md | Domain groupings + event flows |
| **Network & Ports** | port-map.md | Service ports, Kubernetes topology |
| **Service Overview** | service-map.md | Services with dependencies |
| **Database Design** | database-map.md | Databases with SQL schemas |
| **All Events** | event-catalog.md | Event definitions, structures, handlers |
| **Dependencies** | dependency-map.md | Who calls whom, REST + Kafka |
| **Integration Flows** | integration-map.md | Event flows, critical paths |
| **UI → API Request Flow** | end-to-end-request-flow.md | Traces real UI actions (Admin/Storefront/mobile) through the Gateway into each service's Controller/Command/Domain/events, with actual routes and known UI-wiring gaps |
| **Governance** | ../../CLAUDE.md | Service boundaries, integration rules |
| **Deployment** | ../deployment/deployment-guide.md | Environment setup, Docker Compose, Kubernetes |
| **Manual Build/Deploy** | ../deployment/manual-build-deploy-guide.md | Building and running one service or frontend app individually, outside Docker Compose |

---

## System Overview

### 14 Microservices

```
ERP Core (4)              E-Commerce (5)           Retail (1)
├─ Identity              ├─ Sales Order           ├─ POS
├─ Accounting            ├─ CRM
├─ HRM                   ├─ Payment
├─ Purchasing            ├─ Promotion
                         ├─ Shipping

Supply Chain (3)         Cross-Cutting (2)
├─ Product               ├─ Notification (consumer)
├─ Warehouse             ├─ Reporting (consumer)
└─ (none)
```

### Key Metrics

| Metric | Value |
|--------|-------|
| **Total Services** | 14 |
| **Total Databases** | 14 (one per service) |
| **Total API Endpoints** | 200+ |
| **Total Integration Events** | 48 |
| **Event Flows** | 209 |
| **Kafka Topics** | 12 (one per publisher) |
| **REST Dependencies** | 20+ |
| **Peak Load** | ~42 events/second |
| **Daily Event Volume** | ~213 MB |

---

## Architecture Principles

### 1. **Database Per Service**
- ✅ Each service owns its database exclusively
- ✅ No cross-database queries allowed
- ✅ API-only coupling between services
- ✅ Enables independent scaling & technology choice

### 2. **Event-Driven Architecture**
- ✅ State changes published as events (Kafka)
- ✅ Outbox pattern for guaranteed delivery
- ✅ Consumers subscribe to relevant topics
- ✅ Eventual consistency accepted within SLA

### 3. **Domain-Driven Design**
- ✅ Bounded contexts = Services
- ✅ Aggregate roots = Domain entities
- ✅ Business logic in domain layer
- ✅ Value objects for immutable concepts

### 4. **Clean Architecture**
- ✅ 4-layer structure: API → Application → Domain → Infrastructure
- ✅ Dependencies point inward (API depends on Domain)
- ✅ Domain has zero dependencies
- ✅ Infrastructure is pluggable

### 5. **CQRS (Command Query Responsibility Segregation)**
- ✅ Commands for state-changing operations (create, update, delete)
- ✅ Queries for read-only operations (list, search, get)
- ✅ MediatR for command/query dispatching
- ✅ Handlers implement business logic

---

## Service Tiers & Dependencies

### Tier 0: Foundation (No Dependencies)
- **ProductService** — Source of truth for product catalog

### Tier 1: Core Authentication
- **IdentityService** — All services depend on user validation

### Tier 2: Transaction Processing
- **SalesOrderService** — Order pipeline
- **WarehouseService** — Inventory management
- **AccountingService** — Financial records

### Tier 3: Fulfillment & Payments
- **PaymentService** — Cash collection
- **ShippingService** — Order delivery
- **PurchaseOrderService** — Supply chain

### Tier 4: Support Services
- **CRMService** — Customer relationships
- **HRMService** — Payroll
- **PromotionService** — Discounts & loyalty

### Tier 5: Retail
- **POSService** — Point-of-sale transactions

### Tier 6: Analytics & Notifications
- **NotificationService** — Consumer-only (all 48 events)
- **ReportingService** — Consumer-only (data warehouse)

---

## Critical Business Workflows

### 1. Order-to-Cash (E-Commerce)
**Duration:** 2-10 minutes  
**Events:** 8  
**Services Involved:** Sales, Warehouse, Accounting, Payment, Shipping, Notification, Reporting

```
Order Created
  → Inventory Reserved
  → Invoice Created
  → Payment Processed
  → GL Posted
  → Shipment Created
  → Order Delivered
  → Revenue Recognized
```

**SLA:** Order confirmation within 5 seconds, fulfillment within 24 hours

### 2. Procure-to-Pay (Supply Chain)
**Duration:** 2-7 days  
**Events:** 6  
**Services Involved:** Purchasing, Warehouse, Accounting, Payment, Notification

```
PO Created
  → Receipt Scheduled
  → Goods Received
  → Bill Approved
  → Payment Processed
  → GL Posted
```

**SLA:** PO confirmation within 1 hour, payment within terms

### 3. Retail POS (Retail)
**Duration:** 30-60 seconds  
**Events:** 3  
**Services Involved:** Product, Promotion, Payment, Warehouse, Accounting

```
Transaction Created
  → Inventory Decremented
  → GL Posted
  → Receipt Emailed
```

**SLA:** POS transaction under 5 seconds, GL posting within 5 minutes

### 4. User Provisioning (Identity)
**Duration:** 10-30 seconds  
**Events:** 4  
**Services Involved:** Identity, HRM, CRM, Accounting

```
User Created
  → Employee Record Created
  → Contact Record Created
  → GL User Created
  → Welcome Email Sent
```

**SLA:** User fully provisioned within 1 minute

---

## Communication Patterns

### Synchronous (REST)
**When:** Real-time validation, lookups, state checking  
**Examples:** Product lookup, user validation, inventory check  
**Timeout:** 3-10 seconds  
**Circuit Breaker:** After 3 failures, open circuit

```
Client Request
  → Calling Service (API Controller)
  → MediatR Command Handler
  → Repository (database query)
  → Response (DTO)
  → Client (JSON)
```

### Asynchronous (Kafka Events)
**When:** State changes, notifications, analytics  
**Examples:** OrderCreated, PaymentProcessed, InventoryReserved  
**Delivery:** Guaranteed (Outbox pattern)  
**Latency:** 100-5,000ms

```
Domain Event Raised
  → Aggregate Root.AddDomainEvent()
  → DbContext.SaveChanges() (same transaction)
  → OutboxMessage table (persisted)
  → OutboxProcessor (every 5 seconds)
  → Kafka (published)
  → Consumer Handlers (subscribed services)
  → Business Logic
```

---

## Data Flow Patterns

### Pattern A: Query (Synchronous)
```
User calls: GET /api/products/search?q=laptop
  ↓
ProductsController.SearchProducts()
  ↓
MediatR.Send(SearchProductsQuery)
  ↓
SearchProductsHandler
  ↓
Repository.GetAllAsync() → Database
  ↓
Map to DTOs
  ↓
Return JSON response
```

**Latency:** 50-200ms  
**Caching:** Product price cached in Redis (5-minute TTL)

### Pattern B: Command (Synchronous)
```
User calls: POST /api/orders with OrderCreateCommand
  ↓
OrdersController.CreateOrder()
  ↓
FluentValidation validates input
  ↓
MediatR.Send(CreateSalesOrderCommand)
  ↓
CreateSalesOrderHandler
  ↓
SalesOrder.Create() [domain logic]
  ↓
Repository.AddAsync()
  ↓
UnitOfWork.CommitAsync() [same transaction]
  ├─ Save SalesOrder to database
  ├─ Save domain events to OutboxMessages table
  └─ Transaction commits atomically
  ↓
Return OrderId (201 Created)
```

**Latency:** 100-500ms  
**Atomic:** All-or-nothing (ACID)  
**Events:** Persisted for async processing

### Pattern C: Event Handling (Asynchronous)
```
SalesOrderCreatedEvent published to Kafka
  ↓
WarehouseService subscribed
  ↓
WarehouseService.HandleSalesOrderCreatedEvent()
  ↓
Query inventory availability
  ↓
Create reservation
  ↓
Add domain event: InventoryReservedDomainEvent
  ↓
Save to database + OutboxMessages table
  ↓
OutboxProcessor publishes InventoryReservedEvent
  ↓
SalesService subscribes to InventoryReservedEvent
  ↓
Update order status to "Inventory Reserved"
```

**Latency:** 200-1,000ms (event latency)  
**Guaranteed Delivery:** Yes (Outbox pattern)  
**Order Guarantee:** Per-aggregate (events within same aggregate ordered)

---

## Technology Stack

### Backend
- **Framework:** ASP.NET Core 10.0
- **Language:** C# 12
- **Architecture:** Clean Architecture, DDD, CQRS
- **Database:** SQL Server 2022 (per-service)
- **ORM:** Entity Framework Core 8.0

### Message Broker
- **Primary:** Apache Kafka 7.5
- **Secondary:** RabbitMQ (optional, commands)
- **Pattern:** Outbox for guaranteed delivery

### API Gateway
- **Gateway:** Ocelot
- **Port:** 5000 (local), 443 (production)
- **Authentication:** JWT (Identity Service) — Gateway validates the signature, then
  forwards `X-User-*` headers for convenience; since 2026-07-08 every one of the 13
  downstream services *also* independently re-validates the JWT itself
  (`AddErpJwtAuthentication`), rather than trusting those forwarded headers alone —
  see `docs/architecture/end-to-end-request-flow.md` §1.2
- **Rate Limiting:** Not yet implemented at the Gateway/`ocelot.json` level; login
  brute-force protection exists in IdentityService itself (2026-07-08)

### Dependency Injection
- **Container:** Microsoft.Extensions.DependencyInjection
- **Query/Command Bus:** MediatR
- **Validation:** FluentValidation
- **Logging:** Serilog

### Deployment
- **Containerization:** Docker
- **Orchestration:** Kubernetes (production)
- **Container Registry:** Docker Hub / Azure Container Registry
- **Service Mesh:** Istio (optional, Phase 4)

---

## Configuration by Environment

### Development (Local)
```
Services: 14 instances (localhost:5001-5014)
Database: SQL Server (localhost:1433)
Kafka: Docker Compose (localhost:9092)
Auth: JWT with dev certificates
Logging: Console + Serilog
```

### Docker Compose
```
Services: 14 containers (service-names)
Database: SQL Server container (sqlserver:1433)
Kafka: Kafka container + Zookeeper (kafka:9092)
Network: erp-network (internal bridge)
Auth: JWT with shared secret
Logging: Serilog + ELK stack
```

### Production (Kubernetes)
```
Services: 14 deployments (auto-scaling, ≥2 replicas)
Database: AWS RDS SQL Server (managed)
Kafka: AWS MSK (managed)
Network: VPC, Service mesh (Istio)
Auth: JWT with Azure Key Vault
Logging: Managed CloudWatch + ELK
```

---

## Service Ports (Local Development)

```
5000  - Ocelot API Gateway
5001  - IdentityService
5002  - SaleOrderService
5003  - PurchaseOrderService
5004  - WareHouseService
5005  - AccountingService
5006  - CRMService
5007  - HRMService
5008  - ProductService
5009  - PaymentService
5010  - ShippingService
5011  - POSService
5012  - PromotionService
5013  - NotificationService
5014  - ReportingService

HTTPS (7xxx for localhost SSL)
1433  - SQL Server
9092  - Kafka
2181  - Zookeeper
6379  - Redis
```

---

## Database Design

### Database Per Service
14 independent SQL Server databases:

```
AuthDB          → IdentityService
SalesDB         → SaleOrderService
PurchasingDB    → PurchaseOrderService
WarehouseDB     → WareHouseService
AccountingDB    → AccountingService
CrmDB           → CRMService
HrmDb           → HRMService
ProductDB       → ProductService
PaymentDB       → PaymentService
ShippingDB      → ShippingService
PosDB           → POSService
PromotionDB     → PromotionService
NotificationDB  → NotificationService
ReportingDB     → ReportingService (data warehouse)
```

### Outbox Pattern (All Databases)
Every database includes `OutboxMessages` table for guaranteed event delivery:

```sql
CREATE TABLE OutboxMessages (
    Id BIGINT PRIMARY KEY IDENTITY,
    Type NVARCHAR(255),          -- Event class name
    Content NVARCHAR(MAX),       -- JSON
    OccurredOnUtc DATETIME2,
    ProcessedOnUtc DATETIME2,    -- NULL = pending
    Error NVARCHAR(MAX),
    CreatedAt DATETIME2,
    INDEX IX_Outbox_ProcessedOnUtc (ProcessedOnUtc) WHERE ProcessedOnUtc IS NULL
);
```

---

## Event Catalog (48 Events)

### By Service

| Service | Event Count | Topics | Flow |
|---------|-----------|--------|------|
| Identity | 3 | identity-topic | UserCreated, UserUpdated, RoleAssigned |
| Sales | 5 | salesorder-topic | OrderCreated, Approved, Shipped, Delivered, Cancelled |
| Purchasing | 4 | purchaseorder-topic | POCreated, Approved, ReceiptScheduled, GoodsReceived |
| Warehouse | 4 | warehouse-topic | ReservedEvent, StockMoved, AdjustedEvent, LowStockAlert |
| Accounting | 4 | accounting-topic | InvoiceCreated, BillApproved, JournalPosted, PeriodClosed |
| CRM | 5 | crm-topic | LeadCreated, LeadQualified, OpportunityWon, CaseOpened, CaseResolved |
| HRM | 4 | hrm-topic | EmployeeHired, EmployeeTerminated, PaySlipGenerated, LeaveApproved |
| Product | 4 | product-topic | ProductCreated, Published, PriceChanged, Discontinued |
| Payment | 4 | payment-topic | PaymentInitiated, PaymentProcessed, Refunded, Failed |
| Shipping | 3 | shipping-topic | ShipmentCreated, TrackingUpdated, DeliveryConfirmed |
| POS | 3 | pos-topic | TransactionCreated, CashShiftClosed, RegisterBalanced |
| Promotion | 4 | promotion-topic | PromotionCreated, Activated, CouponRedeemed, LoyaltyEarned |
| **TOTAL** | **48** | **12** | — |

---

## API Endpoints (200+ Total)

### ProductService (Example - 8 Implemented, Phase 3.1)
```
POST   /api/products
GET    /api/products
GET    /api/products/{id}
GET    /api/products/sku/{sku}
GET    /api/products/search
PUT    /api/products/{id}/description
POST   /api/products/{id}/publish
POST   /api/products/{id}/discontinue
```

### Each Service (Pattern)
```
CRUD Operations:
  POST   /api/{resource}           - Create
  GET    /api/{resource}/{id}      - Get by ID
  GET    /api/{resource}           - List (paginated)
  PUT    /api/{resource}/{id}      - Update
  DELETE /api/{resource}/{id}      - Delete (soft)

Workflow Operations:
  POST   /api/{resource}/{id}/{action}  - Action (Approve, Publish, etc.)
  
Search/Filter:
  GET    /api/{resource}/search    - Full-text search

Relationships:
  GET    /api/{resource}/{id}/{relation}  - Get related records
  POST   /api/{resource}/{id}/{relation}  - Link resources
  DELETE /api/{resource}/{id}/{relation}  - Unlink resources

Health Check:
  GET    /health                   - Service health (anonymous)
  GET    /health/ready             - Readiness probe (Kubernetes)
  GET    /health/live              - Liveness probe (Kubernetes)
```

---

## Resilience & Failure Handling

### Circuit Breaker (Synchronous Calls)
```
REST call to Service B
  → 5-second timeout (if no response)
  → Retry up to 3 times
  → After 3 failures: Open circuit (fail-fast for 30 seconds)
  → Fallback: Return cached data or error
```

### Outbox Pattern (Asynchronous Events)
```
Domain event published
  → OutboxMessage persisted (same transaction as domain change)
  → Guaranteed: No event loss
  → OutboxProcessor polls every 5 seconds
  → Retry forever (exponential backoff)
  → DeadLetter queue (DLQ) if permanently failed
```

### Service Recovery SLAs

| Service | Down | Impact | Recovery | Manual Recovery |
|---------|------|--------|----------|-----------------|
| Identity | < 15min | Read-only, cached permissions | Automatic | N/A |
| Product | < 1hr | Use cached catalog | Automatic | N/A |
| Warehouse | < 30min | Orders queue | Automatic | Manual allocation |
| Sales | < 2hr | Orders queue | Automatic | Resume from queue |
| Payment | < 4hr | Manual payment | Automatic | Manual bank deposit |
| Accounting | < 8hr | GL posting batched | Automatic | Batch reconciliation |
| Shipping | < 4hr | Orders held | Automatic | Warehouse override |
| POS | < 1hr | Offline mode | Automatic | Manual count |
| Notification | n/a | Emails delayed | Automatic | Email backlog |
| Reporting | n/a | Dashboards stale | Automatic (batch) | N/A |

---

## Security Model

### Authentication
- **Method:** JWT (JSON Web Tokens)
- **Issuer:** IdentityService
- **Expiry:** 1 hour (access token), 7 days (refresh token)
- **Transport:** HTTPS only
- **Storage:** Secure cookies (httpOnly, Secure flags)

### Authorization
- **Model:** Role-Based Access Control (RBAC)
- **Roles:** Admin, Manager, Staff, Customer (extensible)
- **Permissions:** Resource + Action (e.g., "Order.Create", "Invoice.View")
- **Enforcement:** API layer ([Authorize] attribute)

### Cross-Service Security
- **Service-to-Service:** mTLS (mutual TLS) or API keys
- **Database Access:** SQL Server integrated auth + service accounts
- **Secrets Management:** Azure Key Vault (production)

### Data Protection
- **Encryption at Rest:** SQL Server TDE (Transparent Data Encryption)
- **Encryption in Transit:** TLS 1.3
- **PII Handling:** Masked in logs, encrypted fields
- **Audit Trail:** All GL changes logged with user ID

---

## Monitoring & Observability

### Logging
- **Level:** Debug (dev) → Information (production)
- **Tool:** Serilog
- **Sink:** Console (dev) → CloudWatch (production)
- **Structured:** JSON format with correlation IDs

### Metrics
- **Tool:** Prometheus
- **Dashboard:** Grafana
- **Key Metrics:**
  - Request count (by endpoint, status)
  - Response latency (P50, P95, P99)
  - Error rate (by service)
  - Database connection pool
  - Kafka consumer lag

### Tracing
- **Tool:** Jaeger
- **Correlation ID:** Passed through all service calls
- **Traces:** Order creation → fulfillment end-to-end

### Alerting
- **Alert If:**
  - Service down (health check fails)
  - Response time P99 > 5 seconds
  - Error rate > 1%
  - Outbox backlog > 1,000 messages
  - Database query > 1 second

---

## Deployment

### Local Development
```bash
# Run all services in Docker Compose
docker-compose up

# Access
API Gateway: http://localhost:5000
Swagger: http://localhost:5000/swagger
Services: http://localhost:500X (where X = 1-14)
```

### Kubernetes
```yaml
# Deploy all 14 services
kubectl apply -f k8s/services/

# Verify
kubectl get pods
kubectl get services
kubectl logs <pod-name>

# Scale
kubectl scale deployment product-service --replicas=3
```

---

## Performance Targets

### Latency (P99)
- Single product lookup: < 50ms
- Order creation (with inventory check): < 500ms
- Payment processing: 3-5 seconds
- GL posting: < 5 seconds
- Notification delivery: < 30 seconds

### Throughput
- REST API: 1,000 req/sec per service
- Kafka: 100 events/sec per topic (easily scales)
- POS retail: 10,000 transactions/min per store

### Storage
- Daily: ~213 MB events
- 7-day Kafka retention: ~1.5 GB
- Database growth: 100 MB → 1 TB over 5 years (with archiving)

### Availability
- Service SLA: 99.9% uptime (4.3 hours downtime/year)
- RTO (Recovery Time): 15 min - 8 hours (depends on service)
- RPO (Recovery Point): 15 min (every 15 minutes for critical services)

---

## Next Steps (Phase 3-4)

### Phase 3 (In Progress)
- ✅ ProductService CQRS implementation
- ⏳ Complete remaining 13 services (estimated 180+ hours)
- ⏳ Integration tests for all workflows
- ⏳ Performance testing & optimization

### Phase 4 (Future)
- Event Sourcing (store all events permanently)
- CQRS read models (denormalized views)
- Service mesh (Istio)
- Advanced monitoring (tracing, profiling)
- Saga pattern for distributed transactions

---

**Created:** 2026-06-13  
**Total Architecture Documents:** 12 comprehensive files  
**Services Documented:** 14 (all)  
**Total Workflow Paths:** 209 event flows  
**API Endpoints:** 200+ (8 implemented, 192+ planned)  
**Events:** 48 (all documented)
