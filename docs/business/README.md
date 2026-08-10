# Business Domains Index

> Renamed from `BUSINESS_DOMAINS_INDEX.md` during the 2026-07-27 docs
> cleanup to serve as this folder's index (matching `docs/frontend/
> README.md`'s role). On 2026-07-29, a Senior BA documentation pass added
> full `module-0X-*.md` business-process docs for the 5 services that
> previously had none (Product, CRM, Payment, Shipping, Reporting) and
> expanded 2 that were too narrow (Promotion, Notification) — see the
> updated per-domain entries below. A companion database review,
> `docs/architecture/database-performance-indexing-review.md`, covers B-Tree
> indexing correctness across all 16 databases. A generated `.docx` set of
> all of the above lives in `docs/business/docx/`.

**Reference:** All business domain specifications are documented in the comprehensive README.md files for each service. This index serves as a navigation guide.

---

## Business Documentation by Domain

### ERP Core (Back-Office)

#### Identity & Access Management
- **Location:** `src/services/IdentityService/README.md`,
  `docs/business/Identity/ExternalAuthAndTwoFactor.md`,
  `docs/business/Identity/License.md`,
  `docs/business/Identity/MultiTenantLogin.md` (2026-08 — subdomain/header-based
  tenant resolution, replacing the manual Tenant Code login field)
- **Topics:** User management, role-based access control (RBAC), permission
  management, multi-tenancy, external login/SSO (Google/Microsoft/Facebook/Twitter),
  two-factor authentication (SMS/Email), system/tenant auth settings, per-tenant
  module licensing (which of the 14 backend modules a tenant is entitled to use)
- **Workflows:** User registration → Role assignment → Permission grant → Access
  control; Login → (2FA challenge if required) → tokens issued; External login →
  find-or-create user → tokens issued; License issue/renew/suspend/revoke →
  `licensed_module` JWT claims → per-service `LicenseEnforcementMiddleware` check
- **Entities:** User, Role, Permission, Tenant, UserRole, RolePermission,
  ExternalLogin, SystemAuthSettings, TenantAuthSettings, TwoFactorChallenge,
  OAuthState, ModuleDefinition, LicensePlan, TenantLicense

#### Accounting & Financial Management
- **Location:** `src/services/AccountingService/README.md`,
  `docs/frontend/accounting-admin-guide.md` (Admin UI)
- **Topics:** General ledger (GL), accounts receivable (AR), accounts payable (AP), invoicing, journal entries
- **Workflows:** 
  - Order-to-Invoice: SalesOrder → Invoice → JournalEntry → GLPosting
  - Bill-to-Payment: PurchaseOrder → Bill → JournalEntry → GLPosting
- **Entities:** Account, JournalEntry, Invoice, Bill, FiscalPeriod, GLTransaction
- **Admin UI (2026-07-21):** full business-data CRUD added — Chart of Accounts,
  Journal Entries, Invoices, Bills, Fiscal Periods, Tax Rates, Bank Accounts,
  read-only Reports. Fixed a pre-existing gap in the same pass: none of the 7
  business controllers' `accounting:{entity}:view` permissions had ever been
  defined in `ERP.Shared.Authorization.PermissionConstants`/`RoleDefinitions` —
  every GET endpoint was unconditionally Forbidden for every role, including
  SuperAdmin, until this was added.

#### Human Resources & Payroll
- **Location:** `src/services/HRMService/README.md`
- **Topics:** Attendance tracking, leave management, payroll processing, pay slip generation (Employee/Department master data now lives in SharedMasterDataService — see below)
- **Workflows:** Attendance tracking → Leave approval → Payslip generation
- **Entities:** LeaveRequest, PaySlip, Attendance, Shift, EmployeeSnapshot (read-only projection)
- **Security fix (2026-07-21):** every `hrm:*` permission (attendance, leave,
  shifts, payroll, employees) was checked in code but never defined — every
  HRM endpoint was Forbidden for every non-SuperAdmin role. Fixed; Admin gets
  full access, Manager/Viewer get view-only (attendance/employees/shifts).

#### Shared Master Data
- **Location:** `docs/business/MasterData/`, `src/services/SharedMasterDataService/README.md`
- **Topics:** Department & position (job title) master data, employee master data, unit of measure, geographic hierarchy (Country → Province → District → Ward), supported languages, per-entity translations
- **Workflows:** Employee hire → Department assignment → (HRMService) Attendance/Leave/Payroll referencing EmployeeId
- **Entities:** Department, Position, Employee, UnitOfMeasure, Country, Province, District, Ward, SupportedLanguage, {Entity}Translation
- **Security fix (2026-07-21):** same class of bug as HRM above — every
  `masterdata:*` permission was checked but never defined; fixed the same way
  (Admin full, Manager/Viewer view-only).

#### Purchase Order & Procurement
- **Location:** `src/services/PurchaseOrderService/README.md`
- **Topics:** Purchase order creation, supplier management, purchase workflows, receiving, approval processes
- **Workflows:** PurchaseOrder creation → Approval → Supplier confirmation → Receiving → Closing
- **Entities:** PurchaseOrder, Supplier, SupplierContact, PurchaseOrderItem, ReceiptSchedule

---

### Supply Chain & Product Management

#### Product Catalog & Pricing
- **Location:** `src/services/ProductService/README.md`,
  `docs/business/Product/module-09-product.md` (added 2026-07-29 — full Senior
  BA reconciliation: domain model, DB design, the Cost-vs-Price naming
  confusion that leaves `ProductDto.Price` hardcoded to 0 on standard read
  endpoints, and inconsistent per-resource authorization)
- **Topics:** Product catalog management, product variants (SKUs), categories, pricing, images, lifecycle management
- **Workflows:** ProductCreation → Variant definition → Category assignment → Price setting → Publication → Discontinuation
- **Entities:** Product, ProductVariant, Category, ProductImage, PriceList, ProductAttribute

#### Warehouse & Inventory
- **Location:** `src/services/WareHouseService/README.md`
- **Topics:** Inventory tracking, stock movements, reservation management, warehouse locations, reorder points
- **Workflows:** ReceiveGoods → StockMovement → Reservation → Issue → Adjustment
- **Entities:** Inventory, StockMovement, Reservation, WarehouseLocation, InventoryBalance
- **Security fix (2026-07-21):** Stocktakes, TransferOrders, Serials, Lots,
  Locations, and StockMovement controllers had zero auth check at all
  (reachable by anyone, unauthenticated). Fixed by reusing the existing
  `inventory:view`/`inventory:manage` permission pair uniformly across all six
  — this service has no finer per-resource permission convention, by design.

#### Shipping & Logistics
- **Location:** `src/services/ShippingService/README.md`,
  `docs/business/Shipping/module-13-shipping.md` (added 2026-07-29 — real GHN
  carrier integration, POD fields, independent Return aggregate, and the
  since-fixed `ShipmentTrackingEvents.Status` INT→NVARCHAR migration bug)
- **Topics:** Shipment management, carrier integration, shipping rate calculation, tracking, delivery confirmation
- **Workflows:** CreateShipment → AssignCarrier → GenerateLabel → TrackingUpdates → DeliveryConfirmation
- **Entities:** Shipment, Carrier, ShippingRate, TrackingEvent, Manifest, DeliveryProof
- **Security fix (2026-07-21):** Shipments, ReturnShipments, and Carriers
  controllers had unprotected actions (including write actions like
  AddTracking/MarkPickedUp/Cancel). Added two new permissions —
  `shipping:view` and `shipping:carriers:manage` — since this service
  previously only had `Create`/`Update`, no view/manage granularity.

---

### E-Commerce & Sales

#### Sales Order & Customer Orders
- **Location:** `src/services/SaleOrderService/README.md`
- **Topics:** Sales order creation, customer order processing, order workflows, fulfillment tracking, order status management
- **Workflows:** OrderCreation → Approval → InventoryReservation → Fulfillment → Shipment → Delivery
- **Entities:** SalesOrder, Customer, OrderItem, OrderStatus, OrderLine, OrderShipment
- **Security fix (2026-07-21):** `GetMyOrders` had no auth check (low
  severity — verified the query already scoped correctly to the caller's own
  `UserId`, no data leak existed, just a missing permission gate). Added
  `sales_order:read`.

#### Customer Relationship Management (CRM)
- **Location:** `src/services/CRMService/README.md`,
  `docs/business/CRM/module-10-crm.md` (added 2026-07-29 — Lead-qualification
  IS the Lead→Opportunity conversion (no separate step), no Contact dedup rule,
  and a confirmed cross-tenant data-isolation gap: no repository filters by
  TenantId anywhere in this service)
- **Topics:** Contact management, lead tracking, opportunity management, case management, activity logging, customer interactions
- **Workflows:** LeadCreation → LeadQualification → OpportunityCreation → OpportunityClosure → SalesOrder
- **Entities:** Contact, Lead, Opportunity, Case, Activity, InteractionLog
- **Security fix (2026-07-21):** every `crm:*` permission (contacts, leads,
  opportunities, cases, activities) was checked in code but never defined —
  every CRM endpoint was Forbidden for every non-SuperAdmin role. Fixed; Admin
  gets full access, Manager/Viewer get view-only.

#### Payment Processing & Reconciliation
- **Location:** `src/services/PaymentService/README.md`,
  `docs/business/Payment/module-11-payment.md` (added 2026-07-29 — real VNPay
  gateway integration confirmed, partial-refund support, OrderId-uniqueness
  idempotency guard against double-charging, and a confirmed discrepancy:
  `SettlementCreated` event does not exist anywhere in code despite CLAUDE.md
  listing it)
- **Topics:** Payment transaction processing, payment method management, refund handling, settlement reconciliation, payment authorization
- **Workflows:** PaymentInitiation → Authorization → Capture → Settlement → Reconciliation
- **Entities:** PaymentTransaction, PaymentMethod, Refund, Settlement, PaymentGatewayResponse, Reconciliation
- **Security fix (2026-07-21):** Installments and PaymentMethods controllers
  (including a card-delete action) had zero auth check; fixed by reusing
  `payments:view`/`payments:create`/`payments:capture`. Confirmed the VNPay
  `ipn` webhook already has real checksum verification (`VerifyCallbackAsync`)
  before processing — marked `[AllowAnonymous]` with a comment explaining why
  (external server-to-server callback, cannot carry a user JWT), no gap found.

#### Promotions & Loyalty
- **Location:** `src/services/PromotionService/README.md`,
  `docs/business/Promotion/LoyaltyAndCouponSecurity.md`,
  `docs/business/Promotion/module-20-promotion.md` (added 2026-07-29 — full
  lifecycle: Promotion discount calculation, Coupon generation/redemption,
  Flash Sale reservation, Loyalty tier/points math, all reconciled against
  real domain code)
- **Topics:** Promotion management, coupon redemption, flash sales, loyalty program management, discount rules, eligibility
- **Workflows:** PromotionCreation → PromotionActivation → CouponRedeem → LoyaltyPointsEarn → LoyaltyRedemption
- **Entities:** Promotion, Coupon, FlashSale, LoyaltyProgram, LoyaltyAccount, DiscountRule
- **Security fix (2026-07-21) — most severe finding of the platform-wide audit:**
  `LoyaltyController.Earn`/`Redeem` accepted a caller-supplied `userId` with no
  ownership check at all — anyone could mint or spend any other user's loyalty
  points. Verified no frontend app calls these endpoints today, so locked them
  to staff-only (`promotions:manage`) rather than adding a self-service
  ownership check that nothing currently needs — see
  `docs/business/Promotion/LoyaltyAndCouponSecurity.md` for the full model,
  including the public/authenticated split on coupon validate-vs-redeem.

---

### Retail (Point-of-Sale)

#### Point-of-Sale (POS) & Retail Operations
- **Location:** `src/services/POSService/README.md`
- **Topics:** POS terminal management, retail transaction processing, line item management, cash shift management, register reconciliation
- **Workflows:** TerminalSetup → CashShiftOpen → TransactionCreate → PaymentProcess → CashShiftClose → RegisterReconciliation
- **Entities:** POSTerminal, POSTransaction, LineItem, CashShift, Register, PaymentTender
- **Security fix (2026-07-21):** barcode lookup and terminal list actions had
  no auth check. Barcode lookup now requires `pos:transactions` (same gate as
  its sibling cashier actions); terminal list requires `pos:terminals:manage`.
  `TerminalsController.CustomerDisplay` (the customer-facing screen mirroring
  a terminal's current transaction) was deliberately left `[AllowAnonymous]`
  with a comment — it's driven by in-store hardware with no staff login.

---

### Cross-Cutting Concerns

#### Platform Settings
- **Location:** `docs/business/Settings/SettingsOverview.md`,
  `docs/frontend/settings-admin-guide.md`
- **Topics:** 22 System/Tenant two-tier Settings categories distributed across 9
  services by owning domain (General/Company/Auth&Security/Workflow/Localization/
  File Storage/Scheduler/Integration/API/Feature Flags/Audit/Advanced in
  IdentityService; Notification/Email/SMS in NotificationService; Store-Product in
  ProductService; Cart-Checkout/Order in SaleOrderService; Inventory in
  WarehouseService; Payment in PaymentService; Shipping in ShippingService; Tax in
  AccountingService; Currency in SharedMasterDataService). Storage+CRUD, plus
  read-through caching and cross-service change notification via
  `SettingsChangedIntegrationEvent` (Phase 4) — see the overview doc's Known
  Limitations section for what is still not wired to *business* runtime behavior.
- **Workflows:** SuperAdmin sets a System-tier ceiling → Tenant Admin configures
  their own Tenant-tier row → capability effective only when both agree
  (`System.XEnabled && Tenant.XEnabled`)
- **Entities:** `System{X}Settings`/`Tenant{X}Settings` per category (22 pairs, one
  per category), plus `SystemFeatureFlag`/`TenantFeatureFlagOverride` (Feature Flags'
  dynamic key-value shape)

#### Notification & Communication
- **Location:** `src/services/NotificationService/README.md`,
  `docs/business/Notification/NotificationPrivacy.md`,
  `docs/business/Notification/module-21-notification.md` (added 2026-07-29 —
  full lifecycle including retry/backoff and opt-out rules; corrects
  CLAUDE.md's stale claim of "only 1 live consumer" — a second,
  `SalesOrderRabbitMqConsumer`, is also live)
- **Topics:** Multi-channel notifications (email, SMS, push), notification templates, preference management, notification history
- **Workflows:** EventOccurs → TemplateSelect → NotificationCompose → ChannelSend → DeliveryTracking
- **Entities:** NotificationTemplate, Notification, NotificationPreference, NotificationChannel, DeliveryLog
- **Security fix (2026-07-21):** `PreferencesController` and
  `NotificationsController.GetById`/`GetHistory` had no auth check and no
  ownership check — any caller could read any other recipient's notification
  history/preferences (an IDOR). Fixed with a new `notifications:view`
  permission plus a mandatory identity comparison (caller's own ID must match
  the target recipient) unless the caller holds the elevated `notifications:send`
  permission. See `docs/business/Notification/NotificationPrivacy.md`.

#### AI ChatBot
- **Location:** `src/services/ChatBotService/README.md` (service summary),
  `docs/business/ChatBot/README.md` (full technical reference: architecture, provider
  selection, tool-calling mechanism, SignalR streaming, CRM escalation, API reference,
  and step-by-step docker-compose + Kubernetes deployment)
- **Topics:** AI chat assistant for Storefront customers (incl. anonymous guests) and
  Admin staff, answering via live tool-calling against other services' REST APIs
  (never a local data copy), Claude-vs-ChatGPT provider selection reusing the existing
  License/LicensePlan mechanism (no new billing), CRM case escalation
- **Workflows:** StartConversation → SendMessage (streamed reply, looping through
  tool-calls as needed) → optional EscalateToHuman (opens a CRM Case)
- **Entities:** Conversation, Message (owned collection)
- **Added 2026-08-08:** greenfield addition — CRMService gained one small additive
  endpoint (`GET /api/v1/crm/contacts/by-user/{userId}`) to support escalation, and the
  shared `AddErpJwtAuthentication` gained a `/hubs`-path-only query-string JWT fallback
  for SignalR. See `docs/business/ChatBot/README.md` §7 and §6 for why.

#### Reporting & Analytics
- **Location:** `src/services/ReportingService/README.md`,
  `docs/business/Reporting/module-19-reporting.md` (added 2026-07-29 —
  confirms a live indexing bug: the `DateKey` column actually used by all
  fact-table writes has never had a physical index, due to an index-name
  collision in migration 002 with the older, unused `OrderDateKey`/
  `PaymentDateKey` columns' index from migration 001; also corrects
  CLAUDE.md's "all events from all services" claim to the real 6-of-14
  domains actually consumed)
- **Topics:** Analytics and BI, dashboard population, KPI calculations, data warehouse management, BI tool integration
- **Workflows:** EventStream → DataWarehousePopulation → FactTableUpdate → DimensionTableSync → BIQueryExecution
- **Entities:** FactSalesOrder, FactPayment, DimProduct, DimCustomer, DimDate, Metric

---

## Event Flows by Business Process

### Order-to-Cash Cycle (E-Commerce)

```
Customer Places Order
  ↓ SalesOrderCreated
