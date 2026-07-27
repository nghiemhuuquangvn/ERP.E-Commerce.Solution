# Integration Map

> **Note (2026-07-10, carried forward from this file's previous redirect-stub
> version):** this document is a higher-level design narrative and drifts from
> the actual wired code in a few places (e.g. it uses `GoodsReceivedEvent`
> where the real code publishes `PurchaseOrderReceivedDomainEvent`, and
> references a 14-service count from before `SharedMasterDataService`
> existed). For the code-accurate version of the PO and SO flows
> specifically, prefer `docs/business/Purchasing/PurchasingWorkflow.md` and
> `docs/business/Sales/SalesWorkflow.md`.

**Purpose:** Visual and detailed reference of event flows, integration patterns, and data synchronization across the microservices.

---

## Event Flow Matrix (48 Events × 12 Publishers × 13 Consumers)

### Publisher → Subscribers Matrix

| Publisher | Events | Subscribers | Total Flows |
|-----------|--------|-------------|------------|
| Identity | 3 | HRM, CRM, Sales, Accounting, NTF, RPT | 18 |
| Sales | 5 | Warehouse, Accounting, CRM, Payment, Shipping, NTF, RPT | 35 |
| Purchasing | 4 | Warehouse, Accounting, NTF, RPT | 16 |
| Warehouse | 4 | Sales, Accounting, Shipping, POS, NTF, RPT | 24 |
| Accounting | 4 | Payment, Reporting, NTF, RPT | 12 |
| CRM | 5 | Sales, Notification, Reporting | 10 |
| HRM | 4 | Accounting, Payroll, NTF, RPT | 12 |
| Product | 4 | Sales, Warehouse, POS, Promotion, NTF, RPT | 24 |
| Payment | 4 | Sales, Accounting, NTF, RPT | 12 |
| Shipping | 3 | Sales, Accounting, Notification, Reporting | 12 |
| POS | 3 | Warehouse, Accounting, NTF, RPT | 12 |
| Promotion | 4 | Sales, POS, NTF, RPT | 12 |
| **TOTAL** | **48** | **—** | **209 event flows** |

---

## Critical Business Workflows

### 1. Order-to-Cash Cycle (E-Commerce)

**Timeline:** 2-10 minutes | **Event Count:** 8 events

```
START: Customer Places Order at POS/Web
  └─ SalesOrderCreatedEvent (SalesService)
     ├─→ InventoryReservedEvent (WarehouseService)
     │   └─→ SalesService confirms reservation
     │
     ├─→ InvoiceCreatedEvent (AccountingService)
     │   └─→ PaymentService awaits payment
     │
     ├─→ LeadQualifiedEvent (CRMService)
     │   └─→ Track opportunity
     │
     └─→ OrderConfirmationEmail (NotificationService)
         └─→ Customer receives order confirmation

MIDDLE: Customer Pays
  └─ PaymentProcessedEvent (PaymentService)
     ├─→ JournalEntryPostedEvent (AccountingService)
     │   └─→ GL updated in real-time
     │
     ├─→ ShipmentCreatedEvent (ShippingService)
     │   └─→ Warehouse picks & packs
     │
     ├─→ StockMovedEvent (WarehouseService)
     │   └─→ Inventory decremented
     │
     ├─→ OrderShippedEmail (NotificationService)
     │   └─→ Customer receives tracking
     │
     └─→ FactSalesOrderUpdated (ReportingService)
         └─→ Dashboard updated

END: Order Delivered
  └─ DeliveryConfirmedEvent (ShippingService)
     ├─→ SalesOrderDeliveredEvent (SalesService)
     │   └─→ Order marked complete
     │
     ├─→ JournalEntryPostedEvent (AccountingService)
     │   └─→ Revenue recognized
     │
     ├─→ OpportunityWonEvent (CRMService)
     │   └─→ Pipeline updated
     │
     ├─→ DeliveryConfirmedEmail (NotificationService)
     │   └─→ Customer receives thank you
     │
     └─→ FactSalesOrderFinal (ReportingService)
         └─→ Analytics complete
```

**Critical Path:** 3 synchronous REST calls (Product lookup → Promotion discount → Payment) + 8 async events

**Resilience Points:**
- Inventory unavailable: Order queued
- Payment fails: Retry with customer notification
- Shipping unavailable: Order held in warehouse
- Accounting down: GL posted when service recovers (Outbox)

