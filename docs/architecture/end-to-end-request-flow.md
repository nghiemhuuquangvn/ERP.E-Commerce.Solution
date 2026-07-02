# End-to-End Request Flow: UI → Gateway → Service

> **Scope:** how a real user action in one of the four frontends (Admin React SPA,
> Storefront Vue SPA, Customer mobile app, Staff mobile app) travels through the
> Ocelot API Gateway into a microservice's Controller → MediatR Command/Query →
> Domain → persistence, and which integration events fire as a result, verified
> against the actual code (file paths and route strings below are real, not
> illustrative). Business-level summaries of these same flows (without the UI/wire
> detail) are in [`docs/business/ERP-Business-Analysis.md`](../business/ERP-Business-Analysis.md#4-luồng-business-end-to-end).
>
> **Gaps found while tracing these flows are called out explicitly** where a UI
> button, page, or wiring doesn't exist yet — this document describes what is
> actually implemented today, 2026-07-02, not the target design.

## Table of Contents

1. [The general request shape](#1-the-general-request-shape)
2. [Identity & Auth (login, 2FA, external login)](#2-identity--auth-login-2fa-external-login)
3. [E-Commerce Checkout (Storefront / Customer mobile app)](#3-e-commerce-checkout-storefront--customer-mobile-app)
4. [POS Retail Sale (Staff mobile app)](#4-pos-retail-sale-staff-mobile-app)
5. [Procurement (Purchase Order lifecycle)](#5-procurement-purchase-order-lifecycle)
6. [CRM → Sales (Lead to Cash)](#6-crm--sales-lead-to-cash)
7. [HR & Payroll](#7-hr--payroll)
8. [Product Catalog & Search (Admin CRUD + Elasticsearch)](#8-product-catalog--search-admin-crud--elasticsearch)
9. [Master Data (Admin CRUD pattern)](#9-master-data-admin-crud-pattern)

---

## 1. The General Request Shape

### 1.1 Every synchronous request follows this path

```
UI (React/Vue/RN) → axios/fetch client → Ocelot Gateway (:5000) → downstream service (:500x)
  → ASP.NET Controller action → MediatR ISender.Send(Command|Query)
  → Application handler (validates via FluentValidation, loads Aggregate via Repository)
  → Domain method (business rule, raises IDomainEvent via AggregateRoot.AddDomainEvent)
  → DbContext.SaveChangesAsync() override (harvests DomainEvents → writes OutboxMessages row
     in the SAME transaction as the business data)
  → 200/201 response bubbles back up to the UI
```

The domain event's actual delivery to *other* services never happens inline in that
request — it happens later, asynchronously, via one of two mechanisms (below). This
is why, e.g., a `POST /orders` call returns to the customer immediately even though
inventory reservation, payment, and shipping haven't happened yet.

### 1.2 Gateway routing (`src/Gateway/Ocelot.API/ocelot.json`)

22 routes, hybrid shape:
- **Curated, versioned routes** for the primary "public-facing" endpoints, e.g.
  `/api/v1/products/{everything}` (GET-only, no auth — public catalog browsing),
  `/api/v1/orders/{everything}` (Bearer required) → `salesorderservice:5002`,
  `/api/v1/auth/{everything}` (no auth — login/register/2FA can't require a token
  before one exists) → `identityservice:5001`, `/api/v1/pos/{everything}` →
  `posservice:5011`, `/api/v1/payments/...`, `/api/v1/shipments/...`,
  `/api/v1/reports/...` (GET-only), plus one single-purpose route
  `/api/v1/promotions/coupons/validate` (POST-only) → `promotionservice:5012`.
- **Generic catch-all prefix routes** for every service, `/{code}/api/{everything}`
  (all verbs) → `/api/{everything}` downstream, e.g. `/id/api/...`, `/so/api/...`,
  `/po/api/...`, `/wh/api/...`, `/acc/api/...`, `/crm/api/...`, `/hrm/api/...`,
  `/mst/api/...`, `/prd/api/...`, `/pay/api/...`, `/shp/api/...`, `/pos/api/...`,
  `/prm/api/...`, `/ntf/api/...`, `/rpt/api/...`. Services with **no** curated route
  at all (PurchaseOrder, Accounting, CRM, HRM, MasterData) are reached **only**
  through this generic prefix.

Every route except `/api/v1/auth/*` and the GET-only public product/report routes
requires `AuthenticationProviderKey: "Bearer"` + `RouteClaimsRequirement: {"tenant_id": ".*"}`.

**JWT handling** (`src/Gateway/Ocelot.API/Program.cs`): `AddJwtBearer` validates
against `IdentityServer:Authority` but with `ValidateAudience/Issuer/IssuerSigningKey
= false` — i.e. the gateway trusts any token shaped correctly rather than
cryptographically re-verifying it against Identity's signing key. `GatewayAuthorizationMiddleware`
then extracts claims (`sub`, `tenant_id`, roles → computed permissions via
`RoleDefinitions.GetPermissionsForRoles`) and injects them as
`X-User-Id`/`X-Tenant-Id`/`X-User-Roles`/`X-User-Permissions` headers onto the
downstream request — **this is how every downstream Controller gets the caller's
identity**, not from the JWT itself (services never validate the token; they trust
these headers, which only the gateway can set since services aren't
publicly reachable outside the Docker network). No rate limiting is configured
anywhere in `ocelot.json` today.

### 1.3 The two async event-delivery mechanisms

| | SaleOrderService & WareHouseService | Every other service |
|---|---|---|
| Transport | RabbitMQ, direct publish | Kafka, via Outbox pattern |
| Exchange/topic | `sales_order_events` (topic exchange) / `warehouse_exchange` | `{service}-topic` (e.g. `payment-topic`) |
| Routing key / key | `sales_order.{EventTypeShortName}` e.g. `sales_order.SalesOrderCreatedEvent` / `warehouse.{EventTypeShortName}` | n/a (Kafka topic is the whole address) |
| Serialization | Plain `System.Text.Json`, **no type discriminator** | Newtonsoft `JsonConvert.SerializeObject(ev, TypeNameHandling.All)` — includes a `$type` field |
| Producer code | `RabbitMqMessageBus` / `EventPublisher`, called directly from the command handler after `SaveChangesAsync` | `OutboxProcessor : BackgroundService`, polls `OutboxMessages` table every 5s, drains to `KafkaMessageBus` |
| Consumer shape | `IHostedService` + `RabbitMQ.Client` `IModel`/`AsyncEventingBasicConsumer`, `BasicConsume(autoAck:false)`, manual `JsonSerializer.Deserialize<T>` or field-by-field `JObject` mapping | `IHostedService` + `Confluent.Kafka` `IConsumer<string,string>`, poll loop, dispatch by `JObject.Parse(json)["$type"]` substring match |

A consumer **must** match whichever mechanism its source service actually uses — this
was the single most common bug found and fixed across the codebase (see
`ERP-Business-Analysis.md` §6). Below, every flow step notes which mechanism carries
it.

---

## 2. Identity & Auth (login, 2FA, external login)

**Frontends involved:** Admin (`src/frontend/admin/src/pages/auth/`), Storefront
(`src/frontend/storefront/src/pages/auth/`), both mobile apps
(`app/(auth)/login.tsx`). **Backend:** IdentityService only.

### 2.1 Standard email/password login

| Step | UI | Call | Gateway route | Controller → Handler |
|---|---|---|---|---|
| 1 | User submits `LoginPage` (Admin: `LoginPage.tsx`; Storefront: `auth/LoginPage.vue`; mobile: `(auth)/login.tsx`) | `POST /api/v1/auth/login` `{ email, password, tenantCode }` (Admin also sends `userType: 2`) | `/api/v1/auth/{everything}` → identityservice:5001, **no auth required** | `AuthController.Login` (`api/[controller]` → `api/Auth/login`) → `LoginCommand` |
| 2 | — | — | — | Handler validates credentials, checks `TenantAuthSettings`/`SystemAuthSettings` — if 2FA is required for this tenant+user, returns `{ requiresTwoFactor: true, challengeId, twoFactorMethod }` **instead of** a token pair |
| 3a (no 2FA) | Store gets `{ accessToken, refreshToken, user }` | — | — | Admin: Zustand `persist` → `localStorage` key `erp-auth`. Storefront: Pinia `persist` → `localStorage`. Mobile: Zustand + `AsyncStorage` key `erp-customer-auth` (no `localStorage` on RN) |
| 3b (2FA required) | Admin only routes to `/login/2fa` (`TwoFactorPage.tsx`) — **Storefront and both mobile apps have no 2FA UI step today** | `POST /api/v1/auth/2fa/verify {challengeId, code}` | same | `AuthController.VerifyTwoFactor` → `VerifyTwoFactorCommand` |

**Async side-effect of 2FA (Kafka, Outbox pattern):** when the login handler decides
2FA is required, it raises `TwoFactorCodeGenerated`, harvested by `AuthDbContext.SaveChangesAsync`
into `OutboxMessages`, drained to Kafka topic `identity-topic` by Identity's
`OutboxProcessor`. **NotificationService's `IdentityEventConsumer`** (the oldest, most
mature consumer in the system — the pattern every other service's new consumer this
session was built to mirror) picks it up, inspects `Method` (SMS vs Email), and sends
the OTP through `TwilioSmsSender`/an email sender — **never a direct synchronous
call** from Identity to Notification, per `CLAUDE.md`.

### 2.2 External login (Google/Microsoft/Facebook — verifiable-token flow)

`POST /api/v1/auth/external-login {provider, idToken}` → `AuthController.ExternalLogin`
→ resolves the matching `IExternalAuthProvider` (`GoogleAuthProvider`/
`MicrosoftAuthProvider`/`FacebookAuthProvider`) which calls the provider's own
token-verification endpoint synchronously (this one **is** a real outbound HTTP call,
since it's to an external IdP, not another internal service) → on success, same
login/2FA branching as 2.1.

### 2.3 External login (Twitter/X — OAuth 2.0 Authorization Code + PKCE)

Twitter has no single-shot verifiable token like the others, so it's a real redirect
flow: `GET /api/v1/auth/external-login/twitter/authorize-url` (returns the Twitter
authorize URL + PKCE `code_verifier` state) → browser redirects to Twitter → Twitter
redirects back to the SPA with a `code` → `POST
/api/v1/auth/external-login/twitter/callback {code, codeVerifier}` →
`TwitterOAuthService` exchanges the code for Twitter's own tokens, fetches the
profile, then proceeds through the same login/2FA branch.

### 2.4 Settings (SuperAdmin / tenant admin only)

`SettingsController` (`api/Settings/system` `GET/PUT`, `api/Settings/tenant/{tenantId}`
`GET/PUT`) — two-tier gate: `SystemAuthSettings` (SuperAdmin-only, all tenants) must
have a provider/2FA switch **on** before a `TenantAuthSettings` row for the same
switch has any effect. No dedicated Admin UI page found for this controller today
(Admin's `pages/settings/` exists but a page wired to `SettingsController` wasn't
found in the earlier code survey) — currently reachable only via direct API call /
Swagger.

---

## 3. E-Commerce Checkout (Storefront / Customer mobile app)

### 3.1 Browse & search

- Storefront: `ProductListPage.vue` → `productsApi.list({search,page,pageSize,sortBy})`
  → `GET /api/v1/products?...` (gateway route is GET-only + no-auth) →
  `ProductsController.Search` (`ERP.PRD.API`) — this is the **basic** SQL-backed
  search (`GetProductsQuery`), not the Elasticsearch one (see §8.3) which lives at a
  different, uncalled-from-UI route (`/api/products/search/advanced`).
- Customer mobile app: `(tabs)/search.tsx` and `(tabs)/index.tsx` call the same
  `/v1/products` endpoint through the gateway (`http://localhost:5000/api` base).

### 3.2 Add to cart — **client-side only, no backend call**

Storefront `ProductDetailPage.vue`'s `addToCart()` → Pinia `cartStore.addItem()`
(persisted to `localStorage`, `{ persist: true }`). Customer mobile app's cart tab
(`(tabs)/cart.tsx`) is the same client-only pattern. **There is no cart API/service in
this system** — the cart is reconciled into a real order only at checkout submission.
`promotionsApi.validateCoupon()` (`POST /v1/promotions/coupons/validate`) exists in
`src/api/promotions.api.ts` and has a dedicated gateway route, but **no page calls
it** — the coupon-at-cart-time feature is wired at the API-client layer only, not the
UI, on the storefront today.

### 3.3 Checkout submission → order creation

| Step | UI | Call |
|---|---|---|
| Storefront | `CheckoutPage.vue` (3-step wizard: Shipping → Payment → Review), "Place Order" | `POST /api/v1/orders {customerId, shippingAddress, lines, couponCode?}` |
| Customer mobile | `(tabs)/cart.tsx`'s `checkout()` — inline from the cart tab, no separate wizard screen | same endpoint, same body shape |

→ `/api/v1/orders/{everything}` (Bearer required) → `salesorderservice:5002` →
`SalesOrdersController.Create` → `CreateOrderCommand` → validates line prices against
ProductService (synchronous call — allowed, since read-only cross-service lookups via
API are fine; only cross-*database* access is forbidden), applies coupon via
PromotionService if `couponCode` present, persists `SalesOrder` aggregate,
`Order.Create()` raises `SalesOrderCreatedEvent`.

**Publish: RabbitMQ**, exchange `sales_order_events`, routing key
`sales_order.SalesOrderCreatedEvent`. On success, Storefront routes to
`/order-confirmation/:id` and clears the cart.

### 3.4 Saga (choreography, not a central orchestrator today)

Each of the following is a **separate RabbitMQ/Kafka consumer in its own service**,
reacting independently — there is no live orchestrating saga (`OrderFulfillmentSaga`
exists in `SaleOrderService` but is dead code, never registered in DI, and its state
entity isn't in the DbContext model; deciding whether to wire it as true
orchestration or formally remove it in favor of this choreography is an open,
deferred item — see `ERP-Business-Analysis.md` §6).

1. **Inventory reservation** — `WareHouseService.SalesOrderRabbitMqConsumer` (queue
   `warehouse-service.sales-order-events`, bound to
   `sales_order.SalesOrderCreatedEvent`) deserializes the real event
   (`OrderId, CustomerId, LineItems[{ProductId,Quantity,UnitPrice}], TotalAmount,
   Status`) and calls the existing `ReserveInventoryCommand` handler per line →
   `POST-equivalent` internal call → `Inventory.Reserve()` → raises
   `InventoryReservedEvent` (published via the same RabbitMQ mechanism, exchange
   `warehouse_exchange`, routing key `warehouse.InventoryReservedEvent`).
2. **Payment** — `PaymentService.SalesOrderRabbitMqConsumer` consumes
   `sales_order.SalesOrderCreatedEvent` directly (not waiting for inventory reservation
   today — this is a known simplification, not a strict saga step ordering) and
   auto-initiates a `PaymentRequest`. Customer's actual card/e-wallet capture still
   requires a follow-up `POST /api/v1/payments/{id}/capture` call through
   VNPay's real gateway integration; the VNPay IPN webhook (HMAC-verified) calls back
   into `PaymentsController`, which raises `PaymentProcessedEvent` (Outbox+Kafka,
   `payment-topic`).
3. **Shipping** — `ShippingService.SalesOrderRabbitMqConsumer` consumes
   `sales_order.SalesOrderApprovedEvent` (note: *Approved*, not *Created* — shipping
   only starts once the order is approved) and creates a `Shipment` via the real GHN
   carrier integration; separately, `ShippingService.WarehouseRabbitMqConsumer`
   consumes `warehouse.InventoryReservedEvent` from `warehouse_exchange` and just logs
   receipt today (intentionally minimal — a Shipment may not exist yet when this
   arrives, so there's nothing meaningful to update).
4. **Notifications** — NotificationService is designed to consume all of the above
   (per `CLAUDE.md`), but **only `identity-topic` has a live consumer today**
   (`IdentityEventConsumer`); the sales/payment/shipping notification handler classes
   exist and are DI-registered but nothing calls them yet — a documented, deferred gap.
5. **Post-delivery** — CRMService's `SalesOrderRabbitMqConsumer` and
   `CrmKafkaConsumer` (`payment-topic`) link the order to a Contact and update
   purchase history; AccountingService's equivalent consumers create the AR invoice
   and record the payment receipt.

---

## 4. POS Retail Sale (Staff mobile app)

**Frontend:** `src/frontend/mobile/staff/app/(tabs)/pos.tsx`, backed by
`stores/pos.store.ts`. **Note a real gap found while tracing this**: the backend has
a full cash-shift lifecycle (`CashShiftsController`: `POST /open`, `POST
{id}/close`, `GET active/{terminalId}`, `GET {id}/summary`) but **the staff app has
no shift screen at all** — a repo-wide search for "shift" in the staff app's source
found nothing. The POS screen only drives the transaction endpoints; opening/closing
a cash drawer isn't reachable from the UI today.

| Step | Call | Gateway route | Controller → Handler |
|---|---|---|---|
| Open transaction | `POST /v1/pos/transactions/open {terminalId, cashierId}` | `/api/v1/pos/{everything}` → posservice:5011 | `TransactionsController.Open` → `OpenTransactionCommand` → `Transaction.Open()` |
| Scan/add item (per line) | `POST /v1/pos/transactions/{id}/items` | same | `TransactionsController.AddItem` → `AddTransactionItemCommand` |
| Apply discount | *(no call site found in `pos.tsx` today — `ApplyDiscountCommand`/`POST {id}/discount` exists but isn't invoked from the staff app UI)* | | `ApplyDiscountCommand` |
| Complete sale | `POST /v1/pos/transactions/{id}/complete` | same | `TransactionsController.Complete` → `CompleteTransactionCommand` → `Transaction.Complete()` → raises `TransactionCreatedEvent` |

**No producer wired for `TransactionCreatedEvent` today** — POSService has no
Outbox+Kafka producer (confirmed: `⚠️ chưa có Outbox publish` in
`ERP-Business-Analysis.md`'s completion table) — so downstream effects described in
the business-flow doc (real-time stock deduction, revenue posting, loyalty points)
don't actually fire from a completed POS sale yet; POS only **consumes** events
(`product-topic`, `promotion-topic` via `PosKafkaConsumer`, added this session to sync
its local product/promotion cache tables), it doesn't **publish** any.

Inventory (`inventory.tsx` → `GET /v1/inventory`, `POST /v1/inventory/adjust`) and
deliveries (`deliveries.tsx` → `GET /v1/shipments`, `POST
/v1/shipments/{id}/tracking`) are separate staff-app tabs calling WarehouseService
and ShippingService directly through the gateway, unrelated to the POS transaction
flow above.

---

## 5. Procurement (Purchase Order lifecycle)

**No Admin UI exists for this today** — a repo-wide case-insensitive search for
"purchase" across the Admin app's source returned zero matches. The flow below is
reachable via direct API call / Swagger only, through the gateway's generic prefix
route (PurchaseOrderService has **no curated `/api/v1/...` route**, only
`/po/api/{everything}`).

| Step | Call | Controller → Handler | Event |
|---|---|---|---|
| Create PO | `POST /po/api/purchase-orders` | `PurchaseOrdersController.Create` → `CreatePurchaseOrderCommand` | `PurchaseOrderCreatedDomainEvent` |
| Add/remove line items | `POST .../{id}/items`, `DELETE .../{id}/items/{itemId}` | | |
| Submit for approval | `POST .../{id}/submit` | | |
| Approve | `POST .../{id}/approve` | `PurchaseOrderApprovedDomainEvent` | |
| Cancel | `POST .../{id}/cancel` | | |

**Publish mechanism: Outbox+Kafka, topic `purchasing-topic`** — this was built from
scratch this session; PurchaseOrderService previously raised these domain events into
the void (no producer existed at all).

**Consumers today:** AccountingService's `AccountingKafkaConsumer` subscribes
`purchasing-topic` and creates an AP bill on `PurchaseOrderCreatedDomainEvent`
(manually mapping `PurchaseOrderId, PONumber, SupplierId` — the real event has no
`TotalAmount` field, so the bill amount defaults to 0 until goods are actually
received and priced); WarehouseService's Kafka consumer reacts to
`PurchaseOrderApprovedDomainEvent` to prepare for receiving. **The actual GRN
(`ReceiveGoodsCommand`) that would post real received quantities/costs doesn't exist
yet** — deferred (see `ERP-Business-Analysis.md` §6/§7); today the signal arrives at
Warehouse but there's no command to act on it.

---

## 6. CRM → Sales (Lead to Cash)

No dedicated CRM Admin UI page was found in this session's frontend survey either
(the Admin app's page list — dashboard/orders/products/inventory/payments/reports/
monitoring/settings/auth/masterdata — has no `crm` folder) — reachable via
`/crm/api/{everything}` only.

1. `POST /crm/api/leads` → `CreateLeadCommand` → `LeadCreatedEvent` (Outbox+Kafka,
   `crm-topic` — assign to salesperson, notification not yet live per §3.4).
2. `POST /crm/api/leads/{id}/qualify` → `Lead.Qualify()` → `LeadQualifiedEvent`,
   `Opportunity` created at pipeline stage "Prospecting".
3. Stage transitions culminate in `POST /crm/api/opportunities/{id}/won` →
   `OpportunityWonEvent`.
4. **Order creation from a won opportunity is not automatic** — no consumer in
   SaleOrderService reacts to `OpportunityWonEvent` (SaleOrderService doesn't consume
   anything; it only publishes, per its row in `ERP-Business-Analysis.md`'s
   completion table) — a salesperson must manually create the order today.
5. **Post-sale linkage (this session's new wiring):** CRM's
   `SalesOrderRabbitMqConsumer` consumes `sales_order.SalesOrderCreatedEvent` and
   links the order to its Contact; CRM's `CrmKafkaConsumer` (`payment-topic`) updates
   customer lifetime value on `PaymentProcessedEvent`.

---

## 7. HR & Payroll

No dedicated Admin UI found either — reachable via `/hrm/api/{everything}` and
`/mst/api/{everything}` (Employee/Department master data moved to
SharedMasterDataService; HRM keeps a read-only `EmployeeSnapshot` synced via Kafka, no
synchronous cross-service call for it per `CLAUDE.md`).

1. **Hire** happens in SharedMasterDataService, not HRM: `POST /mst/api/employees` →
   `EmployeesController.Create` → `EmployeeHiredEvent` (Outbox+Kafka, `mst-topic`).
   IdentityService is designed to auto-create a user account from this, and HRMService
   consumes it into its local `EmployeeSnapshot` via `MasterDataEventConsumer` — the
   first real, working Kafka consumer in the whole system, and the template every
   consumer built this session mirrors.
2. **Attendance / leave**: `POST /hrm/api/attendance`,
   `POST /hrm/api/leave-requests`, `POST /hrm/api/leave-requests/{id}/approve` →
   `LeaveApprovedEvent` → SharedMasterDataService consumes it (`LeaveApproved` sets
   the canonical Employee to `OnLeave`).
3. **Payroll**: `POST /hrm/api/payroll/process?month=...` → `ProcessPayrollCommand`
   → `PaySlipGeneratedEvent` per employee (Outbox+Kafka, `hrm-topic` — Notification's
   consumer for this topic is not live yet, same §3.4 caveat).

---

## 8. Product Catalog & Search (Admin CRUD + Elasticsearch)

**Frontend gap found**: Admin's `pages/products/ProductListPage.tsx` is the **only**
product page — its "Add Product" button renders (gated by
`hasPermission('products:create')`) but has **no `onClick` handler wired**, and there
is no create/edit page or route registered in `router/index.tsx`. **No
attributes/variants UI exists in Admin at all.** Everything below except §8.1's list
view is reachable via Swagger/direct API call only today.

### 8.1 List/browse (the one real Admin page)

`ProductListPage.tsx` → `productsApi.list/get/create/update/discontinue`
(`src/api/products.api.ts`) → `/api/v1/products/{everything}` (GET-only, no-auth
route) or `/prd/api/{everything}` for the write operations → `ProductsController`.

### 8.2 Attributes / variants (EAV pattern, API-only today)

- `POST /prd/api/product-attributes` → `AttributesController.CreateAttribute` —
  defines a reusable attribute (e.g. "Color", data type enum from
  `AttributeDataType`).
- `POST /prd/api/product-attributes/{id}/options` — for enum-like attributes, define
  allowed values (e.g. Red/Blue/Green).
- `PUT /prd/api/products/{productId}/attributes/{attributeId}` — assign a value to a
  specific product (`ProductAttributeValue`).
- `PUT /prd/api/products/{productId}/variants/{variantId}/attributes/{attributeId}` —
  assign a value at the variant level (e.g. this specific SKU is "Color=Red").
- `VariantsController` (`POST/GET/PUT/DELETE`, plus `GET sku/{sku}`) manages the
  `ProductVariant` entities (SKU, price, dimensions) each product owns.

### 8.3 Search — two independent paths, only one is Elasticsearch-backed

| | Basic search | Advanced/ES search |
|---|---|---|
| Route | `GET /api/v1/products?search=...` (public, called by Storefront) | `GET /prd/api/products/search/advanced` (**no UI calls this today**) |
| Controller | `ProductsController.Search` | `ProductSearchController.SearchAdvanced` |
| Backend | Plain SQL `GetProductsQuery` against `ProductDbContext` | `ElasticsearchProductSearchService` against the `products` ES index |

**How the ES index stays in sync (not Kafka — a same-database queue table):**
`ProductDbContext.SaveChangesAsync` harvests `ProductSearchIndexRequestedDomainEvent`
instances (raised whenever a product/variant/attribute changes) and upserts
deduplicated rows into a local `SearchIndexQueue` SQL table (dedup key: `ProductId`).
`SearchIndexProcessor : BackgroundService` polls that table every 5s, and for each
unindexed row calls `IProductSearchIndexer.IndexAsync(productId)`
(`ElasticsearchProductIndexer`), which rebuilds the full derived ES document
(`ProductSearchDocument`) from SQL and writes it via the ES client — SQL remains the
system of record, ES is a rebuildable read-optimized projection, same
eventual-consistency shape as the Outbox pattern but scoped within one service/DB
rather than crossing a service boundary. `POST
/prd/api/products/search/advanced/reindex-all` exists to force a full rebuild.

**Known, separate gap** (already flagged in `ERP-Business-Analysis.md` §6):
ProductService never raises `ProductUpdated`/`ProductPriceChanged` as *cross-service*
integration events (only the internal `ProductSearchIndexRequestedDomainEvent` above)
— so POSService's and PromotionService's product-price caches, wired to
`product-topic` this session, only ever see the initial `ProductCreatedDomainEvent`
and default price to 0 thereafter; a price change made in Product never propagates to
POS/Promotion until this event is added.

---

## 9. Master Data (Admin CRUD Pattern)

The one domain with a fully real Admin UI end-to-end. Pattern documented in
`docs/frontend/masterdata-admin-guide.md` and confirmed against real files: pages live
in `src/frontend/admin/src/pages/masterdata/`, sharing `mstClient.ts`'s
`MST_BASE = ${GATEWAY_ROOT}/mst/api/v1/masterdata` (a different base path than the
rest of the Admin app's `/api/v1/...` convention — `mstClient.ts` re-derives
`GATEWAY_ROOT` by stripping `/api` off `VITE_API_URL`).

Two CRUD shapes coexist, chosen per entity based on its actual lifecycle:

| Entity | Page | Shape | Why |
|---|---|---|---|
| Department | `DepartmentsPage.tsx` | Single generic edit modal | Simple flat entity, one edit operation covers every field |
| Position | `PositionsController` | Generic edit + explicit `activate`/`deactivate` | Lifecycle has a binary status separate from field edits |
| Employee | `EmployeesPage.tsx` | Four distinct modals: `HireModal`, `UpdateProfileModal`, `TransferModal`, `TerminateModal` | Each maps to a distinct backend command/endpoint (`POST /employees`, `PUT /{id}/profile`, `POST /{id}/transfer`, `POST /{id}/terminate`) with different business rules — a single generic "edit" would blur meaningfully different operations |
| Geography (Country/Province/District/Ward) | `GeographyPage.tsx` | Create-only modals per level | Geographic hierarchy rows are treated as append-only reference data in this system — no edit endpoints exist on `GeographyController` |
| UnitOfMeasure | `UnitsOfMeasureController` | Generic `GET/POST/PUT` | Simple flat entity |

**Rule for adding a new master-data entity** (per `CLAUDE.md`'s instruction to reuse
this pattern rather than re-deriving it): pick the CRUD shape based on how many
distinct real-world operations the entity actually has, not by default reaching for
"generic edit" — Employee is the reference example for a multi-operation entity,
Department for a single-operation one.

**Example trace (Department create):** `DepartmentsPage.tsx` submit → `POST
/mst/api/v1/masterdata/departments {name, parentDepartmentId?}` → gateway
`/mst/api/{everything}` → `DepartmentsController.Create` → `CreateDepartmentCommand` →
`Department` aggregate → `DepartmentCreatedEvent` (Outbox+Kafka, `mst-topic`) — no
known consumer for this specific event today (HRM only consumes
Employee/Leave-related events from this topic).
