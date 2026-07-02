# ERP E-Commerce Solution

## Project Overview

ERP E-Commerce Solution is a .NET microservices ERP platform.

Architecture:
- Microservices
- DDD
- CQRS
- Clean Architecture
- Event Driven Architecture
- Saga Partern
- Outbox Partern

Technology:
- .NET 10
- SQL Server
- RabbitMQ
- Kafka
- Elasticsearch (product search)
- Ocelot API Gateway
- Docker
- Kubernetes

## Repository Structure

src/
    services/
        IdentityService/               (5001)
        SaleOrderService/              (5002)
        PurchaseOrderService/          (5003)
        WareHouseService/              (5004)
        AccountingService/             (5005, Code: ACC)
        CRMService/                    (5006, Code: CRM)
        HRMService/                    (5007, Code: HRM)
        ProductService/                (5008, Code: PRD)
        PaymentService/                (5009, Code: PAY)
        ShippingService/               (5010, Code: SHP)
        POSService/                    (5011, Code: POS)
        PromotionService/              (5012, Code: PRM)
        NotificationService/           (5013, Code: NTF)
        ReportingService/              (5014, Code: RPT)
        SharedMasterDataService/       (5015, Code: MST)

    Shared/
        Core/                          (Domain base classes: BaseEntity, AggregateRoot, ValueObject, IDomainEvent)
        Contracts/                     (Integration event contracts: IntegrationEventBase)
        Infrastructure/                (Shared implementations: OutboxMessage)
        Messaging/                     (Message bus: IMessageBus, IEventHandler, OutboxMessage)

    Database/
        (Database migration scripts per service)

    Gateway/
        Ocelot.API/                    (API Gateway on port 5000)

    frontend/
        admin/                         (React 18 + TS admin back-office SPA)
        storefront/                    (Vue 3.5 + TS customer-facing web storefront)
        mobile/
            customer/                  (Expo/React Native customer app)
            staff/                     (Expo/React Native field/retail staff app)

docs/
    frontend/                          (Frontend app guides: stacks, i18n, Master Data CRUD pattern)
    architecture/
        domain-map.md                  (Domain groupings: ERP Core, Supply Chain, E-Commerce, Retail, Cross-Cutting)
        port-map.md                    (Network topology & port assignments)
        service-map.md                 (Service dependency graph)
        event-catalog.md               (All 39+ integration events & their publishers/consumers)
        database-map.md                (14 database schemas & ERDs)
        dependency-map.md              (Service-to-service dependencies)
        integration-map.md             (Event flow matrix across all services)

    business/
        Identity/
        Sales/
        Purchasing/
        Warehouse/
        Accounting/
        CRM/
        HRM/
        Product/
        Payment/
        Shipping/
        POS/
        Promotion/
        Notification/
        Reporting/

    Events/
        EventCatalog.md

deployment/
    docker/
        docker-compose.yml             (All 14 services + infrastructure)
        Dockerfile                     (Per service)

    k8s/
        configmap.yaml
        secrets.yaml
        deployments/                   (Deployment manifests for all 14 services)
        services/                      (Kubernetes service definitions)
        statefulsets/                  (For stateful services)

tests/
    unit/                              (Unit tests per service)
    integration/                       (Integration tests per service)
    performance/                       (Load testing, stress testing)

## Service Boundaries

**Reference:** See `docs/architecture/domain-map.md` for complete domain groupings, `docs/architecture/port-map.md` for network topology, and `docs/architecture/service-map.md` for service dependency graph.

### ERP Core (Enterprise Back-Office)

#### Identity Service (Port 5001)

Responsibilities:
- Authentication (JWT-based)
- External login / SSO (Google, Microsoft, Facebook, Twitter/X) — Google/Microsoft/
  Facebook verify a client-obtained token via `IExternalAuthProvider`; Twitter/X uses
  a separate OAuth 2.0 Authorization Code + PKCE redirect flow (`TwitterOAuthService`)
  since it has no equivalent single-shot verifiable token
- Two-factor authentication (2FA) via SMS or Email, code delivered through
  NotificationService via Kafka (`identity-topic`) — never a direct/synchronous call
