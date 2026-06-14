# Service Map

Complete overview of microservices architecture, service responsibilities, dependencies, and communication patterns.

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Client Applications                         │
│                                                                 │
└─────────────────────────────────┬───────────────────────────────┘
                                  │ HTTP/HTTPS
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                   Ocelot API Gateway                            │
│              (Service Discovery & Routing)                      │
└──────────────┬──────────────┬──────────────┬──────────────┬─────┘
               │              │              │              │
        HTTP/REST       HTTP/REST     HTTP/REST      HTTP/REST
               │              │              │              │
        ┌──────▼─────┐ ┌──────▼─────┐ ┌──────▼─────┐ ┌──────▼──────┐
        │  Identity  │ │    Sales   │ │ Purchasing │ │  Warehouse  │
        │  Service   │ │   Service  │ │  Service   │ │   Service   │
        └──────┬─────┘ └──────┬─────┘ └──────┬─────┘ └──────┬──────┘
               │              │              │              │
               ├──────┬───────┼──────┬───────┼──────┬───────┤
               │      │       │      │       │      │       │
               └──────┼───────┼──────┼───────┼──────┼───────┘
                      │       │      │       │      │
                      ▼       ▼      ▼       ▼      ▼
                  ┌──────────────────────────────────────┐
                  │      RabbitMQ Message Bus            │
                  │  (Commands & Integration Events)     │
                  └──────────────────────────────────────┘
                  
                      Kafka Cluster
              (Event Streaming & Analytics)