Warehouse (Inventory Reservation)
  ↓ InventoryReserved
Accounting (Invoice Creation)
  ↓ InvoiceCreated
CRM (Opportunity Management)
  ↓ OpportunityTracked
Payment (Payment Processing)
  ↓ PaymentProcessed
Accounting (Receipt Posting)
  ↓ JournalEntryPosted
Shipping (Shipment Creation)
  ↓ ShipmentCreated
Warehouse (Inventory Decrement)
  ↓ StockMoved
Notification (Customer Email)
  ↓ NotificationSent
Reporting (Sale Recorded)
  ↓ FactSalesOrderPopulated
```

### Procure-to-Pay Cycle (Supply Chain)

```
Procurement Team Creates PO
  ↓ PurchaseOrderCreated
Warehouse (Receiving Preparation)
  ↓ ReceiptScheduled
Warehouse (Goods Receipt)
  ↓ InventoryUpdated
Accounting (Bill Recording)
  ↓ BillCreated
Accounting (GL Posting)
  ↓ JournalEntryPosted
Payment (Payment Processing)
  ↓ PaymentProcessed
Accounting (Payment Posting)
  ↓ JournalEntryPosted
Reporting (Expense Recorded)
  ↓ FactPaymentPopulated
```

### Retail Transaction Cycle (POS)

```
Customer at Retail Store
  ↓ POSTransactionCreated
