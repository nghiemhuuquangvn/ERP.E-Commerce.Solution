# Service Dependency Map

> **Note (added during the 2026-07-27 docs cleanup):** this document is a
> design-level reference (dated 2026-06-13, before `SharedMasterDataService`
> existed, so counts/tables below say "14 services") — treat it the same way
> as `integration-map.md`'s own caveat: some event/method names here may drift
> from what's actually wired in code today. For code-accurate service-to-service
> flows, prefer the per-module business docs under `docs/business/`.

**Purpose:** Visual and tabular reference of all service-to-service dependencies across the microservices.

---

## Dependency Matrix

**Legend:**
- ✅ Synchronous REST call (request-response)
- 📧 Asynchronous Kafka event subscription
- 🚫 No dependency

### Complete Matrix

```
                    |Id |SO |PO |WH |ACC|CRM|HRM|PRD|PAY|SHP|POS|PRM|NTF|RPT
                    |---|---|---|---|---|----|---|---|---|---|---|---|---|---
 Identity Service   | - |✅ |✅ |✅ |✅ |✅ |✅ | 🚫|🚫| 🚫|🚫| 🚫|📧|📧
 Sales Order Service| 🚫 | - |🚫|📧|📧|✅ |🚫|✅ |📧|🚫|🚫|✅ |📧|📧
 Purchase Service   |✅ |🚫| - |📧|📧|🚫|🚫| 🚫|🚫|🚫|🚫|🚫|📧|📧
 Warehouse Service  |✅ |📧|📧| - |📧|🚫|🚫|✅ |🚫|📧|📧|🚫|📧|📧
 Accounting Service |🚫 |📧|📧|📧| - |🚫|🚫|🚫|📧|🚫|📧|🚫|📧|📧
 CRM Service        |✅ |📧|🚫|🚫|🚫| - |🚫|🚫|📧|🚫|🚫|🚫|📧|📧
 HRM Service        |✅ |🚫|🚫|🚫|🚫|🚫| - |🚫|🚫|🚫|🚫|🚫|📧|📧
 Product Service    |🚫 |🚫|🚫|✅ |🚫|🚫|🚫| - |🚫|🚫|✅ |✅ |📧|📧
 Payment Service    |🚫 |📧|🚫|🚫|📧|🚫|🚫|🚫| - |🚫|📧|🚫|📧|📧
 Shipping Service   |🚫 |📧|🚫|📧|🚫|🚫|🚫|🚫|🚫| - |🚫|🚫|📧|📧
 POS Service        |🚫 |🚫|🚫|📧|📧|🚫|🚫|✅ |✅ |🚫| - |✅ |📧|📧
 Promotion Service  |🚫 |📧|🚫|🚫|🚫|🚫|🚫|✅ |🚫|🚫|📧| - |📧|📧
 Notification Svc   |🚫 |🚫|🚫|🚫|🚫|🚫|🚫|🚫|🚫|🚫|🚫|🚫| - |🚫
 Reporting Svc      |🚫 |🚫|🚫|🚫|🚫|🚫|🚫|🚫|🚫|🚫|🚫|🚫|🚫| - 
```

---

## Detailed Dependency Tables

### 1. Identity Service (Port 5001)

| Depends On | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| — | — | — | — | **No upstream dependencies** |

| Called By | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Sales | REST | ValidateUser, GetUserRoles | — | User/role validation before order |
| Purchasing | REST | ValidateUser, GetUserRoles | — | Buyer validation |
| Warehouse | REST | ValidateUser | — | Permission check |
| Accounting | REST | ValidateUser | — | GL user validation |
| Accounting | REST (service token, `/internal`) | `GET internal/v1/exchange-rates/effective` on SharedMasterData | — | Exchange-rate suggestion and period-end revaluation (P1-4) |
| CRM | REST | ValidateUser | — | Contact/user sync |
| HRM | REST | ValidateUser | — | Employee user creation |
| Notification | — | — | UserCreated, UserUpdated, RoleAssigned | Email/SMS templates |
| Reporting | — | — | UserCreated | User dimension table |

**Criticality:** CRITICAL — All services depend on Identity authentication

---

### 2. Sales Order Service (Port 5002)

