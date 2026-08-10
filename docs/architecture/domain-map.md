# Domain Map: ERP E-Commerce Solution

**Purpose:** Organize all 14 microservices into logical business domains and layers.

---

## Domain Groupings

### **ERP Core (Enterprise Back-Office)**

These services handle core enterprise resource planning functions.

| Service | Code | Port | Database | Responsibility |
|---------|------|------|----------|-----------------|
| **IdentityService** | Identity | 5001 | AuthDB | Authentication, authorization, user management, role/permission management |
| **AccountingService** | ACC | 5005 | AccountingDB | General ledger, accounts receivable, accounts payable, fiscal periods, journal entries |
| **HRMService** | HRM | 5007 | HrmDB | Attendance, leave requests, payroll/pay slips (Employee/Department master data now in SharedMasterDataService) |
| **PurchaseOrderService** | PO | 5003 | PurchasingDB | Purchase order management, supplier orders, purchasing workflows |

**Key Events:** UserCreated → AccountingService, HRMService, CRMService; InvoiceCreated, JournalEntryPosted; EmployeeHired, LeaveApproved; PurchaseOrderCreated

**Integration Pattern:** Purchase order → Warehouse reservation → Accounting (bill creation) → Payment

---

### **Supply Chain & Product Management**

Services managing product lifecycle, inventory, and fulfillment.

| Service | Code | Port | Database | Responsibility |
|---------|------|------|----------|-----------------|
| **ProductService** | PRD | 5008 | ProductDB | Product catalog, variants, categories, pricing, images, discontinuation |
| **WareHouseService** | WH | 5004 | WarehouseDB | Inventory management, stock movement, reservations, warehouse operations |
| **ShippingService** | SHP | 5010 | ShippingDB | Shipment management, carrier integration, shipping rates, tracking events |

**Key Events:** ProductCreated, ProductPriceChanged → POS, Promotion; SalesOrderCreated → WareHouse (reserve); InventoryReserved → Shipping; ShipmentCreated

**Integration Pattern:** Product catalog → Sales order → Inventory reserve → Shipment → Delivery tracking

---

### **E-Commerce & Sales**

Services managing customer-facing sales and commercial operations.

| Service | Code | Port | Database | Responsibility |
|---------|------|------|----------|-----------------|
| **SaleOrderService** | SO | 5002 | SalesDB | Sales orders, customer orders, sales workflows, order approvals |
| **CRMService** | CRM | 5006 | CrmDB | Contacts, leads, opportunities, cases, activities, customer interactions |
| **PaymentService** | PAY | 5009 | PaymentDB | Payment transactions, payment methods, refunds, settlement reconciliation |
| **PromotionService** | PRM | 5012 | PromotionDB | Promotions, coupons, flash sales, loyalty programs, discount rules |

**Key Events:** SalesOrderCreated → Accounting, CRM, Warehouse; PaymentReceived → Accounting, CRM; LeadQualified, OpportunityWon; PromotionApplied → Pricing

**Integration Pattern:** Lead (CRM) → Sales order → Promotion discount → Payment → Invoice → Shipment

---

### **Retail (Point-of-Sale)**

Service managing physical retail operations.

| Service | Code | Port | Database | Responsibility |
|---------|------|------|----------|-----------------|
| **POSService** | POS | 5011 | PosDB | POS terminals, transactions, line items, cash shifts, retail inventory |

**Key Events:** POSTransactionCompleted → Accounting, Inventory, Payment; CashShiftClosed → Accounting

**Integration Pattern:** POS terminal → Product lookup → Inventory decrement → Payment → Receipt generation

**Upstream Dependencies:** ProductService (product lookup), PromotionService (discount validation), PaymentService (payment processing)

---

### **Cross-Cutting Concerns**

Services providing platform-wide capabilities (pure consumers or broadcast-only).

| Service | Code | Port | Database | Responsibility |
|---------|------|------|----------|-----------------|
| **NotificationService** | NTF | 5013 | NotificationDB | Multi-channel notifications (email, SMS, push), templates, preferences, notification history |
| **ReportingService** | RPT | 5014 | ReportingDB | Analytics, business intelligence, dashboards, KPI reports (read-only data warehouse) |
| **SharedMasterDataService** | MST | 5015 | MasterDataDB | Department, position, employee, unit of measure, geography (Country→Province→District→Ward), supported-language reference data used by other services |
| **LoggingService** | LOG | 5016 | — (Elasticsearch `service-logs-*`, no SQL DB) | Centralized Information/Warning/Error/Exception log management — App Insights-style — across backend services and frontend apps; pilot scope: Gateway, ProductService, IdentityService, Admin |
| **ChatBotService** | CHB | 5017 | ChatBotDB | AI chat assistant (Storefront customers + Admin staff) — Claude or ChatGPT depending on the tenant's LicensePlan (see ModuleCodes.ChatBotPremium), answers via live tool-calling against other services' REST APIs (never a local data copy), streamed over its own SignalR hub |

