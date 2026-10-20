# Database Map: ERP E-Commerce Solution

**Purpose:** Document the service databases, their schemas, entities, and relationships. (Originally written for 14 databases; MasterDataDB and ChatBotDB were added later, LoggingService uses Elasticsearch only — see CLAUDE.md "Database Ownership" for the current list.)

---

## Tenant ownership (all databases, 2026-10-20)

- Every business table has a `TenantId` column and every service filters/stamps it through EF
  (CLAUDE.md "Multi-Tenant Isolation"; migrations `*_TenantIsolation*.sql` added the column
  where it was missing — e.g. Warehouse, Reporting facts, POS/Promotion caches,
  `PurchaseRequests`, Sales credit profiles/commissions).
- Business codes/numbers are unique per `(TenantId, …)`, never globally.
- `System*Settings` tables: one platform-wide row (`TenantId` = empty GUID).
- Shared reference data lives in the SYSTEM tenant (`00000000-0000-0000-0000-000000000001`)
  and is readable by every tenant: MasterData countries/provinces/districts/wards, languages,
  currencies, units of measure (+ translations); NotificationDB templates (tenant rows with
  the same code override them). `ReportingDB.DimDate` is global.
- Direct SQL / BI queries bypass the EF filter — always filter on `TenantId`.

---

## Database Summary

| # | Service | Database | Port | Purpose | Record Volume |
|---|---------|----------|------|---------|---|
| 1 | IdentityService | AuthDB | 1433 | User authentication & authorization | 100K-1M users |
| 2 | SaleOrderService | SalesDB | 1433 | Sales orders & customer data | 1M-10M orders |
| 3 | PurchaseOrderService | PurchasingDB | 1433 | Purchase orders & supplier data | 100K-1M POs |
| 4 | WareHouseService | WarehouseDB | 1433 | Inventory & stock movements | 10M-100M transactions |
| 5 | AccountingService | AccountingDB | 1433 | GL, AR, AP, invoices, bills | 100M-1B transactions |
| 6 | CRMService | CrmDb | 1433 | Contacts, leads, opportunities, cases | 100K-10M records |
| 7 | HRMService | HrmDb | 1433 | Attendance, leave, payroll, EmployeeSnapshots (read-only) | 10K-100K records |
| 8 | ProductService | ProductDB | 1433 | Products, variants, categories, pricing | 10K-1M products |
| 9 | PaymentService | PaymentDB | 1433 | Transactions, methods, refunds, settlements | 10M-100M transactions |
| 10 | ShippingService | ShippingDB | 1433 | Shipments, carriers, tracking, manifests | 1M-10M shipments |
| 11 | POSService | PosDB | 1433 | Registers, transactions, cash shifts | 100M-1B transactions |
| 12 | PromotionService | PromotionDB | 1433 | Promotions, coupons, loyalty, flash sales | 10K-100K records |
| 13 | NotificationService | NotificationDB | 1433 | Templates, notifications, preferences, history | 100M-1B records |
| 14 | ReportingService | ReportingDB | 1433 | Fact tables, dimension tables (data warehouse) | 1B+ denormalized records |
| 15 | ChatBotService | ChatBotDB | 1433 | Conversations, ConversationMessages only — no local copy of order/product/inventory data, always fetched live via the Gateway | 1M-10M records |
| 15 | SharedMasterDataService | MasterDataDB | 1433 | Departments, positions, employees, UoM, geography, languages | 10K-100K records |
| 16 | LoggingService | — (Elasticsearch, index `service-logs-*`) | 9200 | Information/Warning/Error/Exception log entries from backend services and frontend apps — no SQL Server database, deliberate exception to the one-SQL-DB-per-service convention | 1M-100M+ log entries (monthly rolling indices) |

**Total Storage (estimated):** 500GB-5TB across all services (varies by transaction volume)

---

## Key Database Rules

### Database Ownership

Each service owns its database exclusively. **No cross-database queries allowed.**

✅ **ALLOWED:**
- Service accesses its own database via ORM (EF Core)
- Other services access via REST API calls
- Event-driven data propagation via Kafka topics