```

## All 14 Services

**Complete service catalog organized by domain. For detailed specifications, see each service's README.md**

### ERP Core (Back-Office)

#### 1. Identity Service (Port 5001)

**Purpose:** Centralized authentication, authorization, and user/role management.

**Key Entities:** User, Role, Permission, UserRole, Tenant (multi-tenant support)

**Database:** AuthDB

**Published Events:** UserCreated, UserUpdated, RoleAssigned, PermissionGranted

**Consumed Events:** None

**Dependencies:** Upstream (no service dependencies)

**README:** src/services/IdentityService/README.md

---

#### 2. Accounting Service (Port 5005, Code: ACC)

**Purpose:** General ledger, accounts receivable, accounts payable, fiscal periods.

**Key Entities:** Account, JournalEntry, Invoice, Bill, FiscalPeriod

**Database:** AccountingDB

**Published Events:** InvoiceCreated, BillApproved, JournalEntryPosted, FiscalPeriodClosed

**Consumed Events:** SalesOrderCreated, PurchaseOrderCreated, PaymentReceived, WarehouseMovement

**Dependencies:** Sales, Purchasing, Warehouse, Payment

**README:** src/services/AccountingService/README.md

---

#### 3. Human Resources Service (Port 5007, Code: HRM)

**Purpose:** Employee management, payroll, leave, attendance, departments.

**Key Entities:** Employee, Department, LeaveRequest, PaySlip, Attendance

**Database:** HrmDB

**Published Events:** EmployeeHired, EmployeeTerminated, PaySlipGenerated, LeaveApproved

**Consumed Events:** UserCreated

**Dependencies:** Identity

**README:** src/services/HRMService/README.md

---

#### 4. Purchase Order Service (Port 5003, Code: PO)

**Purpose:** Purchase order management, supplier relationships, purchasing workflows.

**Key Entities:** PurchaseOrder, Supplier, PurchaseOrderItem, PurchaseOrderStatus

**Database:** PurchasingDB

**Published Events:** PurchaseOrderCreated, PurchaseOrderApproved, ReceiptScheduled

**Consumed Events:** UserCreated, InventoryUpdatedEvent

**Dependencies:** Identity, Warehouse

**README:** src/services/PurchaseOrderService/README.md

---

### Supply Chain & Product Management

#### 5. Product Service (Port 5008, Code: PRD)

**Purpose:** Product catalog, variants, categories, pricing, images.

**Key Entities:** Product, ProductVariant, Category, ProductImage, PriceList

**Database:** ProductDB

**Published Events:** ProductCreated, ProductUpdated, ProductPriceChanged, ProductDiscontinued

**Consumed Events:** None

**Dependencies:** Upstream (no service dependencies)

**README:** src/services/ProductService/README.md

---

#### 6. Warehouse Service (Port 5004, Code: WH)

**Purpose:** Inventory management, stock movements, reservations, warehouse operations.

**Key Entities:** Inventory, StockMovement, Reservation, WarehouseLocation

**Database:** WarehouseDB

**Published Events:** InventoryReserved, StockMoved, InventoryAdjusted, ReceiptCompleted

**Consumed Events:** SalesOrderCreated, PurchaseOrderCreated, SalesOrderCancelled

**Dependencies:** Sales, Purchasing, Identity

**README:** src/services/WareHouseService/README.md

---

#### 7. Shipping Service (Port 5010, Code: SHP)

**Purpose:** Shipment management, carrier integration, tracking, delivery confirmation.

**Key Entities:** Shipment, Carrier, ShippingRate, TrackingEvent, Manifest

**Database:** ShippingDB

**Published Events:** ShipmentCreated, TrackingUpdated, DeliveryConfirmed

**Consumed Events:** SalesOrderApproved, InventoryReserved

**Dependencies:** Sales, Warehouse

**README:** src/services/ShippingService/README.md

---

### E-Commerce & Sales

#### 8. Sales Order Service (Port 5002, Code: SO)

**Purpose:** Sales order management, customer orders, sales workflows, fulfillment.

**Key Entities:** SalesOrder, Customer, OrderItem, OrderStatus

**Database:** SalesDB

**Published Events:** SalesOrderCreated, SalesOrderApproved, SalesOrderShipped

**Consumed Events:** UserCreated, ProductCreated, PromotionCreated

**Dependencies:** Identity, Product, Promotion

**README:** src/services/SaleOrderService/README.md

---

#### 9. CRM Service (Port 5006, Code: CRM)

**Purpose:** Customer relationship management, leads, opportunities, support cases, activities.

**Key Entities:** Contact, Lead, Opportunity, Case, Activity

**Database:** CrmDB

**Published Events:** LeadCreated, LeadQualified, OpportunityWon, CaseOpened, CaseResolved

**Consumed Events:** UserCreated, SalesOrderCreated, PaymentReceived

**Dependencies:** Identity, Sales, Payment

**README:** src/services/CRMService/README.md

---

#### 10. Payment Service (Port 5009, Code: PAY)

**Purpose:** Payment processing, payment methods, refunds, settlements, reconciliation.

**Key Entities:** PaymentTransaction, PaymentMethod, Refund, Settlement, Reconciliation

**Database:** PaymentDB

**Published Events:** PaymentProcessed, PaymentRefunded, SettlementCreated, PaymentFailed

**Consumed Events:** SalesOrderCreated, InvoiceCreated

**Dependencies:** Sales, Accounting

**README:** src/services/PaymentService/README.md

---

#### 11. Promotion Service (Port 5012, Code: PRM)

**Purpose:** Promotions, coupons, flash sales, loyalty programs, discount rules.

**Key Entities:** Promotion, Coupon, FlashSale, LoyaltyProgram, DiscountRule

**Database:** PromotionDB

**Published Events:** PromotionCreated, CouponRedeemed, LoyaltyPointsEarned, FlashSaleStarted

**Consumed Events:** ProductCreated, SalesOrderCreated

**Dependencies:** Product, Sales

**README:** src/services/PromotionService/README.md

---

### Retail (Point-of-Sale)

#### 12. POS Service (Port 5011, Code: POS)

**Purpose:** Retail point-of-sale terminals, transactions, cash shifts, register management.

**Key Entities:** POSTerminal, POSTransaction, LineItem, CashShift, Register

**Database:** PosDB

**Published Events:** TransactionCreated, CashShiftClosed, RegisterBalanced

**Consumed Events:** ProductCreated, PromotionCreated, PaymentProcessed

**Dependencies:** Product, Promotion, Payment

**README:** src/services/POSService/README.md

---

### Cross-Cutting Concerns

#### 13. Notification Service (Port 5013, Code: NTF)

**Purpose:** Multi-channel notifications (email, SMS, push), templates, preferences, history.

**Key Entities:** NotificationTemplate, Notification, NotificationPreference, NotificationChannel

**Database:** NotificationDB

**Published Events:** None (consumer-only service)

**Consumed Events:** ALL events from all 13 other services

**Dependencies:** All services (event fanout pattern)

**Pattern:** Consumes all domain events and sends notifications via Email, SMS, Push channels

**README:** src/services/NotificationService/README.md

---

#### 14. Reporting Service (Port 5014, Code: RPT)

**Purpose:** Analytics, BI, dashboards, reporting, data warehouse (read-only).

**Key Entities:** FactSalesOrder, FactPayment, DimProduct, DimCustomer, DimDate

**Database:** ReportingDB (OLAP data warehouse)

**Published Events:** None (consumer-only, analytics-only)

**Consumed Events:** ALL events from all 13 other services

**Dependencies:** All services (event fanout pattern)

**Pattern:** Read-only Kafka consumer populating fact & dimension tables for BI analysis

**README:** src/services/ReportingService/README.md

---

## Service Communication Patterns

### 1. Synchronous Communication (REST)

**Used for:**
- Real-time data lookups
- User-initiated actions
- Immediate validation needs

**Example:**
```
Sales Service → [HTTP GET] → Identity Service
(Validate user role before creating order)
```

**Timeouts:** 5-10 seconds per request
**Circuit Breaker:** Enabled for resilience

### 2. Asynchronous Communication (RabbitMQ)

**Used for:**
- Event notifications
- Workflow orchestration
- Fire-and-forget operations

**Example:**
```
Sales Service → [Publish] → RabbitMQ → Warehouse Service
(SalesOrderCreatedEvent triggers inventory reservation)
```

**Delivery Guarantee:** Exactly-once with Outbox Pattern
**Retry Policy:** 3 immediate retries, then exponential backoff

### 3. Event Streaming (Kafka)

**Used for:**
- Analytics and reporting
- Audit logging
- Data warehousing
- Real-time dashboards

**Example:**
```
All Services → [Stream] → Kafka → Analytics Engine
(Collect all events for business intelligence)
```

**Topics:**
- `erp.sales.events`
- `erp.purchasing.events`
- `erp.inventory.events`
- `erp.audit-log`

---

## Service Dependencies

### Complete Dependency Graph (All 14 Services)

```
                    ┌──────────────┐
                    │   Identity   │ (No dependencies)
                    │   Service    │
                    │   (5001)     │
                    └──────┬───────┘
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
    ┌───▼────┐         ┌───▼───┐         ┌───▼────┐
    │  HRM   │         │ Sales │         │Product │
    │(5007)  │         │(5002) │         │(5008)  │
    └────────┘         └───┬───┘         └───┬────┘
                           │                  │
                    ┌──────┴──────┐           │
                    │             │           │
                ┌───▼──┐      ┌───▼────┐  ┌──▼─────┐
                │CRM   │      │Warehouse   │Promotion
                │(5006)│      │(5004)  │  │(5012)
                └──────┘      └───┬────┘  └────────┘
                                  │
                    ┌─────────────┼─────────────┐
                    │             │             │
                ┌───▼──┐      ┌───▼──┐     ┌───▼────┐
                │Payment   │Accounting  │Shipping│
                │(5009)│      │(5005)  │  │(5010)
                └──────┘      └────────┘  └────────┘
                                  │
                    ┌─────────────┼──────────────────┐
                    │             │                  │
                ┌───▼──┐      ┌───▼────┐        ┌───▼──┐
                │ POS  │      │Purchse │        │ ...  │
                │(5011)│      │(5003)  │        │ ...  │
                └──────┘      └────────┘        └──────┘
                    
                ┌──────────────────────────────────────┐
                │  Notification Service (NTF, 5013)    │
                │  Consumes from ALL 13 services       │
                │  (Event fanout pattern)              │
                └──────────────────────────────────────┘
                
                ┌──────────────────────────────────────┐
                │  Reporting Service (RPT, 5014)       │
                │  Consumes from ALL 13 services       │
                │  (Data warehouse population)         │
                └──────────────────────────────────────┘