Product Service (Lookup SKU & Price)
  ↓ ProductFound
Promotion Service (Coupon Validation)
  ↓ DiscountApplied
Payment Service (Process Payment)
  ↓ PaymentProcessed
Warehouse (Inventory Decrement)
  ↓ StockMoved
Accounting (Sale Recording)
  ↓ JournalEntryPosted
Notification (Receipt Sent)
  ↓ NotificationSent
Reporting (Transaction Recorded)
  ↓ FactPOSTransactionPopulated
```

### User Provisioning Cycle (Identity)

```
New User Registration
  ↓ UserCreated
HRM (Employee Record Creation)
  ↓ EmployeeCreated
CRM (Contact Record Creation)
  ↓ ContactCreated
Sales (Salesperson Registration)
  ↓ SalesPersonRegistered
Accounting (GL User Creation)
  ↓ GLUserCreated
Notification (Welcome Email)
  ↓ NotificationSent
```

---

## Key Business Rules & Constraints

### Data Integrity Rules

1. **Inventory Consistency:** Quantity Available = Quantity On Hand - Quantity Reserved
2. **GL Balancing:** Sum of All Debits = Sum of All Credits (per journal entry)
3. **Invoice Uniqueness:** Each SalesOrder maps to max 1 Invoice
4. **Bill Uniqueness:** Each PurchaseOrder maps to max 1 Bill
5. **Reservation Expiry:** Reservations auto-release after 24 hours if not confirmed

### Business Process Rules

1. **SalesOrder Approval:** Must have inventory available before approval
2. **PurchaseOrder Approval:** Requires manager sign-off
3. **Payment Processing:** Requires valid payment method on file
4. **Payroll Processing:** Cannot run during open fiscal period
5. **Promotion Validity:** Promotions must have start date < end date
6. **Leave Request:** Cannot overlap with existing approved leaves

### Security & Authorization Rules

1. **Cross-Tenant Isolation:** No user can access data from other tenants
2. **Role-Based Access:** API endpoints protected by role-based authorization
3. **Service Isolation:** Services cannot directly access other services' databases
4. **Event Filtering:** NotificationService respects user notification preferences
5. **Audit Trail:** All GL entries must be traceable to source transaction
6. **Notification privacy (clarified 2026-07-21):** a caller can only read their
   own notification history/preferences; cross-recipient access requires the
   elevated `notifications:send` permission (Admin only) — see
   `docs/business/Notification/NotificationPrivacy.md`.
7. **Loyalty points are staff-administered only (clarified 2026-07-21):** there
   is no customer self-service loyalty earn/redeem flow today — an authenticated
   staff member (`promotions:manage`) acts on a customer's behalf. Coupon
   *validation* is public/pre-login (read-only, checkout-facing); coupon
   *redemption* requires the customer to be logged in but needs no staff
   permission (it's a real state change, but a customer's own action). See
   `docs/business/Promotion/LoyaltyAndCouponSecurity.md`.
8. **RBAC vocabulary completeness (2026-07-21):** a platform-wide audit found
   CRMService, HRMService, and SharedMasterDataService's permission strings
   (checked in every controller via `[AuthorizePermission]`) had never been
   defined in `ERP.Shared.Authorization.PermissionConstants`/`RoleDefinitions` —
   every endpoint in all three services was unconditionally Forbidden for every
   role except SuperAdmin. Fixed alongside the same-class Accounting gap found
   earlier the same day. See "Domain notes" under each affected domain below.

---

## References

**Service Documentation:**
- See individual service README.md files for detailed API endpoints (200+ total)
- See docs/architecture/event-catalog.md for complete integration event definitions
- See docs/architecture/service-map.md for service dependencies
- See docs/architecture/domain-map.md for domain groupings

**Architecture Documentation:**
- docs/architecture/port-map.md — Service ports and network topology
- docs/architecture/database-map.md — Database schemas and relationships
- docs/architecture/dependency-map.md — Complete service dependencies
- docs/architecture/integration-map.md — Event flow patterns

---

**Created:** 2026-06-13  
**Status:** Phase 2 Architecture Complete | Phase 2 Business Documentation Complete (via README.md) | Ready for Phase 3 Implementation

**Next:** Phase 3 - Business Logic Implementation (Controllers, Handlers, Repositories, Domain Logic)