| Depends On | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Identity | REST | ValidateUser | — | User/role check |
| Product | REST | GetProduct, GetProductBySku, CheckAvailability | — | Price lookup, stock check |
| Promotion | REST | ValidateCoupon, GetDiscountRules | — | Apply discount codes |
| Warehouse | EVENT | — | InventoryReserved | Confirm inventory allocated |
| Accounting | EVENT | — | InvoiceCreated | Invoice to post |

| Called By | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| POS | REST | GetOrderHistory | — | Customer order lookup |
| Promotion | EVENT | — | SalesOrderCreated | Apply promotion tracking |
| Warehouse | EVENT | — | SalesOrderCreated | Trigger reservation |
| Warehouse | EVENT | — | SalesOrderApproved | Trigger fulfillment |
| Accounting | EVENT | — | SalesOrderCreated, SalesOrderApproved | Create/finalize invoice |
| CRM | EVENT | — | SalesOrderCreated | Track opportunity |
| Payment | EVENT | — | SalesOrderCreated | Invoice to payment |
| Shipping | EVENT | — | SalesOrderApproved | Create shipment |
| Notification | EVENT | — | SalesOrderCreated, SalesOrderDelivered | Order confirmation, delivery email |
| Reporting | EVENT | — | SalesOrderCreated, SalesOrderDelivered | Sales fact table |

**Criticality:** CRITICAL — Core revenue stream

---

### 3. Warehouse Service (Port 5004)

| Depends On | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Identity | REST | ValidateUser | — | Warehouse staff validation |
| Product | REST | GetProduct, GetProductPrice | — | Product info lookup |
| Warehouse (internal) | — | — | StockMovedEvent, InventoryAdjustedEvent | Inventory syncs |

| Called By | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Sales | REST | CheckInventoryAvailable | InventoryReservedEvent | Confirm stock allocated |
| Purchasing | EVENT | — | PurchaseOrderCreated | Prepare receipt |
| Accounting | EVENT | — | StockMoved, InventoryAdjusted | GL inventory valuation |
| Shipping | REST | GetReservedInventory, ReleaseForShipping | — | Fulfillment |
| POS | EVENT | — | POSTransactionCreated | Decrement retail sales |
| Notification | EVENT | — | InventoryReserved, StockMoved | Alerts |
| Reporting | EVENT | — | StockMoved, InventoryAdjusted | Inventory analytics |

**Criticality:** CRITICAL — Blocks order fulfillment if down

---

### 4. Accounting Service (Port 5005)

| Depends On | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Sales | EVENT | — | SalesOrderCreated, SalesOrderApproved | Invoice creation |
| Purchasing | EVENT | — | PurchaseOrderCreated, GoodsReceivedEvent | Bill creation |
| Warehouse | EVENT | — | StockMoved, InventoryAdjusted | COGS tracking |
| Payment | EVENT | — | PaymentProcessed | Cash receipt posting |

| Called By | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| POS | EVENT | — | POSTransactionCreated, CashShiftClosed | Daily sales GL posting |
| HRM | EVENT | — | PaySlipGenerated | Salary expense |
| Notification | EVENT | — | InvoiceCreated, BillApproved, JournalEntryPosted | Email notifications |
| Reporting | EVENT | — | JournalEntryPosted, FiscalPeriodClosed | GL fact tables |

**Criticality:** HIGH — Financial data integrity critical

---

### 5. Product Service (Port 5008)

| Depends On | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| — | — | — | — | **No dependencies** |

| Called By | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Sales | REST | GetProduct, GetProductBySku, CheckAvailability | — | Product lookup for order |
| Warehouse | REST | GetProduct, GetProductPrice | — | Product info for inventory |
| POS | REST | GetProduct, GetProductBySku, GetPrice | ProductPublishedEvent | POS catalog sync |
| Promotion | REST | GetProduct, CheckEligibility | ProductPriceChangedEvent | Discount rule validation |
| Notification | EVENT | — | ProductCreated, ProductPublished, ProductDiscontinued | Catalog notifications |
| Reporting | EVENT | — | ProductCreated, ProductPriceChanged | Product dimension table |

**Criticality:** CRITICAL — Upstream source of product catalog

---

### 6. Payment Service (Port 5009)

| Depends On | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Sales | EVENT | — | SalesOrderCreated | Invoice to collect |
| Accounting | EVENT | — | InvoiceCreated, BillApproved | Payment confirmation posting |