**Event Consumption:** Notification/Reporting consume from ALL 14 other services (UserCreated, SalesOrderCreated, PaymentReceived, EmployeeHired, etc.); SharedMasterDataService additionally consumes UserCreated (to auto-create Employee records) and HRM's LeaveApproved (to set Employee OnLeave status); LoggingService consumes `ServiceLogEvent` from the shared `system-logs-topic` (backend) plus direct HTTP posts (frontend); ChatBotService consumes no Kafka/RabbitMQ topics at all in this MVP — it answers by calling other services' REST APIs synchronously through the Gateway, forwarding the caller's own JWT (see docs/business/ChatBot/README.md), not by building a local event-sourced projection

**Pattern:** Event fanout — NotificationService sends emails/SMS/push for all business events; ReportingService populates data warehouse for BI queries; SharedMasterDataService is the reference-data source other services increasingly depend on (e.g. HRMService's EmployeeSnapshot projection); LoggingService is a pure log sink with no SQL database, backed entirely by Elasticsearch (the only other consumer of Elasticsearch in the platform is ProductService's unrelated product-search index); ChatBotService is the one cross-cutting service that is a synchronous REST *consumer* of its siblings (Sales/Product/Warehouse/Shipping/Promotion/CRM) rather than an async event consumer

---

## Domain Dependency Graph

```
┌─────────────────────────────────────────────────────────────┐
│                      API Gateway (5000)                      │
└──────────────────┬──────────────────────────────────────────┘
                   │
         ┌─────────┼─────────┐
         │         │         │
    ┌────▼──┐  ┌───▼────┐  ┌─▼──────┐
    │Identity│  │ Product │  │Warehouse│
    │(5001)  │  │  (5008) │  │ (5004) │
    └────┬───┘  └────┬───┘  └─┬──────┘
         │           │        │
    ┌────▼─────────────┴────────┘
    │
 ┌──▼──┐  ┌──────┐  ┌─────┐  ┌────────┐
 │Sales │  │CRM   │  │Pay  │  │Shipping│
 │(5002)│  │(5006)│  │(5009)  │(5010) │
 └──┬───┘  └──────┘  └─────┘  └────────┘
    │
 ┌──▼───────────┐
 │Accounting    │
 │(5005)        │
 └──────────────┘

 ┌─────────┐  ┌─────────┐
 │HRM (5007)  │POS (5011)  Promotion(5012)
 └─────────┘  └─────────┘

 ┌──────────────┐  ┌────────────┐
 │Notification  │  │ Reporting  │
 │ (5013)       │  │  (5014)    │
 │ [Consumer-ALL]   │ [Consumer-ALL]
 └──────────────┘  └────────────┘
```

---

## Service Maturity Levels

### **Level 1: Production-Ready (Fully Scaffolded)**
- ✅ ProductService (PRD) — 25 files, domain entities, infrastructure
- ✅ AccountingService (ACC) — 18 files, domain entities, infrastructure
- ✅ CRMService (CRM) — 17 files, domain entities, infrastructure
- ✅ HRMService (HRM) — 16 files, domain entities, infrastructure

### **Level 2: Template-Ready (Documentation + Solution, Infrastructure Templated)**
- ⏳ PaymentService (PAY)
- ⏳ ShippingService (SHP)
- ⏳ POSService (POS)
- ⏳ PromotionService (PRM)
- ⏳ NotificationService (NTF)
- ⏳ ReportingService (RPT)

