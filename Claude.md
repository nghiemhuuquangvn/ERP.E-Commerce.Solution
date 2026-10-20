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
        LoggingService/                (5016, Code: LOG)
        ChatBotService/                (5017, Code: CHB)

    Shared/                            (ERP.Shared.csproj is a zero-code umbrella; each folder
                                        below is its own ERP.Shared.* project)
        Core/                          (Domain base classes: BaseEntity, AggregateRoot, ValueObject, IDomainEvent)
        Contracts/                     (Integration event contracts: IntegrationEventBase,
                                        SettingsChanged, TenantLicenseChanged, ServiceLogEvent)
        Messaging/                     (IMessageBus, IEventHandler, OutboxMessage,
                                        OutboxEventDeserializer)
        Authorization/                 (Permissions, ModuleCodes, license middleware, user context)
        Infrastructure/                (Caching, Data (repository/tenant filter), Exceptions)
        Localization/, Middleware/, Observability/ (Kafka log sink), Extensions/,
        Security/ (AES secret protector), Versioning/
        Infrastructure/Data/           (ITenantProvider, TenantScope, ApplyTenantQueryFilters,
                                        EnforceTenantOnSave — multi-tenant isolation)

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

deployment/
    docker/
        README.md                      (How to run the whole platform via Docker —
                                         no host-installed SQL Server needed)
        run-migrations.sh              (Applies every service's SQL migration against
                                         the "sqlserver" container; used by db-migrate)

    (docker-compose.yml and the shared Dockerfile actually live at the repo root,
    not under deployment/docker/ — their build contexts are relative to repo root)

    k8s/
        configmap.yaml
        secrets.yaml
        deployments/                   (Deployment manifests: 17 services + gateway + frontend)
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
- PasswordResetRequested, EmailConfirmationRequested (consumed only by
  NotificationService; carry single-use links — excluded from tenant webhooks)

Security invariants (2026-10-19 review — docs/business/Identity/SecurityReview-2026-10.md):
- Anonymous account creation (register, external-login auto-provisioning) is ALWAYS
  Customer — never trust a client-supplied UserType/role.
- Secrets/tokens (reset, confirm, OTP) only leave the service via NotificationService
  events — never in an HTTP response (dev-only flag excepted, blocked outside Development).
- Every token-issuing path goes through `EnsureAccountCanAuthenticateAsync`
  (IsActive user + tenant); credential/status changes call
  `ISessionService.RevokeAllForUserAsync`.
- Client IP comes from `RemoteIpAddress` (after `UseForwardedHeaders` with trusted
  networks), never from the raw `X-Forwarded-For` header.
- Outbound calls to tenant-configured URLs (webhooks) go through `WebhookUrlRules`.
- No secrets in tracked appsettings; `JwtKeyGuard` rejects known keys outside Development.

Tenant & permission scope (2026-10-20):
- **Tenant = request host.** The Gateway resolves host → tenant via IdentityService
  (`ITenantDomainService`: `Tenancy:PlatformHost` → SYSTEM, `TenantDomains` row, or
  `<code>.<Tenancy:BaseDomain>`), strips client `X-Tenant-*`/`X-User-*` headers, and
  rejects a JWT whose `tenant_id` ≠ the host's tenant. Never read a tenant from a request
  body/query for authentication flows.