| Called By | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| POS | REST | ProcessPayment | PaymentProcessedEvent | Retail payment |
| Accounting | EVENT | — | PaymentProcessed, PaymentRefunded (since 2026-10-24, confirmed refunds only) | GL posting |
| Notification | EVENT | — | PaymentProcessed, PaymentFailed, PaymentRefunded (since P1-6) | Receipts and refund / failure messages |
| Reporting | EVENT | — | PaymentProcessed | Payment fact table |

**Criticality:** HIGH — Revenue realization

---

### 7. CRM Service (Port 5006)

| Depends On | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Identity | REST | GetUser, GetUserRoles | — | Contact user sync |
| Sales | EVENT | — | SalesOrderCreated, SalesOrderDelivered | Opportunity tracking |
| Payment | EVENT | — | PaymentProcessed | Payment status update |

| Called By | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Sales | REST | GetCustomerHistory, CheckCreditLimit | — | Order validation |
| Notification | EVENT | — | LeadCreated, LeadQualified, OpportunityWon, CaseResolved | Customer communications |
| Reporting | EVENT | — | LeadCreated, OpportunityWon | Sales pipeline metrics |

**Criticality:** MEDIUM — Nice-to-have but not critical

---

### 8. HRM Service (Port 5007)

| Depends On | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Identity | REST | GetUser, GetUserRoles | — | Employee user creation |

| Called By | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Accounting | EVENT | — | EmployeeHired, PaySlipGenerated | Payroll GL entries |
| Notification | EVENT | — | EmployeeHired, PaySlipGenerated, LeaveApproved | HR notifications |
| Reporting | EVENT | — | EmployeeHired | Employee metrics |

**Criticality:** LOW-MEDIUM — Back-office only

---

### 9. Promotion Service (Port 5012)

| Depends On | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Product | REST | GetProduct, CheckEligibility | ProductPriceChangedEvent | Product validation for promos |
| Sales | EVENT | — | SalesOrderCreated | Track promotion usage |

| Called By | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Sales | REST | ValidateCoupon, GetDiscountRules, ApplyDiscount | — | Discount application |
| POS | REST | ValidateCoupon, ApplyDiscount | — | Retail discounts |
| Notification | EVENT | — | PromotionCreated, CouponRedeemed | Promo emails |
| Reporting | EVENT | — | CouponRedeemed, LoyaltyPointsEarned | Promotion analytics |

**Criticality:** MEDIUM — Revenue impact but survivable as read-only fallback

---

### 10. Shipping Service (Port 5010)

| Depends On | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Sales | EVENT | — | SalesOrderApproved | Order to ship |
| Warehouse | REST | ReleaseInventory, GetPickingList | — | Inventory allocation |

| Called By | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Warehouse | REST | CreateShipment, UpdateTracking | ShipmentCreatedEvent, TrackingUpdatedEvent | Fulfillment |
| Accounting | EVENT | — | DeliveryConfirmedEvent | Recognize revenue |
| Notification | EVENT | — | ShipmentCreated, TrackingUpdated, DeliveryConfirmed | Tracking emails |
| Reporting | EVENT | — | ShipmentCreated, DeliveryConfirmed | Shipping metrics |

**Criticality:** HIGH — Order fulfillment path

---

### 11. POS Service (Port 5011)

| Depends On | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Product | REST | GetProduct, GetProductBySku, GetPrice | — | Lookup on sale |
| Promotion | REST | ValidateCoupon, ApplyDiscount | — | Coupon validation |
| Payment | REST | ProcessPayment | — | Payment processing |
| Warehouse | EVENT | — | StockMovedEvent | Real-time inventory |

| Called By | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| Warehouse | EVENT | — | POSTransactionCreated | Inventory decrement |
| Accounting | EVENT | — | POSTransactionCreated, CashShiftClosed | Sales GL posting |
| Notification | EVENT | — | POSTransactionCreated | Receipt emails |
| Reporting | EVENT | — | POSTransactionCreated | POS analytics |

**Criticality:** MEDIUM — Retail only (not critical for online orders)

---

### 12. Notification Service (Port 5013)

| Depends On | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| **All other services** | EVENT | — | All events | Customer & internal notifications |

| Called By | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| — | — | — | — | **Notification consumer only - no callers** |