---

### 2. Procure-to-Pay Cycle (Supply Chain)

**Timeline:** 2-7 days | **Event Count:** 6 events

```
START: Procurement Creates PO
  └─ PurchaseOrderCreatedEvent (PurchaseOrderService)
     ├─→ ReceiptScheduledEvent (WarehouseService)
     │   └─→ Warehouse prepares receiving location
     │
     └─→ NotificationEmail (NotificationService)
         └─→ Supplier notified

MIDDLE: Goods Received
  └─ GoodsReceivedEvent (WarehouseService)
     ├─→ StockMovedEvent (WarehouseService)
     │   └─→ Inventory incremented
     │
     ├─→ BillApprovedEvent (AccountingService)
     │   └─→ Payable recorded
     │
     └─→ BillApprovedEmail (NotificationService)
         └─→ AP team notified

END: Payment Made
  └─ PaymentProcessedEvent (PaymentService)
     ├─→ JournalEntryPostedEvent (AccountingService)
     │   └─→ GL and AP ledgers updated
     │
     └─→ PaymentConfirmationEmail (NotificationService)
         └─→ Supplier paid

Analytics:
  └─ FactPaymentFinalized (ReportingService)
     └─→ Procure-to-Pay metrics calculated
```

**Critical Path:** PurchaseOrderCreated → ReceiptScheduled → GoodsReceived → BillApproved → PaymentProcessed = 5 sequential events

**Average Duration:** PO creation (5 min) → Receipt (2-7 days) → Payment (1-3 days after receipt)

---

### 3. Retail POS Transaction Cycle

**Timeline:** 30-60 seconds | **Event Count:** 3 events

```
START: Customer at Register
  └─ POS Terminal Creation
     ├─ Product Lookup (REST: ProductService) [100ms]
     ├─ Coupon Validation (REST: PromotionService) [50ms]
     ├─ Payment Processing (REST: PaymentService) [3-5sec]
     │
     └─ POSTransactionCreatedEvent
        ├→ StockMovedEvent (WarehouseService)
        │   └─→ Retail inventory decremented
        │
        ├→ JournalEntryPostedEvent (AccountingService)
        │   └─→ Sales GL posted
        │
        ├→ ReceiptEmail (NotificationService)
        │   └─→ Digital receipt sent
        │
        └→ FactPOSTransactionUpdated (ReportingService)
            └─→ Retail metrics updated

END: Cash Shift Closed (End of Day)
  └─ CashShiftClosedEvent
     ├→ JournalEntryPostedEvent (AccountingService)
     │   └─→ Daily sales GL posting
     │
     └→ CashShiftSummaryEmail (NotificationService)
         └─→ Manager receives shift report
```

**Critical Path:**
- Synchronous: Product lookup (100ms) + Promotion (50ms) + Payment (3-5sec) = 3.15-5.15 seconds per transaction
- Asynchronous: StockMoved + JournalEntry + Receipt = near-real-time (< 1 second)

**Transaction Throughput:**
- At 5 seconds per transaction: 12 transactions/minute per register
- 50 registers × 12 = 600 transactions/minute per store
- Typical store (10 registers): 120 transactions/minute

---

### 4. User Provisioning Cycle

**Timeline:** 10-30 seconds | **Event Count:** 4 events

```
START: Admin Creates User
  └─ UserCreatedEvent (IdentityService)
     ├─→ EmployeeRecordCreated (HRMService)
     │   └─→ HR records created for new employee
     │
     ├─→ ContactRecordCreated (CRMService)
     │   └─→ CRM contact established
     │
     ├─→ GLUserCreated (AccountingService)
     │   └─→ GL user account set up
     │
     ├─→ WelcomeEmail (NotificationService)
     │   └─→ User receives onboarding email
     │
     └─→ UserDimensionCreated (ReportingService)
         └─→ Analytics tracking begins
```

**Critical Path:** UserCreated → (parallel to HRM, CRM, Accounting, Notification)

**Sequential Constraints:** None — all async, can happen in parallel

---

## Integration Patterns Used

### Pattern A: Event Fanout (Broadcast)

**Used by:** Notification Service, Reporting Service

```
Event published by Service A
  ↓
Topic (Kafka)
  ├→ Notification Service subscribed
  │   └─→ Filter by notification type → Send Email/SMS/Push
  │
  └→ Reporting Service subscribed
      └─→ Denormalize → Update Fact Table
```