### **Level 3: Scaffold-Only (Core 4 Pre-Existing)**
- 🔨 IdentityService — Production-ready (JWT, RBAC, multi-tenant)
- ✅ SaleOrderService — *(Updated 2026-07-10, was "Core scaffold, minimal
  implementation" — stale)* Full CQRS: create/submit/approve/reject/pick/ship
  (incl. partial fulfillment)/deliver/cancel/return, RabbitMQ integration
  events, status-history audit trail. See `docs/business/Sales/SalesOrder.md`
  for the real state machine and known gaps (no inventory check before
  Approve, dead `OrderFulfillmentSaga`).
- ✅ PurchaseOrderService — *(Updated 2026-07-10, was "Domain complete,
  Application/Infrastructure stubs" — stale)* Full CQRS: create/submit/
  approve/cancel/receive (GRN)/schedule-receipt/compare-prices/three-way-match,
  Outbox + Kafka `purchasing-topic`. See
  `docs/business/Purchasing/PurchaseOrder.md` for the real state machine and
  known gaps (no separate Reject flow, no accept/reject quantity on receipt).
- 🔨 WareHouseService — Scaffold with README

---

## Event Flow by Domain

### **1. Order-to-Cash Cycle (E-Commerce Domain)**

```
SaleOrderService (SO)
  ↓ SalesOrderCreated
CRMService (CRM) ← Track opportunity
AccountingService (ACC) ← Create invoice
WareHouseService (WH) ← Reserve inventory
  ↓ InventoryReserved
PromotionService (PRM) ← Apply discounts
  ↓
PaymentService (PAY)
  ↓ PaymentReceived
AccountingService (ACC) ← Post receipt
CRMService (CRM) ← Close opportunity
ShippingService (SHP) ← Create shipment
  ↓
NotificationService (NTF) ← Email confirmation
ReportingService (RPT) ← Populate sales fact table
```

### **2. Procure-to-Pay Cycle (ERP Core Domain)**

```
PurchaseOrderService (PO)
  ↓ PurchaseOrderCreated
WareHouseService (WH) ← Plan receipt
AccountingService (ACC) ← Create bill
  ↓ BillApproved
PaymentService (PAY)
  ↓ PaymentProcessed
AccountingService (ACC) ← Post payment
  ↓ JournalEntryPosted
ReportingService (RPT) ← Populate GL fact table
```

### **3. User Provisioning Cycle (Identity Domain)**

```
IdentityService (Identity)
  ↓ UserCreated
AccountingService (ACC) ← Create GL user
SharedMasterDataService (MST) ← Create employee record
  ↓ EmployeeHired
HRMService (HRM) ← Sync local EmployeeSnapshot projection
CRMService (CRM) ← Create contact record
SaleOrderService (SO) ← Register salesperson
NotificationService (NTF) ← Send welcome email
```

### **4. POS Transaction Cycle (Retail Domain)**

```
POSService (POS)
  ↓ POSTransactionCompleted
ProductService (PRD) ← Lookup SKU
PromotionService (PRM) ← Validate coupon
PaymentService (PAY) ← Process payment
WareHouseService (WH) ← Decrement inventory
AccountingService (ACC) ← Post sale
NotificationService (NTF) ← Send receipt
ReportingService (RPT) ← Populate transaction fact
```

---

## Integration Patterns by Domain

### **Pattern A: Synchronous Request-Response (API Gateway)**
- **When:** User interaction, real-time data needs, validation queries
- **Services:** Identity → other services (token validation)
- **Example:** GET /products/{id} from ProductService via Gateway

### **Pattern B: Asynchronous Event Publishing (Kafka)**
- **When:** Cross-domain state changes, eventual consistency acceptable
- **Services:** All domain state changes (OrderCreated, PaymentReceived, InvoicePosted)
- **Topic:** {service-name}-topic per service
- **Guarantee:** Outbox pattern ensures no lost events

### **Pattern C: Consumer Fanout (Broadcast Events)**
- **When:** One event needs multiple independent handlers
- **Services:** NotificationService, ReportingService
- **Pattern:** Both subscribe to all 13+ topics, process independently
- **No coupling:** Each maintains own event handler list

### **Pattern D: Data Warehouse (Read-Only Cache)**
- **When:** Analytics, BI queries, reporting dashboards
- **Service:** ReportingService
- **Sources:** Consume from all Kafka topics, populate fact/dimension tables
- **Access:** External BI tools (Tableau, Power BI) query ReportingService DB directly

---

## Database Isolation & Service Boundaries

### **Database Ownership Rule**
Each service owns its database. No other service may access it directly.

| Service | Database | Owned Tables | Exposed Via |
|---------|----------|--------------|------------|
| IdentityService | AuthDB | Users, Roles, Permissions, Tenants, Claims | REST API + JWT tokens |
| SaleOrderService | SalesDB | Orders, OrderLines, OrderStatus, Customers | REST API + Kafka events |
| PurchaseOrderService | PurchasingDB | POs, POLines, Suppliers, ReceiptSchedules | REST API + Kafka events |
| WareHouseService | WarehouseDB | Inventory, StockMovements, Reservations, Locations | REST API + Kafka events |
| AccountingService | AccountingDB | GLAccounts, JournalEntries, Invoices, Bills, APVouchers | REST API + Kafka events |
| CRMService | CrmDB | Contacts, Leads, Opportunities, Cases, Activities | REST API + Kafka events |
| HRMService | HrmDB | LeaveRequests, PaySlips, Attendance, EmployeeSnapshots (read-only projection) | REST API + Kafka events |
| ProductService | ProductDB | Products, Variants, Categories, Images, PriceLists | REST API + Kafka events |
| PaymentService | PaymentDB | Transactions, Methods, Refunds, Settlements, Reconciliation | REST API + Kafka events |
| ShippingService | ShippingDB | Shipments, Carriers, Rates, TrackingEvents, Manifests | REST API + Kafka events |
| POSService | PosDB | Terminals, Transactions, LineItems, CashShifts, Registers | REST API + Kafka events |
| PromotionService | PromotionDB | Promotions, Coupons, FlashSales, LoyaltyAccounts, Rules | REST API + Kafka events |
| NotificationService | NotificationDB | Templates, Notifications, Preferences, Channels, History | REST API + Kafka events |
| ReportingService | ReportingDB | FactSalesOrders, FactPayments, DimProducts, DimCustomers, DimDates | REST API (read-only) |
| SharedMasterDataService | MasterDataDB | Departments, Positions, Employees, UnitsOfMeasure, Countries/Provinces/Districts/Wards, SupportedLanguages, {Entity}Translations | REST API + Kafka events |

---

## Deployment Topology

### **Local Development (All services on localhost)**

```
Docker Compose Stack:
  - sqlserver:1433 (14 databases)
  - kafka:9092
  - zookeeper:2181
  - redis:6379 (caching, sessions)
  - nginx:80/443 (API Gateway)
  
Services (14 x dotnet run):
  - localhost:5000 (Ocelot API Gateway)
  - localhost:5001 (IdentityService)
  - localhost:5002 (SaleOrderService)
  - localhost:5003 (PurchaseOrderService)
  - localhost:5004 (WareHouseService)
  - localhost:5005 (AccountingService)
  - localhost:5006 (CRMService)
  - localhost:5007 (HRMService)
  - localhost:5008 (ProductService)
  - localhost:5009 (PaymentService)
  - localhost:5010 (ShippingService)
  - localhost:5011 (POSService)
  - localhost:5012 (PromotionService)
  - localhost:5013 (NotificationService)
  - localhost:5014 (ReportingService)
```

### **Production (Kubernetes Deployment)**

```
Kubernetes Cluster:
  - SQL Server Pod (RDS equivalent) with 14 databases
  - Kafka Cluster (3 brokers, HA)
  - Redis Cluster (caching, sessions)
  - Ingress Controller (API Gateway → Ocelot Service)
  
Service Deployments (1 pod minimum per service, auto-scale):
  - ocelot-gateway (2 replicas)
  - identity-service (3 replicas)
  - saleorder-service (4 replicas)
  - purchaseorder-service (2 replicas)
  - warehouse-service (3 replicas)
  - accounting-service (2 replicas)
  - crm-service (2 replicas)
  - hrm-service (2 replicas)
  - product-service (3 replicas)
  - payment-service (3 replicas)
  - shipping-service (2 replicas)
  - pos-service (4 replicas)
  - promotion-service (2 replicas)
  - notification-service (2 replicas)
  - reporting-service (1 replica, horizontal scale for large BI queries)

Monitoring:
  - Prometheus + Grafana (metrics collection/dashboards)
  - ELK Stack (logs aggregation)
  - Jaeger (distributed tracing)
```

---

## Next Steps (Phase 2)

1. ✅ Create domain-map.md (this file)
2. ⏳ Create port-map.md (port assignments + network topology)
3. ⏳ Update CLAUDE.md Service Boundaries section (all 14 services)
4. ⏳ Create/update architecture docs (service-map.md, event-catalog.md, database-map.md, dependency-map.md, integration-map.md)
5. ⏳ Fill business documentation (42 files: 30 new + 12 existing)
6. ⏳ Create API contracts (14 files with endpoint specifications)

---

**Created:** 2026-06-13  
**Domain Groupings:** 4 (ERP Core, Supply Chain, E-Commerce, Retail) + 2 (Cross-Cutting)  
**Services Mapped:** 14 total