- Auth settings, two-tier: `SystemAuthSettings` (SuperAdmin-only master switch, all
  tenants) and `TenantAuthSettings` (per-tenant, requires the System switch to also be
  on) — each gates the external-login providers and 2FA availability/requirement
- Authorization & access control
- User management & lifecycle
- Role-based access control (RBAC)
- Permission management
- Multi-tenant support

Database: AuthDB

Published Events:
- UserCreated
- UserUpdated
- RoleAssigned
- PermissionGranted
- TwoFactorCodeGenerated (consumed only by NotificationService, to deliver the OTP)

Consumed Events: None

#### Accounting Service (Port 5005, Code: ACC)

Responsibilities:
- General ledger (GL) accounting
- Accounts receivable (AR)
- Accounts payable (AP)
- Journal entries & posting
- Fiscal periods & closing
- Financial reporting foundations

Database: AccountingDB

Published Events:
- InvoiceCreated
- BillApproved
- JournalEntryPosted
- FiscalPeriodClosed

Consumed Events:
- SalesOrderCreated
- PurchaseOrderCreated
- PaymentReceived
- WarehouseMovement

#### Human Resources Service (Port 5007, Code: HRM)

Responsibilities:
- Attendance tracking
- Leave request management
- Payroll processing
- Pay slip generation

Note: Employee and Department master data moved to SharedMasterDataService (Port 5015,
Code: MST) — HRM keeps a local, read-only EmployeeSnapshot projection (synced from MST's
Kafka events) so leave/payroll handlers can check employee status without a synchronous
cross-service call. Do not re-add Employee/Department entities here.

Database: HrmDB

Published Events:
- PaySlipGenerated
- LeaveApproved
- LeaveRejected

Consumed Events:
- EmployeeHired, EmployeeTerminated, EmployeeTransferred (from SharedMasterDataService)

#### Shared Master Data Service (Port 5015, Code: MST)

Responsibilities:
- Department & position (job title) master data
- Employee master data (hire/terminate/transfer/profile — HR transactional records
  like leave/payroll stay in HRMService, referencing Employee only by Id)
- Unit of measure (UoM) reference data
- Geographic hierarchy: Country → Province → District → Ward
- Supported language reference list (admin-managed; each service's own
  Localization:SupportedCultures config is the runtime source of truth)
- Per-language translation rows (Name/Description) for its own reference entities

Database: MasterDataDB

Published Events:
- DepartmentCreated, DepartmentUpdated
- PositionCreated
- EmployeeHired, EmployeeTerminated, EmployeeTransferred
- UnitOfMeasureCreated
- CountryCreated

Consumed Events:
- UserCreated (from IdentityService — auto-creates an Employee record)
- LeaveApproved (from HRMService — sets the canonical Employee to OnLeave)

#### Purchase Order Service (Port 5003, Code: PO)

Responsibilities:
- Purchase order creation & management
- Supplier management
- Purchasing workflows
- PO approvals
- Receipt scheduling

Database: PurchasingDB

Published Events:
- PurchaseOrderCreated
- PurchaseOrderApproved
- ReceiptScheduled

Consumed Events:
- UserCreated

---

### Supply Chain & Product Management

#### Product Service (Port 5008, Code: PRD)

Responsibilities:
- Product catalog management
- Product variants (SKUs, colors, sizes)
- Product categories & hierarchy
- Product images & media
- Price lists & pricing strategies
- Product lifecycle (active, discontinued)

Database: ProductDB

Published Events:
- ProductCreated
- ProductUpdated
- ProductPriceChanged
- ProductDiscontinued

Consumed Events: None (upstream source)

#### Warehouse Service (Port 5004, Code: WH)

Responsibilities:
- Inventory management
- Stock movement tracking
- Inventory reservations
- Warehouse locations
- Stock adjustments
- Receiving & put-away

Database: WarehouseDB

Published Events:
- InventoryReserved
- StockMoved
- InventoryAdjusted
- ReceiptCompleted

Consumed Events:
- SalesOrderCreated
- PurchaseOrderCreated

#### Shipping Service (Port 5010, Code: SHP)

Responsibilities:
- Shipment creation & tracking
- Carrier integration & management
- Shipping rate calculation
- Tracking events & updates
- Manifest generation
- Delivery confirmation