**Services:** NotificationService (all events), ReportingService (all events)

**Decoupling:** Complete — subscribers never call publishers

---

### Pattern B: Request-Reply via Events

**Used by:** Sales → Warehouse

```
SalesService publishes SalesOrderCreatedEvent
  ↓
WarehouseService subscribes (async)
  ├─→ Validate inventory
  ├─→ Create reservation
  └─→ Publish InventoryReservedEvent

SalesService subscribes to InventoryReservedEvent
  └─→ Confirm order can proceed
```

**Use Case:** Inventory availability response (guaranteed)

**Timing:** SalesOrderCreated → InventoryReserved = 200-500ms

---

### Pattern C: Eventual Consistency

**Used by:** Sales → Payment → Accounting

```
SalesService creates order
  ↓ SalesOrderCreatedEvent
PaymentService receives payment
  ↓ PaymentProcessedEvent
AccountingService posts GL
  ↓ JournalEntryPostedEvent

Consistency guarantee: Within 1-5 seconds all services see consistent state
(AR ledger updated, GL updated, revenue recorded)
```

---

### Pattern D: Saga Pattern (Distributed Transaction)

**Used by:** Order-to-Cash workflow

```
Client sends CreateOrder request
  ↓
SalesService creates order
  └─→ SalesOrderCreatedEvent
     ├─→ WarehouseService reserves inventory
     │   └─→ InventoryReservedEvent (commit step 1)
     │
     ├─→ AccountingService creates invoice
     │   └─→ InvoiceCreatedEvent (commit step 2)
     │
     └─→ If payment fails:
         └─→ SalesOrderCancelledEvent
             ├─→ InventoryReservation released (compensating transaction)
             └─→ Invoice cancelled (compensating transaction)
```

**Guaranteed Atomic Behavior:** Either entire order succeeds or rolls back (no partial orders)

---

## Service-to-Service Communication Timeline

### Fastest Paths (Parallel, < 100ms)

```
Product Service: GET product (via cache) → 10-50ms
  └─→ Warehouse: GET inventory (via cache) → 10-50ms
  └─→ Promotion: VALIDATE coupon (via cache) → 10-50ms
Total: ~100ms (3 parallel REST calls)
```

### Critical Path (Sequential, 200-500ms)

```
POS creates transaction
  → Product lookup: 50ms
  → Promotion validation: 50ms
  → Payment processing: 300-400ms
Total: 400-500ms
```

### Longest End-to-End Path (3-10 minutes)

```
SalesOrderCreated
  → InventoryReserved: 200ms
  → InvoiceCreated: 100ms
  → PaymentProcessed: 3-5 seconds
  → ShipmentCreated: 1-2 minutes
  → Warehouse picking: 5-10 minutes
Total: 6-15 minutes
```

---

## Message Volume Estimation

### Daily Event Volume (by Service)

Assuming:
- 10,000 orders/day
- 1,000 POS transactions/hour = 24,000/day
- 100 purchase orders/day
- 500 GL entries/day
- etc.

| Service | Events/Day | Avg Size | Daily Volume |
|---------|-----------|----------|---|
| Sales | 50,000 | 2 KB | 100 MB |
| POS | 24,000 | 1 KB | 24 MB |
| Warehouse | 100,000 | 500 B | 50 MB |
| Payment | 11,000 | 1 KB | 11 MB |
| Accounting | 5,000 | 2 KB | 10 MB |
| Shipping | 10,000 | 1.5 KB | 15 MB |
| Product | 100 | 3 KB | 300 KB |
| CRM | 1,000 | 2 KB | 2 MB |
| HRM | 50 | 2 KB | 100 KB |
| Promotion | 500 | 1 KB | 500 KB |
| **TOTAL** | **201,650/day** | **~1.4 KB avg** | **~213 MB/day** |

**Peak Load:** 201,650 / (8 business hours × 60 min) = 42 events/second

**Storage:** 213 MB/day × 365 days = 77.7 GB/year (with 7-day Kafka retention)

---

## Failure Scenarios & Recovery

### Scenario 1: Payment Service Down

```
Customer places order
  ✅ SalesOrderCreatedEvent published (stored in Outbox)
  ✅ InventoryReservedEvent published
  ❌ PaymentService offline → Cannot process payment

Recovery:
  1. Order marked "Awaiting Payment" in Sales Service
  2. Inventory reservation expires after 24 hours
  3. PaymentService comes back up
  4. Outbox processor retries → PaymentProcessed event eventually published
  5. Order proceeds with fulfillment

Duration: Order queued until payment recovers (max 24 hours)
```