```

### Cross-Service Dependencies (All Calls)

| From | To | Method | Event(s) | Purpose |
|------|-----|--------|----------|---------|
| **Sales** | Identity | REST | UserCreated | Validate user/role |
| **Sales** | Warehouse | Event | SalesOrderCreated | Reserve inventory |
| **Sales** | Accounting | Event | SalesOrderCreated | Invoice generation |
| **Sales** | CRM | Event | SalesOrderCreated | Track opportunity |
| **Sales** | Product | REST | ProductCreated | Price lookup |
| **Sales** | Promotion | REST | PromotionCreated | Discount validation |
| **Purchasing** | Identity | REST | UserCreated | Validate user/role |
| **Purchasing** | Warehouse | Event | PurchaseOrderCreated | Prepare receipt |
| **Purchasing** | Accounting | Event | PurchaseOrderCreated | Bill recording |
| **Warehouse** | Identity | REST | InventoryReserved | Audit trail |
| **Warehouse** | Sales | Event | InventoryReserved | Confirm reservation |
| **Warehouse** | Accounting | Event | StockMoved | Inventory valuation |
| **Accounting** | Sales | Event | InvoiceCreated | Post to ledger |
| **Accounting** | Purchasing | Event | BillApproved | Post to ledger |
| **Accounting** | Payment | Event | PaymentReceived | Post receipt |
| **CRM** | Identity | REST | UserCreated | Register contact |
| **CRM** | Sales | REST | SalesOrderCreated | Track pipeline |
| **CRM** | Payment | Event | PaymentReceived | Update status |
| **Payment** | Accounting | Event | PaymentProcessed | Post receipt |
| **Payment** | Sales | Event | PaymentProcessed | Update order |
| **Shipping** | Warehouse | Event | ShipmentCreated | Decrement inventory |
| **Shipping** | Sales | Event | DeliveryConfirmed | Close order |
| **POS** | Product | REST | TransactionCreated | Lookup SKU |
| **POS** | Promotion | REST | TransactionCreated | Validate coupon |
| **POS** | Payment | REST | TransactionCreated | Process payment |
| **POS** | Warehouse | Event | TransactionCreated | Decrement inventory |
| **Notification** | All 13 | Kafka | All events | Multi-channel delivery |
| **Reporting** | All 13 | Kafka | All events | Data warehouse population |

### Service Criticality & Dependencies

| Service | Dependency Count | Criticality | Upstream | Downstream |
|---------|-----------------|------------|----------|-----------|
| **Identity** | 0 | CRITICAL | None | All others |
| **Product** | 0 | CRITICAL | None | Sales, POS, Promotion |
| **Warehouse** | 2 | CRITICAL | Identity, Sales, Purchasing | Accounting, Shipping |
| **Sales** | 3 | CRITICAL | Identity, Product, Promotion | Accounting, Payment, CRM, Warehouse |
| **Accounting** | 3 | HIGH | Sales, Purchasing, Payment | Reporting |
| **Payment** | 1 | HIGH | Sales | Accounting |
| **Purchasing** | 2 | HIGH | Identity, Warehouse | Accounting, Warehouse |
| **CRM** | 2 | MEDIUM | Identity, Sales, Payment | Notification |
| **HRM** | 1 | MEDIUM | Identity | Notification |
| **Shipping** | 2 | MEDIUM | Sales, Warehouse | Notification |
| **Promotion** | 1 | MEDIUM | Product | Sales, POS |
| **POS** | 3 | MEDIUM | Product, Promotion, Payment | Warehouse, Accounting |
| **Notification** | 13 | LOW | All services | None |
| **Reporting** | 13 | LOW | All services | External BI tools |

### Forbidden Calls

These patterns MUST NOT be used:

❌ **Direct Database Access**
```csharp
// WRONG: Accessing another service's database
var order = _salesDbContext.Orders.Find(id);
```

✅ **Use REST API or Events**
```csharp
// RIGHT: Call API endpoint
var order = await _salesService.GetOrderAsync(id);
```

❌ **Tight Coupling via Synchronous Cascades**
```csharp
// WRONG: Chain of synchronous calls
var order = GetOrder(id);
var inventory = ReserveInventory(order);  // Sync call
var invoice = GenerateInvoice(order);     // Sync call
```

✅ **Use Events for Orchestration**
```csharp
// RIGHT: Publish event, services react independently
PublishEvent(new OrderCreatedEvent { OrderId = id });
```

---

## Data Flow Scenarios

### Scenario 1: Create Sales Order

```
User Request
    ↓