- **Platform vs tenant permissions** — `ERP.Shared.Authorization.PermissionScopes` is the
  single list of cross-tenant (platform) permissions. They are effective only in the
  platform tenant (`PlatformTenant.Id`), stripped from every other token
  (`EffectivePermissions.ResolveAsync` — the ONLY way to compute token permissions), and
  not grantable elsewhere (`EffectivePermissions.WhyNotGrantableAsync`: also "only grant
  what you hold" — used by role grants and API key scopes).
- No authority from role NAMES: `AuthorizePermission` checks permissions only; platform
  role codes (`PlatformRoles.IsReserved`) can't be created.
- Cross-tenant endpoints: put `[TenantRouteAccess(<platform permission>)]` on the
  controller (backed by `ITenantAccessPolicy`) — don't hand-write tenant checks.

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
- UserCreated (from IdentityService — auto-creates an Employee record). The
  transport itself (Outbox → `identity-topic` → `IdentityEventConsumer`) had
  two independent serialization bugs fixed 2026-07-21 (missing
  `TypeNameHandling.All` on the publish side, wrong class-name substring
  check on the consume side) — before that fix this event silently never
  dispatched at all. Even with transport fixed, auto-creation still won't
  fire for a real user today: IdentityService's `UserRegisteredDomainEvent`/
  `UserRegisteredIntegrationEvent` never carries `IsEmployee`/
  `DefaultDepartmentId` (registration has no concept of "this new user is an
  employee, in department X" yet), and the consumer's own guard clause
  intentionally no-ops without them. Wiring that up is a product decision
  (how does a user get flagged as an employee at registration?), not
  implemented.
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

Live consumers today (verified 2026-10-20):
- `IdentityEventConsumer` — `identity-topic`: TwoFactorCodeGenerated,
  PasswordResetRequested, EmailConfirmationRequested, UserRegistered, RoleAssigned,
  PermissionGranted
- `NotificationKafkaConsumer` — `hrm-topic`, `mst-topic`, `payment-topic`,
  `purchasing-topic`, `shipping-topic`: EmployeeHired, LeaveApproved/Rejected,
  PaymentProcessed, PurchaseOrderApproved, PurchaseOrderPendingFinanceApproval,
  ShipmentCreated
- `SalesOrderRabbitMqConsumer` — RabbitMQ: SalesOrderCreated, SalesOrderApproved

Not subscribed yet: crm, accounting, pos, promotion, product, warehouse topics. Don't
assume an `INotification` handler in `IntegrationEventHandlers.cs` is live until one of
the consumers above dispatches to it (pattern: parse `$type`, map to the local
integration event, `IMediator.Publish`).

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

#### ChatBot Service (Port 5017, Code: CHB)

Responsibilities:
- AI chat assistant for Storefront customers (incl. anonymous guests) and Admin staff
- Answers via live tool-calling against other services' REST APIs through the Gateway,
  forwarding the caller's own JWT — never a local copy of order/product/inventory data
- Claude (paid) vs ChatGPT-free provider selection, driven entirely by the tenant's
  existing LicensePlan (see `docs/business/Identity/License.md` and
  `ERP.Shared.Authorization.ModuleCodes.ChatBotPremium`) — no new billing code
- Streams replies over its own SignalR hub (`/hubs/chat`, port 5017 directly — bypasses
  the Gateway, which doesn't reliably proxy SignalR's WebSocket upgrade)
- Escalation to a human via CRMService's existing Case API, using the caller's own JWT

Database: ChatBotDB

Published Events: None (no Outbox in this MVP — see README.md)

Consumed Events: None (answers via synchronous REST, not event consumption)

**Pattern:** The one cross-cutting service that is a synchronous REST *consumer* of its
siblings (Sales/Product/Warehouse/Shipping/Promotion/CRM) rather than an async event
consumer like Notification/Reporting.

---

#### Logging Service (Port 5016, Code: LOG)

Responsibilities:
- Centralized log management for all backend services and frontend apps
- Consumes `ServiceLogEvent` (Shared/Contracts) from Kafka `system-logs-topic` — produced
  by every service through the shared Serilog Kafka sink
  (`ERP.Shared.Extensions.LoggingExtensions` → `Observability/KafkaLogSink`)
- Client-side log ingestion: `POST /api/v1/logs/client` (anonymous, for frontend apps)
- Log search/query: `GET /api/v1/logs` (authenticated), exposed through the Gateway

Database: None (no SQL database) — logs are stored in Elasticsearch indices prefixed
`service-logs`; the originating `ServiceLogEvent.EventId` is the Elasticsearch document
`_id`, so a redelivered event overwrites rather than duplicates.

Published Events: None

Consumed Events:
- ServiceLogEvent (`system-logs-topic`, consumer group `logging-service-group`)

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
| ChatBotService | ChatBotDB | Owned by ChatBot; others access via API only |
| LoggingService | Elasticsearch `service-logs*` (no SQL DB) | Owned by Logging; others write via Kafka/`/logs/client`, read via API only |

## Multi-Tenant Isolation

Every `BaseEntity` row belongs to exactly one tenant. A service's DbContext must:
- implement `ITenantScopedDbContext` (`CurrentTenantId` from an injected
  `ITenantProvider`, registered with
  `services.AddTenantProvider(sp => sp.GetRequiredService<IUserContextService>().GetTenantId())`);
- call `modelBuilder.ApplyTenantQueryFilters(this)` LAST in `OnModelCreating` — never a
  constant-valued filter (EF caches the model per process, so a constant leaks the first
  request's tenant to everyone);
- call `ChangeTracker.EnforceTenantOnSave(CurrentTenantId)` first in `SaveChanges(Async)`.

Kafka/RabbitMQ consumers and background jobs have no user — wrap each message in
`using (TenantScope.Begin(tenantId))` with the tenant taken from the event (or derived
from data it references). With no tenant, queries return nothing and tenant-owned saves
throw (fail closed). `IgnoreQueryFilters()` only for deliberate cross-tenant lookups.

Rollout status (2026-10-20): IdentityService (own explicit tenant predicates), CRMService
and PurchaseOrderService (query filter; PO doesn't enforce on save yet) are isolated. All
other services still share data across tenants — see
`docs/business/Identity/SecurityReview-2026-10.md`.

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

### Outbox rules (every service's OutboxProcessor)

- **EventId must survive serialization.** `IntegrationEventBase.EventId`/`OccurredAt` are
  `init` properties so Newtonsoft/System.Text.Json restore the original values — never
  make them get-only again (a get-only property regenerated the Guid on every
  deserialize, which made EventId useless as a consumer dedup key). New event classes
  that don't derive from `IntegrationEventBase` must follow the same rule.
- **Resolve events with `ERP.Shared.Messaging.OutboxEventDeserializer.TryDeserialize`**,
  which uses the `$type` embedded in `Content` (writers serialize with
  `TypeNameHandling.All`). Don't resolve via `Type.GetType(message.Type)` — the `Type`
  column is inconsistent across services (bare `Name`, `FullName` or
  `AssemblyQualifiedName`); a bare name resolves to null, which used to silently drop
  every `SettingsChangedIntegrationEvent` in 9 services until 2026-10-19.
- **Poll in batches:** `.Take(OutboxEventDeserializer.DefaultBatchSize)`, and exclude
  dead-lettered rows (`Error` starting with `OutboxEventDeserializer.DeadLetterPrefix`).
- **Never set `ProcessedOnUtc` on a message that wasn't published.** Unrecoverable
  messages are dead-lettered (`MarkDeadLettered`, `LogError`) and stay in the table;
  clearing `Error` re-queues them. Publish failures keep `Error = ex.Message` and retry.
- Exception: IdentityService has its own `OutboxMessage` entity and publishes the raw
  `Content` string to `identity-topic` without deserializing (batch of 20).
- Unit tests for these rules: `tests/unit/ERP.Shared.UnitTests`.

## Documentation

Architecture:
docs/architecture

Business:
docs/business

Events:
docs/architecture/event-catalog.md

Conventions:
docs/conventions

Frontend:
docs/frontend (app stacks, i18n pattern, Admin Master Data CRUD pattern, Admin HRM
CRUD pattern — read before adding a new frontend page/entity so existing
conventions are reused, not re-derived)

## Claude Working Rules

When reviewing code:

1. Stay within the current service boundary.

2. Do not move business logic into Infrastructure.

3. Do not introduce cross-service database access.

4. Prefer existing Shared Contracts before creating new DTOs.

5. Reuse existing events before creating new integration events.

6. Follow repository structure.

7. Review business documentation before changing business logic.

8. Review `docs/architecture/event-catalog.md` before modifying messaging flows.

9. Before adding a new frontend page, entity, or translation, review
   `docs/frontend/` — reuse the existing i18n/Master Data CRUD patterns instead of
   creating a parallel mechanism.

## Priority Reading Order

1. **CLAUDE.md** (This file — service boundaries, database ownership, coding standards)
   → then **docs/PROJECT-STATUS.md** (current branch state, unfinished work, deployment
   prerequisites, prioritised backlog — read before starting any task; update it at the
   end of every session)
2. **docs/architecture/domain-map.md** (Domain groupings, service clusters, event flows by domain)
3. **docs/architecture/port-map.md** (Port assignments, network topology, service discovery)
4. **docs/architecture/service-map.md** (Visual service dependency graph, communication patterns)
5. **docs/architecture/event-catalog.md** (All 39+ integration events, publishers, consumers, C# definitions)
6. **docs/architecture/database-map.md** (14 database schemas, entities per database)
7. **docs/architecture/dependency-map.md** (Complete service-to-service dependency matrix)
8. **Service README.md** (e.g., ProductService/README.md — business logic, entities, events, endpoints)
9. **docs/business/{Domain}/*.md** (Domain business documentation — workflows, rules, examples)
10. **docs/frontend/*.md** (Frontend app stacks, i18n pattern, Admin Master Data CRUD pattern, Admin HRM CRUD pattern — read before touching `src/frontend/`)
11. **Source Code** (After understanding architecture and business domain)