❌ **NOT ALLOWED:**
- Direct SQL queries across databases
- Linked servers to other databases
- Shared database access between services

### Outbox Pattern (All 14 Databases)

Every database includes an `OutboxMessages` table for guaranteed event delivery:

```sql
CREATE TABLE OutboxMessages (
    Id BIGINT PRIMARY KEY IDENTITY(1,1),
    Type NVARCHAR(255) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    OccurredOnUtc DATETIME2 NOT NULL,
    ProcessedOnUtc DATETIME2 NULL,
    Error NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    INDEX IX_OutboxMessages_ProcessedOnUtc (ProcessedOnUtc) WHERE ProcessedOnUtc IS NULL
);
```

---

## Database Schemas

### 1. AuthDB (IdentityService, Port 5001)

**Primary Purpose:** User authentication, authorization, role-based access control (RBAC).

**Key Entities:**
- Users (100K-1M records)
- Roles (10-100 records)
- Permissions (100-1K records)
- UserRoles (many-to-many relationships)
- Tenants (multi-tenant support)

**Critical Tables:**

```
Users
├── Id (PK, GUID)
├── Email (Unique per (Email, TenantId) — not globally unique; changed 2026-08,
│         migration 014_ChangeEmailUniqueIndexToPerTenant.sql, see
│         docs/business/Identity/MultiTenantLogin.md)
├── PasswordHash
├── FirstName, LastName
├── TenantId (FK)
├── IsActive, IsDeleted
└── Audit (CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)

Roles
├── Id (PK)
├── Name
├── TenantId (FK)
└── Audit fields

UserRoles (Junction)
├── UserId (FK)
├── RoleId (FK)
└── TenantId (FK)

Permissions
├── Id (PK)
├── Name (e.g., "Sales.Orders.Create")
├── ResourceName
└── ActionName

RolePermissions (Junction)
├── RoleId (FK)
└── PermissionId (FK)

SystemAuthSettings (single well-known-GUID row — SuperAdmin master switch)
├── Id (PK, fixed GUID)
├── GoogleEnabled, MicrosoftEnabled, FacebookEnabled, TwitterEnabled (bit)
└── TwoFactorEnabled (bit)

TenantAuthSettings (one row per Tenant — requires SystemAuthSettings to also allow)
├── Id (PK)
├── TenantId (FK, unique)
├── GoogleEnabled, MicrosoftEnabled, FacebookEnabled, TwitterEnabled (bit)
├── TwoFactorEnabled (bit — available for this tenant)
└── TwoFactorRequired (bit — mandatory for every user in this tenant)

TwoFactorChallenges (one-shot OTP challenges, created at login when 2FA is required)
├── Id (PK)
├── UserId (FK), TenantId
├── CodeHash (hashed via the same IPasswordHasher used for login passwords)
├── Method ("Sms" | "Email"), Destination (masked)
├── ExpiredAt, AttemptCount, MaxAttempts, VerifiedAt
└── Index: (UserId, ExpiredAt)

OAuthStates (Twitter/X Authorization Code + PKCE flow only — short-lived)
├── Id (PK)
├── State (unique), CodeVerifier, TenantCode
└── ExpiredAt
```

Migration `002_AddExternalAuthSettingsAndTwoFactor.sql` also added `Users.PhoneNumber`,
`Users.TwoFactorEnabled`, `Users.TwoFactorMethod` and 4 new permissions
(`settings:system:view/manage`, `settings:tenant:view/manage`).

**Relationships:**
- Users → Tenants (many-to-one)
- Users ↔ Roles (many-to-many via UserRoles)
- Roles ↔ Permissions (many-to-many via RolePermissions)

**Published Events:**
- UserCreated
- UserUpdated
- RoleAssigned
- PermissionGranted

---

### 2. SalesDB (SaleOrderService, Port 5002)

**Primary Purpose:** Quotations, sales orders, order tracking, per-customer credit limit, sales commission.

> Corrected 2026-07-27 — there is no local `Customers` table; CustomerId/Name/
> Email are denormalized fields captured on SalesOrders/SalesQuotations at
> creation time (no live Customer entity exists anywhere in this system — see
> `docs/business/Sales/module-05-sales.md` reconciliation notes).