**Criticality:** LOW — Nice-to-have (can queue emails offline)

---

### 13. Reporting Service (Port 5014)

| Depends On | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| **All other services** | EVENT | — | All events | Data warehouse population |

| Called By | Type | Method | Event(s) | Purpose |
|-----------|------|--------|----------|---------|
| External BI tools | REST | GetFactTables, GetDimensions, QueryMetrics | — | Analytics queries |

**Criticality:** LOW — Analytics only (can run offline)

---

## Service Tier Classification

### Tier 0: Foundation (No Dependencies)
- **Product Service** — All services need products

### Tier 1: Core Authentication
- **Identity Service** — All services validate users

### Tier 2: Core Transaction Processing
- **Sales Order Service** — Order pipeline
- **Warehouse Service** — Inventory allocation
- **Accounting Service** — Financial records

### Tier 3: Fulfillment
- **Shipping Service** — Order delivery
- **Payment Service** — Cash collection
- **Purchasing Service** — Supply replenishment

### Tier 4: Enablement
- **CRM Service** — Customer relationships
- **HRM Service** — Payroll
- **Promotion Service** — Discounts

### Tier 5: Retail
- **POS Service** — Point-of-sale

### Tier 6: Analytics & Notifications
- **Notification Service** — Customer comms
- **Reporting Service** — BI dashboards

---

## Critical Path Analysis

### Most Critical Service (Most Callers)
1. **Identity Service** — Called by nearly all other services (100% dependency)
2. **Product Service** — Called by 5 services (Sales, Warehouse, POS, Promotion)
3. **Warehouse Service** — Called by 4 services (Sales, Purchasing, POS, Shipping)

### Failure Impact

| Service Down | Impact | RTO | Mitigation |
|-------------|--------|-----|-----------|
| Identity | Complete system outage | 15 min | Cache role permissions |
| Product | Cannot create orders | 1 hour | Use cached catalog |
| Warehouse | Cannot reserve inventory | 30 min | Manual allocation |
| Sales | Cannot process orders | 2 hours | Queue orders offline |
| Payment | Cannot collect cash | 4 hours | Manual processing |
| Accounting | Cannot post GL | 8 hours | Batch posting when up |
| Shipping | Orders stuck | 4 hours | Hold in warehouse |
| POS | Retail unable to sell | 1 hour | Offline POS mode |
| CRM | Lost opportunity tracking | No impact | Minimal impact |
| HRM | Payroll delayed | 1 week | Manual payroll |
| Notification | Users not notified | No impact | Notifications lost |
| Reporting | Dashboards stale | No impact | BI delayed |

---

## Dependency Depth (Longest Chain)

```
Customer Places Order in POS
  POS → Product (GET price) [Depth 1]
  POS → Promotion (VALIDATE coupon) [Depth 1]
  POS → Payment (PROCESS) [Depth 1]
  POS → Sales (CREATE order) [Depth 2]
      Sales → Identity (VALIDATE) [Depth 3]
      Sales → Product (GET product) [Depth 3]
      Sales → Promotion (GET rules) [Depth 3]
      Sales → Warehouse (RESERVE) [Depth 3]
          Warehouse → Identity (VALIDATE) [Depth 4]
          Warehouse → Product (GET) [Depth 4]

Maximum Depth: 4 hops
Critical Path: POS → Product, Payment, Promotion = 1 hop each (parallel)
```

---

## Resilience Patterns

### Circuit Breaker Pattern
- Sales calling Product: 5-second timeout, 3 failures = open circuit
- Sales calling Promotion: 3-second timeout, fail-open (no discount)
- POS calling Payment: 10-second timeout, queue for retry

### Fallback Strategy
- Product unavailable: Cache last known price
- Promotion unavailable: Apply no discount
- Payment unavailable: Queue transaction offline
- Warehouse unavailable: Hold order, manual allocation

### Retry Policy
- Transient errors (5xx): Retry 3x with exponential backoff
- Timeout errors: Retry 2x with longer timeout
- Permanent errors (4xx): Fail immediately, log for ops

---

**Created:** 2026-06-13
**Synchronous Dependencies:** 20+ REST calls
**Asynchronous Dependencies:** 48 Kafka events
**Maximum Call Depth:** 4 hops
**Critical Path:** 3 services (Identity, Product, Warehouse)