API Gateway (Route to Sales Service)
    ↓
Sales Service (Create SalesOrder aggregate)
    ↓
Save to SalesDB
    ↓
Publish SalesOrderCreatedEvent
    ↓
RabbitMQ (Route to subscribers)
    ├→ Warehouse Service (Process InventoryReservedEvent)
    │   ├→ Query WarehouseDB for stock
    │   ├→ Create Reservation
    │   └→ Publish InventoryReservedEvent
    │
    ├→ Accounting Service (Process accounting)
    │   ├→ Create invoice liability
    │   └→ Record in general ledger
    │
    └→ Kafka (Stream to analytics)
        └→ Update dashboard & reports
```

### Scenario 2: Receive Purchase Order

```
Warehouse Staff Input
    ↓
API Gateway (Route to Purchasing Service)
    ↓
Purchasing Service (Update PurchaseOrder status)
    ├→ Validate receipt against order
    ├→ Record received quantities
    └→ Save to PurchasingDB
    ↓
Publish PurchaseOrderReceivedEvent
    ↓
RabbitMQ (Route to subscribers)
    ├→ Warehouse Service
    │   ├→ Update Inventory (Received items)
    │   ├→ Publish InventoryUpdatedEvent
    │   └→ Release receiving location
    │
    ├→ Accounting Service
    │   ├→ Record actual cost
    │   ├→ Update inventory valuation
    │   └→ Process supplier invoice
    │
    └→ Kafka (Stream to analytics)
        └→ Update purchasing metrics