**Key Entities:**
- SalesOrders (1M-10M records), SalesOrderItems (5M-50M records)
- SalesQuotations, SalesQuotationItems
- CustomerSalesProfiles (one per customer with a configured credit limit)
- CommissionPlans, CommissionRecords
- OrderNumberSequences (per-tenant/prefix/year/month counter, not row-per-order)

**Critical Tables:**

```
SalesOrders
├── Id (PK)
├── OrderNo (Unique, "SO-yyyyMM-00001")
├── CustomerId, CustomerName, CustomerEmail (denormalized)
├── OrderSource (0=Manual, 1=Ecommerce, 2=POS), DepartmentId, SalesRepId, QuotationId (all nullable)
├── OrderDate
├── TotalAmount, PromotionId, DiscountAmount
├── Status (0=Draft, 1=Submitted, 2=Approved, 3=Picked, 4=Shipped, 5=Delivered,
│           6=Rejected, 7=Cancelled, 8=PartiallyShipped, 9=ReturnRequested, 10=Returned)
└── Audit fields

SalesOrderItems (owned collection)
├── Id (PK)
├── OrderId (FK)
├── ProductId (External FK → ProductService)
├── Quantity, UnitPrice (snapshot at order creation), ShippedQuantity
└── LineTotal (computed, not persisted)

SalesQuotations
├── Id (PK)
├── QuotationNo (Unique, "QT-yyyyMM-00001")
├── CustomerId, CustomerName, CustomerEmail (denormalized)
├── Status (0=Draft, 1=Sent, 2=Accepted, 3=Rejected, 4=Expired, 5=Converted)
├── ValidUntil, TotalAmount, ConvertedOrderId
└── SalesQuotationItems (owned collection — ProductId, ProductName, Quantity, UnitPrice)

CustomerSalesProfiles
├── Id (PK)
├── CustomerId (Unique) — this service is the source of truth, no external sync
├── CreditLimit, CurrentBalance (denormalized open-order exposure)
└── UpdatedAt

CommissionPlans (Id, Name, RateType, RateValue, IsActive)
CommissionRecords (Id, PlanId FK, SalesRepId, OrderId [Unique], Amount, Status, CalculatedAt)
```

**Relationships:**
- SalesOrderItems → SalesOrders (many-to-one, owned)
- SalesOrderItems → ProductService.Products (external, ID-only)
- SalesOrders.QuotationId → SalesQuotations (optional, set only when converted from a quotation)
- CommissionRecords → CommissionPlans (many-to-one), CommissionRecords.OrderId → SalesOrders (external-in-DB-terms — no FK constraint, same aggregate cluster)

**Published Events:**
- SalesOrderCreated
- SalesOrderApproved
- SalesOrderShipped

---

### 3. PurchasingDB (PurchaseOrderService, Port 5003)

**Primary Purpose:** Purchase orders, supplier relationships, purchasing workflows.