Database: ShippingDB

Published Events:
- ShipmentCreated
- TrackingUpdated
- DeliveryConfirmed

Consumed Events:
- SalesOrderApproved
- InventoryReserved

---

### E-Commerce & Sales

#### Sales Order Service (Port 5002, Code: SO)

Responsibilities:
- Sales order creation & management
- Customer order processing
- Sales order workflows
- Order approvals
- Order status tracking
- Line item management

Database: SalesDB

Published Events:
- SalesOrderCreated
- SalesOrderApproved
- SalesOrderShipped

Consumed Events:
- UserCreated
- ProductCreated
- PromotionCreated

#### CRM Service (Port 5006, Code: CRM)

Responsibilities:
- Contact & customer management
- Lead tracking & qualification
- Sales opportunity management
- Customer support cases
- Activity logging
- Customer interaction history

Database: CrmDB

Published Events:
- LeadCreated
- LeadQualified
- OpportunityWon
- CaseOpened
- CaseResolved

Consumed Events:
- UserCreated
- SalesOrderCreated
- PaymentReceived

#### Payment Service (Port 5009, Code: PAY)

Responsibilities:
- Payment transaction processing
- Payment method management
- Payment gateway integration
- Refund processing & management
- Settlement reconciliation
- Payment authorization & capture

Database: PaymentDB

Published Events:
- PaymentProcessed
- PaymentRefunded
- SettlementCreated
- PaymentFailed

Consumed Events:
- SalesOrderCreated
- InvoiceCreated

#### Promotion Service (Port 5012, Code: PRM)

Responsibilities:
- Promotion & campaign management
- Coupon creation & redemption
- Flash sale management
- Loyalty program management
- Discount rules & calculations
- Promotion eligibility

Database: PromotionDB

Published Events:
- PromotionCreated
- CouponRedeemed
- LoyaltyPointsEarned
- FlashSaleStarted

Consumed Events:
- ProductCreated
- SalesOrderCreated

---

### Retail (Point-of-Sale)

#### POS Service (Port 5011, Code: POS)

Responsibilities:
- POS terminal management
- Retail transaction processing
- Line item management (cart)
- Cash shift management
- Register reconciliation
- Local inventory sync

Database: PosDB

Published Events:
- TransactionCreated
- CashShiftClosed
- RegisterBalanced

Consumed Events:
- ProductCreated
- PromotionCreated
- PaymentProcessed

---

### Cross-Cutting Concerns

#### Notification Service (Port 5013, Code: NTF)

Responsibilities:
- Multi-channel notification delivery (email, SMS, push)
- Notification template management
- Notification preference management
- Notification history tracking
- Channel coordination

Database: NotificationDB

Published Events: None (consumer-only service)

Consumed Events:
- UserCreated
- SalesOrderCreated
- PaymentProcessed
- EmployeeHired
- LeadQualified
- TwoFactorCodeGenerated (from IdentityService — sends the OTP via SMS or Email
  depending on the event's `Method`)
- (and all other significant domain events)

Note: a live Kafka consumer (`IdentityEventConsumer`, `IHostedService`) exists only
for IdentityService's `identity-topic` today — it is the only topic this service
actually subscribes to at runtime; the other consumed-event names above describe the
intended, not-yet-wired design for the other 12 services' topics. Follow
`IdentityEventConsumer`'s pattern (mirrors HRMService's `MasterDataEventConsumer`,
dispatches via `IMediator.Publish`) when wiring up the next topic — don't assume the
`INotification` handler classes already in `IntegrationEventHandlers.cs` are live
until a matching consumer exists for their topic.

**Pattern:** Consumes from all 13 other service topics for event-driven notifications.

#### Reporting Service (Port 5014, Code: RPT)

Responsibilities:
- Analytics & business intelligence
- Dashboard data population
- KPI calculations
- Data warehouse management
- Report generation foundations
- BI tool integration (Tableau, Power BI)

Database: ReportingDB (read-only data warehouse)

Published Events: None (consumer-only, analytics-only)

Consumed Events:
- All events from all services (denormalized into fact & dimension tables)