```

---

## Performance Considerations

### Database Per Service

Each service has isolated database for:
- **Independent Scaling**: Scale based on individual load
- **Technology Choice**: Use appropriate DB type per service
- **Failure Isolation**: DB failures don't cascade
- **Data Consistency**: ACID within service, eventual consistency across services

### Caching Strategy

| Service | Cache | TTL | Strategy |
|---------|-------|-----|----------|
| Identity | Permissions, Roles | 1 hour | Redis |
| Sales | Customer data | 30 min | In-memory |
| Warehouse | Inventory | 5 min | Redis |
| Purchasing | Supplier data | 1 hour | Redis |

### Asynchronous Processing

- Long-running operations via background jobs
- Event processing is non-blocking
- Reservation holds have expiration times
- Automatic cleanup of stale reservations

---

## Failure Scenarios & Resilience

### When Identity Service is Down
- Other services function independently
- Failed authentication requests are rejected
- Historical role cache allows some operations

### When Warehouse Service is Down
- Sales orders can be created but not fulfilled
- Inventory updates are queued
- UI shows "Out of Stock" for items
- Manual intervention may be needed

### When RabbitMQ is Down
- Outbox pattern prevents message loss
- Services retry publishing when broker recovers
- No events are lost

### Circuit Breaker Pattern
- REST calls have circuit breakers
- Failed calls are retried with exponential backoff
- Service degrades gracefully under load

---

## Monitoring & Observability

### Key Metrics per Service

- **Request Count**: API calls per minute
- **Response Time**: P50, P95, P99 latencies
- **Error Rate**: Failed requests percentage
- **Business Metrics**: Orders/minute, revenue, etc.

### Distributed Tracing

All requests have CorrelationId for end-to-end tracing:
```
[CorrelationId: abc-123-def]
  Sales Service → Warehouse Service → Inventory Update
```

### Logging Standards

- **INFO**: Key business events
- **WARN**: Recoverable issues
- **ERROR**: Failed operations
- **DEBUG**: Detailed execution flow

---

## Deployment Topology

### Development
- All services in Docker Compose
- Shared databases (if needed)
- Single-instance deployments

### Production
- Kubernetes pods with auto-scaling
- Separate database servers
- Multi-replica deployments
- Load balancing via Service mesh

---

**Last Updated:** 2026-06-13  
**Version:** 1.0