**Key Entities:**
- Suppliers (1K-10K records)
- PurchaseOrders (100K-1M records)
- PurchaseOrderItems (500K-5M records)
- PurchaseRequests (added 2026-07-27, Module 8 — suggestion-only, auto-created from
  WareHouseService's `StockAlertEvent`; see Critical Tables below)

**Critical Tables:**

```
Suppliers
├── Id (PK)
├── SupplierCode (Unique)
├── Name
├── Email, PhoneNumber
├── Address
├── IsActive, IsDeleted
└── CreatedAt

PurchaseOrders
├── Id (PK)
├── PoNumber (Unique)
├── SupplierId (FK)
├── OrderDate, ExpectedDeliveryDate
├── TotalAmount
├── Status (0=Draft, 1=Submitted, 2=Approved, 3=Receiving, 4=Closed)
└── Audit fields

PurchaseOrderItems
├── Id (PK)
├── PurchaseOrderId (FK)
├── ProductId (External FK → ProductService)
├── Quantity, UnitPrice
└── LineTotal

PurchaseRequests (added 2026-07-27, Module 8 — no TenantId/composite key, unlike every
other table above; the upstream StockAlertEvent signal carries no tenant concept)
├── Id (PK)
├── ProductId, ProductName, CurrentQuantity, ReorderPoint, SuggestedQuantity
├── Status (0=Suggested/1=Dismissed/2=Converted)
└── CreatedAt, ResolvedAt
```

**Published Events:**
- PurchaseOrderCreated
- PurchaseOrderApproved
- ReceiptScheduled

**Consumed Events (added 2026-07-27, Module 8):**
- `StockAlertEvent` (RabbitMQ `warehouse_exchange`, from WareHouseService) — first-ever
  inbound event consumer in this service; creates a `PurchaseRequest`

---

### 4. WarehouseDB (WareHouseService, Port 5004)

**Primary Purpose:** Inventory tracking (product-level AND bin-level), stock movements,
reservations, stock adjustment/stocktake approval, warehouse sites, barcode resolve.

> Corrected 2026-07-27 — the table shape below previously didn't match the real EF
> model at all (real PK is `ProductId` not a surrogate `Id`; `StockMovements` references
> `ProductId` directly, not an `InventoryId` FK; `Reservations` holds a list of items via
> an owned collection, not one row per product). This service also deliberately merges
> what a stricter design would split into "Warehouse" (movement) and "Inventory"
> (balance) bounded contexts — see `docs/business/Warehouse/module-07-warehouse.md`'s
> reconciliation notes.

**Key Entities:**
- Inventories (10K-100K SKUs, PK=ProductId, the cross-location rollup)
- InventoryLocationStocks (bin-level breakdown, additive alongside Inventories)
- StockMovements (10M-100M transactions, append-style log)
- Reservations / Reservation_ReservedItems (100K-1M active reservations — soft hold as of 2026-07-27, see below)
- InventoryLots (now with a `ReservedQuantity` soft-hold column, added 2026-07-27), InventorySerials (lot/expiry + serial tracking)
- CostConsumptionLogs (per-sale COGS audit trail, added 2026-07-27)
- InventoryValuationSnapshots / InventoryValuationSnapshotLines (period-end, immutable close, added 2026-07-27)
- TransferOrders / TransferOrderLines, Stocktakes / StocktakeLines
- StockAdjustments (approval-gated — see below)
- Warehouses (physical sites) / WarehouseLocations (zone/aisle/bin/shelf)
- BarcodeMappings

**Critical Tables:**

```
Inventories
├── ProductId (PK, External FK → ProductService)
├── ProductName, ProductSku
├── TotalQuantity, ReservedQuantity, LockedQuantity, AvailableQuantity (computed, persisted)
├── ReorderPoint, ReorderQuantity, Cost, ValuationMethod (0=FIFO/1=LIFO/2=Average)
├── LocationId (nullable, legacy single-location hint)
└── LastMovementDate, UpdatedAt

InventoryLocationStocks
├── Id (PK)
├── ProductId, LocationId (unique together)
├── Quantity, ReservedQuantity, LockedQuantity
└── UpdatedAt

StockMovements
├── MovementId (PK)
├── ProductId
├── Quantity, MovementType (0=Received/1=Sold/2=Adjusted/3=Damaged/4=Transferred)
├── FromLocation/ToLocation (free-text), FromLocationId/ToLocationId (unmapped in EF)
├── Reference, ReferenceId, Reason, RecordedBy
└── RecordedAt

Reservations
├── ReservationId (PK), OrderId
├── Status (0=Reserved/1=Fulfilled/2=Released/3=Expired)
├── HoldUntil, ReservedAt, ReleasedAt, ReleaseReason
└── Reservation_ReservedItems (owned collection: ProductId, Quantity, WarehouseLocation,
    LotAllocationsJson — replaced a single nullable LotId column, 2026-07-27; JSON list of
    every (LotId, Quantity) the line was allocated across, since a line can span >1 lot)

InventoryLots (ReservedQuantity column added 2026-07-27 — a soft hold; RemainingQuantity
is only touched at ship time now, not at reserve time)

CostConsumptionLogs (added 2026-07-27 — new; StockMovement never recorded a unit cost)
├── Id (PK), ProductId, LotId (nullable), Quantity, UnitCost, TotalCost
└── SourceOrderId, ConsumedAt

InventoryValuationSnapshots / InventoryValuationSnapshotLines (added 2026-07-27 — new)
├── Snapshot: Id (PK), PeriodCode, WarehouseId (nullable), SnapshotDate, IsClosed
│   (unique index on PeriodCode+WarehouseId enforces "closed once")
└── Line: Id (PK), SnapshotId (FK, cascade), ProductId, Quantity, UnitCost, TotalValue

StockAdjustments (added 2026-07-27 — did not exist before as an aggregate)
├── Id (PK)
├── ProductId, LocationId (nullable)
├── QuantityDelta, Reason (0=Damaged/1=Lost/2=Correction/3=Other), Notes
├── Status (0=Pending/1=Approved/2=Rejected)
└── RequestedBy/RequestedAt, ApprovedBy/ApprovedAt, RejectionReason

Warehouses (added 2026-07-27 — did not exist anywhere in this system before)
├── Id (PK), Code (unique), Name, Address, IsActive

WarehouseLocations
├── LocationId (PK)
├── WarehouseId (nullable FK → Warehouses, added 2026-07-27)
├── LocationCode, Zone, Aisle, Bin, Shelf, Capacity, CurrentOccupancy, IsActive
└── CreatedAt, UpdatedAt

BarcodeMappings (added 2026-07-27 — did not exist before)
├── Id (PK), Barcode (unique)
└── ProductId, LotId, SerialNumber
```

**Published Events:**
- InventoryReservedEvent, InventoryInsufficientEvent, InventoryUpdatedEvent
- ReceiptCompletedEvent, StockMovementRecordedEvent, StockAlertEvent
- StockAdjustmentApprovedEvent / StockAdjustmentRejectedEvent (added 2026-07-27)
- StocktakeCompletedEvent, TransferOrderCompletedEvent
- InventoryValuationSnapshotClosed (added 2026-07-27, Module 8 — no consumer yet)
- All staged via Outbox (added 2026-07-27) — see event-catalog.md for the retrofit note

**Fixed 2026-07-27 (Module 8 reconciliation):** `SalesOrderShippedEvent`'s handler
previously never reduced `Inventory.TotalQuantity` at all — normal order fulfillment
silently never decremented on-hand stock. Also fixed a related double-depletion bug
(reservation-time FEFO used to destructively deplete `InventoryLot.RemainingQuantity`
immediately; a separate manual "Sold" movement path could deplete the same lots again).
Reservation is now a genuine non-destructive soft hold; only ship-time consumption
touches `RemainingQuantity`. See `docs/business/Inventory/module-08-inventory.md`'s
reconciliation notes and the Warehouse Service README for full detail.

---

### 5. AccountingDB (AccountingService, Port 5005)

**Primary Purpose:** General ledger, accounts receivable, accounts payable, invoice processing.

**Key Entities:**
- GLAccounts (50-500 accounts)
- JournalEntries (100M-1B entries)
- Invoices (100M-1B records)
- Bills (10M-100M records)
- FiscalPeriods (periods for GL)

**Critical Tables:**

```
GLAccounts
├── Id (PK)
├── AccountCode (Unique, e.g., "1000", "2000", "4000")
├── Name
├── AccountType (1=Asset, 2=Liability, 3=Equity, 4=Revenue, 5=Expense)
├── DebitBalance, CreditBalance
├── IsActive
└── CreatedAt

JournalEntries
├── Id (PK)
├── EntryNumber (Unique)
├── EntryDate
├── Description
├── IsPosted
├── IsDeleted
└── Audit fields

JournalLines
├── Id (PK)
├── JournalEntryId (FK)
├── GLAccountId (FK)
├── Debit, Credit (one must be zero)
├── LineDescription
└── FK constraints ensure Debit XOR Credit

Invoices (AR)
├── Id (PK)
├── InvoiceNumber (Unique)
├── SalesOrderId (External FK → SalesService)
├── InvoiceDate, DueDate
├── GrossAmount, TaxAmount, NetAmount
├── AmountPaid
├── Status (0=Draft, 1=Issued, 2=PartiallyPaid, 3=Paid, 4=Overdue, 5=Cancelled)
└── CreatedAt

Bills (AP)
├── Id (PK)
├── BillNumber (Unique)
├── PurchaseOrderId (External FK → PurchasingService)
├── SupplierInvoiceNumber
├── BillDate, DueDate
├── GrossAmount, TaxAmount, NetAmount
├── AmountPaid
├── Status (0=Draft, 1=Received, 2=Approved, 3=PartiallyPaid, 4=Paid)
└── CreatedAt
```

**Published Events:**
- InvoiceCreated
- BillApproved
- JournalEntryPosted
- FiscalPeriodClosed

---

### 6. CrmDB (CRMService, Port 5006)

**Key Entities:**
- Contacts (100K-10M records)
- Leads (100K-5M records)
- Opportunities (10K-1M records)
- Cases (10K-1M records)
- Activities (1M-100M records)

**Published Events:**
- LeadCreated
- LeadQualified
- OpportunityWon
- CaseOpened
- CaseResolved

---

### 7. HrmDb (HRMService, Port 5007)

**Key Entities:**
- Employees (10K-100K records)
- Departments (10-1K records)
- LeaveRequests (100K-1M records)
- PaySlips (100K-10M records)
- Attendance (1M-100M records)

**Published Events:**
- EmployeeHired
- EmployeeTerminated
- PaySlipGenerated
- LeaveApproved

---

### 8. ProductDB (ProductService, Port 5008)

**Key Entities:**
- Products (10K-1M records) — includes `ExtendedDataJson` opaque escape-hatch column
- ProductVariants (50K-10M records) — includes structured `Length`/`Width`/`Height`/
  `DimensionUnit`/`WeightUnit` (first-class columns for shipping/logistics math;
  legacy free-text `Dimensions` kept for backward-compat read) + `ExtendedDataJson`
- Categories (100-10K records)
- ProductImages (100K-10M records)
- PriceLists (1K-100K records)
- ProductAttributes / ProductAttributeOptions / ProductAttributeValues —
  EAV-lite, category-scoped spec system (color, size, material, technical specs).
  See `docs/architecture/extensible-attributes-pattern.md` for the design and the
  reusable table shape for adopting this in other services.
- ProductExternalMappings — reconciles Product/Variant IDs against third-party
  systems (marketplace SKU, supplier item code, ERP integration key)

**Published Events:**
- ProductCreated
- ProductUpdated
- ProductPriceChanged
- ProductDiscontinued

---

### 9. PaymentDB (PaymentService, Port 5009)

**Key Entities:**
- PaymentTransactions (10M-100M records)
- PaymentMethods (100K-10M records)
- Refunds (1M-10M records)
- Settlements (100K-1M records)

**Published Events:**
- PaymentProcessed
- PaymentRefunded
- SettlementCreated
- PaymentFailed

---

### 10. ShippingDB (ShippingService, Port 5010)

**Key Entities:**
- Shipments (1M-10M records)
- Carriers (10-1K records)
- ShippingRates (1K-100K records)
- TrackingEvents (10M-100M records)

**Published Events:**
- ShipmentCreated
- TrackingUpdated
- DeliveryConfirmed

---

### 11. PosDB (POSService, Port 5011)

**Key Entities:**
- POSTerminals (1K-100K records)
- POSTransactions (100M-1B records)
- LineItems (500M-5B records)
- CashShifts (10K-1M records)

**Published Events:**
- TransactionCreated
- CashShiftClosed
- RegisterBalanced

---

### 12. PromotionDB (PromotionService, Port 5012)

**Key Entities:**
- Promotions (1K-100K records)
- Coupons (10K-10M records)
- FlashSales (1K-100K records)
- LoyaltyPrograms (100-10K records)

**Published Events:**
- PromotionCreated
- CouponRedeemed
- LoyaltyPointsEarned
- FlashSaleStarted

---

### 13. NotificationDB (NotificationService, Port 5013)

**Key Entities:**
- NotificationTemplates (100-1K records)
- Notifications (100M-1B records)
- NotificationPreferences (100K-10M records)
- NotificationChannels (5-10 records: Email, SMS, Push)

**Pattern:** Consumer-only service, stores notifications received from all 13 other services.

---

### 14. ReportingDB (ReportingService, Port 5014)

**Purpose:** Read-only data warehouse for analytics and BI.

**Key Entities (Fact Tables):**
- FactSalesOrders (denormalized sales data)
- FactPayments (payment transactions)
- FactInventoryMovements (inventory analytics)
- FactJournalEntries (GL analytics)

**Key Entities (Dimension Tables):**
- DimProducts (product attributes)
- DimCustomers (customer attributes)
- DimDates (calendar dimension)
- DimSuppliers (supplier attributes)

**Pattern:** Non-transactional, populated by consuming all Kafka topics from all 13 services. Optimized for OLAP queries (BI tools, Tableau, Power BI).

---

## Backup & Recovery Strategy

### Production Backup Schedule

| Backup Type | Frequency | Retention | Target RTO |
|------------|-----------|-----------|------------|
| Full Backup | Daily (00:00 UTC) | 30 days | < 4 hours |
| Differential | Every 6 hours | 7 days | < 30 mins |
| Transaction Log | Every 15 minutes | 24 hours | < 5 mins |

### Recovery Point Objective (RPO) by Service

| Service Criticality | RPO | Backup Frequency |
|-------------------|-----|------------------|
| CRITICAL (Identity, Accounting, Sales, Warehouse) | 15 minutes | Every 15 mins |
| HIGH (Payment, Purchasing, CRM, Shipping) | 1 hour | Every 1 hour |
| MEDIUM (Product, Promotion, HRM, POS) | 4 hours | Every 4 hours |
| LOW (Notification, Reporting) | 24 hours | Daily |

---

## Database Sizing Estimates (Annual Growth)

| Service | Initial | Year 1 | Year 3 | Year 5 |
|---------|---------|--------|--------|--------|
| AuthDB | 100 MB | 500 MB | 2 GB | 5 GB |
| SalesDB | 500 MB | 5 GB | 30 GB | 100 GB |
| WarehouseDB | 1 GB | 20 GB | 100 GB | 300 GB |
| AccountingDB | 500 MB | 10 GB | 50 GB | 150 GB |
| PaymentDB | 500 MB | 10 GB | 50 GB | 150 GB |
| ReportingDB | 1 GB | 30 GB | 200 GB | 500 GB |
| **Other 8 Services** | 2 GB | 20 GB | 100 GB | 300 GB |
| **TOTAL** | **6 GB** | **95 GB** | **530 GB** | **1.5 TB** |

---

## Performance Optimization

### Indexing Strategy

**Primary Key Indexes (on all tables):**
```sql
-- GUID Primary Keys
CREATE CLUSTERED INDEX PK_[TableName] ON [TableName](Id)

-- Unique constraint for natural key
CREATE UNIQUE INDEX UX_[TableName]_[NaturalKey] ON [TableName]([NaturalKey])

-- Foreign Keys
CREATE INDEX IX_[TableName]_[ForeignKeyField] ON [TableName]([ForeignKeyField])
```

**High-Volume Table Indexes:**
```sql
-- OutboxMessages: Filter by unprocessed
INDEX IX_OutboxMessages_ProcessedOnUtc (ProcessedOnUtc) WHERE ProcessedOnUtc IS NULL

-- StockMovements: Time-range queries
INDEX IX_StockMovements_CreatedAt (CreatedAt)

-- JournalLines: GL lookups
INDEX IX_JournalLines_GLAccountId (GLAccountId)

-- Invoices: Status filtering
INDEX IX_Invoices_Status (Status)
```

### Query Performance (P99 Targets)

| Query Type | Target Latency |
|-----------|-----------------|
| Single row by PK | < 5 ms |
| Index scan (1K rows) | < 50 ms |
| Full table scan (1M rows) | < 500 ms |
| Join (2 tables, 100K rows) | < 200 ms |
| Aggregate (1B rows) | < 5 seconds |

---

**Created:** 2026-06-13  
**Database Count:** 14  
**Total Estimated Tables:** 70+  
**Outbox Tables:** 14 (one per service)  
**Storage Estimate:** 6GB initial → 1.5TB Year 5