**Pattern:** Read-only Kafka consumer populating OLAP fact/dimension tables for BI queries.

---

## Database Ownership

Each service owns its database exclusively. No cross-database queries allowed.

| Service | Database | Rule |
|---------|----------|------|
| IdentityService | AuthDB | Owned by Identity; others access via API only |
| SaleOrderService | SalesDB | Owned by Sales; others access via API only |
| PurchaseOrderService | PurchasingDB | Owned by Purchasing; others access via API only |
| WareHouseService | WarehouseDB | Owned by Warehouse; others access via API only |
| AccountingService | AccountingDB | Owned by Accounting; others access via API only |
| CRMService | CrmDB | Owned by CRM; others access via API only |
| HRMService | HrmDB | Owned by HRM; others access via API only |
| ProductService | ProductDB | Owned by Product; others access via API only |
| PaymentService | PaymentDB | Owned by Payment; others access via API only |
| ShippingService | ShippingDB | Owned by Shipping; others access via API only |
| POSService | PosDB | Owned by POS; others access via API only |
| PromotionService | PromotionDB | Owned by Promotion; others access via API only |
| NotificationService | NotificationDB | Owned by Notification; others access via API only |
| ReportingService | ReportingDB | Owned by Reporting; read-only via direct BI tool queries |
| SharedMasterDataService | MasterDataDB | Owned by MasterData; others access via API/events only |

## Database Ownership

Identity Service
    -> Identity Database

Sales Service
    -> Sales Database

Purchase Service
    -> Purchasing Database

Warehouse Service
    -> Warehouse Database

## Integration Rules

Services must not access another service database directly.

Allowed:
- REST API
- RabbitMQ
- Kafka
- Shared Contracts

Not Allowed:
- Cross Database Query
- Direct Table Access

## Coding Standards

Architecture:
- Clean Architecture
- TDD
- DDD
- CQRS

Application Layer:
- Use MediatR
- Use FluentValidation

Domain Layer:
- Contains business rules only

Infrastructure Layer:
- External systems
- Database access
- Message broker

API Layer:
- Controllers only

## Messaging

RabbitMQ:
- Commands
- Integration Events

Kafka:
- Event Streaming
- Analytics

Shared Contracts:
src/Shared/Contracts

## Documentation

Architecture:
docs/architecture

Business:
docs/business

Events:
docs/Events

Conventions:
docs/conventions

Frontend:
docs/frontend (app stacks, i18n pattern, Admin Master Data CRUD pattern — read
before adding a new frontend page/entity so existing conventions are reused, not
re-derived)

## Claude Working Rules

When reviewing code:

1. Stay within the current service boundary.

2. Do not move business logic into Infrastructure.

3. Do not introduce cross-service database access.

4. Prefer existing Shared Contracts before creating new DTOs.

5. Reuse existing events before creating new integration events.

6. Follow repository structure.

7. Review business documentation before changing business logic.

8. Review EventCatalog before modifying messaging flows.

9. Before adding a new frontend page, entity, or translation, review
   `docs/frontend/` — reuse the existing i18n/Master Data CRUD patterns instead of
   creating a parallel mechanism.

## Priority Reading Order

1. **CLAUDE.md** (This file — service boundaries, database ownership, coding standards)
2. **docs/architecture/domain-map.md** (Domain groupings, service clusters, event flows by domain)
3. **docs/architecture/port-map.md** (Port assignments, network topology, service discovery)
4. **docs/architecture/service-map.md** (Visual service dependency graph, communication patterns)
5. **docs/architecture/event-catalog.md** (All 39+ integration events, publishers, consumers, C# definitions)
6. **docs/architecture/database-map.md** (14 database schemas, entities per database)
7. **docs/architecture/dependency-map.md** (Complete service-to-service dependency matrix)
8. **Service README.md** (e.g., ProductService/README.md — business logic, entities, events, endpoints)
9. **docs/business/{Domain}/*.md** (Domain business documentation — workflows, rules, examples)
10. **docs/frontend/*.md** (Frontend app stacks, i18n pattern, Admin Master Data CRUD pattern — read before touching `src/frontend/`)
11. **Source Code** (After understanding architecture and business domain)