### Scenario 2: Warehouse Service Down

```
Customer places order
  ✅ SalesOrderCreatedEvent published
  ❌ WarehouseService offline → Cannot reserve inventory

Recovery:
  1. Order created but not confirmed to customer
  2. WarehouseService recovers
  3. Outbox processor retries SalesOrderCreatedEvent
  4. InventoryReservedEvent published
  5. Order can proceed

Duration: Order queued until warehouse recovers (max 4 hours with alerting)
```

### Scenario 3: Notification Service Down

```
Customer places order
  ✅ SalesOrderCreatedEvent published
  ✅ InventoryReserved
  ✅ Order confirmed
  ❌ NotificationService offline → No confirmation email

Recovery:
  1. NotificationService recovers
  2. Outbox processor retries UserCreatedEvent, SalesOrderCreatedEvent, etc.
  3. Backlog of emails sent
  4. No impact on order fulfillment

Duration: Emails delayed but no business impact
```

### Scenario 4: Accounting Service Down

```
Customer places order
  ✅ SalesOrderCreatedEvent published
  ✅ InventoryReserved
  ✅ Payment processed
  ❌ AccountingService offline → No GL posting

Recovery:
  1. AcccountingService recovers
  2. Outbox processor retries:
     → InvoiceCreatedEvent
     → JournalEntryPostedEvent
     → PaymentReceivedEvent
  3. GL catches up (batch processing)
  4. Financial reports regenerated

Duration: GL behind by max time service was down (typically < 1 hour)
Workaround: Manual journal entry or GL query from recovered service
```

---

## Monitoring & Observability

### Event Processing SLAs

| Service | Event Processing | Target |
|---------|-----------------|--------|
| Identity | UserCreatedEvent → Subscribers | < 1 second |
| Sales | SalesOrderCreatedEvent → Warehouse | < 500ms |
| Warehouse | InventoryReservedEvent → Sales | < 200ms |
| Payment | PaymentProcessedEvent → GL posting | < 5 seconds |
| Shipping | DeliveryConfirmedEvent → Invoice | < 1 second |
| POS | POSTransactionCreatedEvent → GL | < 5 seconds |
| Notification | All events → Email sent | < 30 seconds |
| Reporting | All events → Fact table updated | < 5 minutes (batch) |

### Key Metrics

- **Event Throughput:** Events/second by service
- **Event Latency:** Publish time → Consumer processing time (P50, P95, P99)
- **Outbox Lag:** Age of oldest unprocessed OutboxMessage
- **Kafka Consumer Lag:** Distance behind producer
- **Failed Events:** Events that failed and landed in DLQ

### Alerting Rules

```
Alert if:
  - Outbox size > 1000 (service possibly broken)
  - Outbox age > 5 minutes (processor not running)
  - Consumer lag > 10,000 (catching up too slow)
  - DLQ size > 100 (repeated failures)
  - Event latency P99 > 5 seconds (SLA breach)
  - Service unavailable (health check fails)
```

---

## Future Enhancements

### 1. Event Sourcing (Phase 4)
- Store all events in event store (in addition to Outbox)
- Rebuild aggregates from event history
- Perfect audit trail

### 2. CQRS Read Models (Phase 4)
- Populate read-optimized views from events
- Ultra-fast product searches, customer queries
- Real-time dashboards

### 3. Event Versioning (Phase 3.5)
- Support multiple event versions in parallel
- Graceful schema evolution
- Backward compatibility

### 4. Choreography vs Orchestration (Phase 4)
- Replace saga pattern with orchestration service
- More explicit workflow management
- Better error handling

### 5. Event Deduplication (Phase 3.5)
- Use EventId for exactly-once semantics
- Idempotent event handlers
- No duplicate charging scenarios

---

**Created:** 2026-06-13
**Event Flows:** 209 total (48 events × average 4.4 subscribers)
**Critical Paths:** 4 (Order-to-Cash, Procure-to-Pay, POS, User Provisioning)
**Peak Load:** ~42 events/second
**Daily Volume:** ~213 MB with 7-day retention
**Failure Recovery:** Guaranteed via Outbox pattern (< 24 hour max delay)
