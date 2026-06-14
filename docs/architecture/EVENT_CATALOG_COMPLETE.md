# Complete Event Catalog - All 14 Services

**Purpose:** Reference guide for all 39+ integration events in the ERP E-Commerce platform.

**Format:** `{AggregateRoot}{Action}Event`

**Delivery:** Kafka topics (one per service) with Outbox pattern for guaranteed delivery

---

## Event Summary (39+ Events)

| Service | Event Count | Topics |
|---------|------------|--------|
| Identity | 3 | identity-topic |
| Sales | 5 | salesorder-topic |
| Purchasing | 4 | purchaseorder-topic |
| Warehouse | 4 | warehouse-topic |
| Accounting | 4 | accounting-topic |
| CRM | 5 | crm-topic |
| HRM | 4 | hrm-topic |
| Product | 4 | product-topic |
| Payment | 4 | payment-topic |
| Shipping | 3 | shipping-topic |
| POS | 3 | pos-topic |
| Promotion | 4 | promotion-topic |
| Notification | 0 | (consumer only) |
| Reporting | 0 | (consumer only) |
| **TOTAL** | **48 events** | **12 topics** |

---

## 1. Identity Service Events (identity-topic)

### UserCreatedEvent
- **Occurs:** New user registered in system
- **Publisher:** IdentityService
- **Subscribers:** HRMService, CRMService, SalesService, AccountingService
- **Properties:** UserId, Email, FirstName, LastName, Roles, TenantId, CreatedAt
- **Usage:** Initialize user records in dependent services

### UserUpdatedEvent
- **Occurs:** User profile information changed
- **Publisher:** IdentityService
- **Subscribers:** All services (optional)
- **Properties:** UserId, Email, FirstName, LastName, Roles, UpdatedAt
- **Usage:** Sync user data across services

### RoleAssignedEvent
- **Occurs:** Role assigned to user
- **Publisher:** IdentityService
- **Subscribers:** Authorization services
- **Properties:** UserId, RoleName, Permissions[], AssignedAt
- **Usage:** Update user permissions in cache

---

## 2. Sales Order Service Events (salesorder-topic)

### SalesOrderCreatedEvent
- **Occurs:** New sales order entered
- **Publisher:** SalesOrderService
- **Subscribers:** WarehouseService, AccountingService, CRMService, NotificationService, ReportingService
- **Properties:** OrderId, CustomerId, OrderDate, TotalAmount, LineItems[], Status, CreatedAt
- **Critical Path:** Triggers inventory reservation
- **Usage:** Order-to-Cash workflow start

### SalesOrderApprovedEvent
- **Occurs:** Sales order approved for fulfillment
- **Publisher:** SalesOrderService
- **Subscribers:** WarehouseService, AccountingService, ShippingService
- **Properties:** OrderId, CustomerId, ApprovedAt, TotalAmount, ApprovedBy
- **Critical Path:** Begins fulfillment
- **Usage:** Confirms inventory reservation

### SalesOrderShippedEvent
- **Occurs:** Order shipped from warehouse
- **Publisher:** SalesOrderService (via Warehouse sync)
- **Subscribers:** NotificationService, ReportingService
- **Properties:** OrderId, CustomerId, TrackingNumber, CarrierId, ShippedAt
- **Usage:** Notify customer, update analytics

### SalesOrderDeliveredEvent
- **Occurs:** Order delivered to customer
- **Publisher:** SalesOrderService (via Shipping sync)
- **Subscribers:** AccountingService, CRMService, NotificationService
- **Properties:** OrderId, CustomerId, DeliveryDate, DeliveredAt
- **Usage:** Close order, update CRM opportunity, finalize payment

### SalesOrderCancelledEvent
- **Occurs:** Order cancelled (customer or system)
- **Publisher:** SalesOrderService
- **Subscribers:** WarehouseService, AccountingService, NotificationService
- **Properties:** OrderId, CustomerId, CancellationReason, CancelledAt, CancelledBy
- **Usage:** Release inventory reservation, reverse invoice

---

## 3. Purchase Order Service Events (purchaseorder-topic)

### PurchaseOrderCreatedEvent
- **Occurs:** New purchase order created
- **Publisher:** PurchaseOrderService
- **Subscribers:** WarehouseService, AccountingService
- **Properties:** POId, SupplierId, OrderDate, TotalAmount, LineItems[], CreatedAt
- **Usage:** Prepare warehouse receiving location

### PurchaseOrderApprovedEvent
- **Occurs:** PO approved (sent to supplier)
- **Publisher:** PurchaseOrderService
- **Subscribers:** WarehouseService, AccountingService, NotificationService
- **Properties:** POId, SupplierId, ApprovedAt, ApprovedBy, ExpectedDeliveryDate
- **Usage:** Confirm supplier order

### ReceiptScheduledEvent
- **Occurs:** Expected delivery date set/updated
- **Publisher:** PurchaseOrderService
- **Subscribers:** WarehouseService
- **Properties:** POId, ExpectedDeliveryDate, ScheduledAt
- **Usage:** Alert warehouse of incoming goods

### GoodsReceivedEvent
- **Occurs:** Physical goods received in warehouse
- **Publisher:** WarehouseService (triggered by PurchaseOrder)
- **Subscribers:** AccountingService, NotificationService
- **Properties:** POId, ReceiptDate, ReceivedQuantities[], Inspection[], ReceivedAt
- **Usage:** Update inventory, record AP invoice, notify supplier

---

## 4. Warehouse Service Events (warehouse-topic)

### InventoryReservedEvent
- **Occurs:** Inventory quantity reserved for sales order
- **Publisher:** WarehouseService
- **Subscribers:** SalesOrderService, AccountingService, NotificationService
- **Properties:** InventoryId, SalesOrderId, Quantity, ReservedAt, ExpirationDate
- **Critical Path:** Confirms inventory availability
- **Usage:** Confirm to customer that items are allocated

### StockMovedEvent
- **Occurs:** Inventory moved between locations or issued from warehouse
- **Publisher:** WarehouseService
- **Subscribers:** AccountingService, ReportingService
- **Properties:** InventoryId, MovementType (Issue, Adjustment, Transfer), Quantity, FromLocation, ToLocation, MovedAt
- **Usage:** Track stock flow, update GL inventory valuation

### InventoryAdjustedEvent
- **Occurs:** Inventory adjusted due to cycle count or damage
- **Publisher:** WarehouseService
- **Subscribers:** AccountingService, ReportingService
- **Properties:** InventoryId, AdjustmentType, OldQuantity, NewQuantity, Reason, AdjustedAt
- **Usage:** Record inventory variance, update GL

### LowStockAlertEvent
- **Occurs:** Product falls below reorder point
- **Publisher:** WarehouseService
- **Subscribers:** NotificationService, PurchaseOrderService
- **Properties:** ProductId, CurrentQuantity, ReorderPoint, AlertAt
- **Usage:** Trigger reorder, notify procurement

---

## 5. Accounting Service Events (accounting-topic)

### InvoiceCreatedEvent
- **Occurs:** Invoice generated from sales order
- **Publisher:** AccountingService
- **Subscribers:** PaymentService, NotificationService, ReportingService
- **Properties:** InvoiceId, SalesOrderId, CustomerId, InvoiceDate, DueDate, Amount, Terms
- **Usage:** Issue invoice to customer, record AR

### BillApprovedEvent
- **Occurs:** Vendor bill approved for payment
- **Publisher:** AccountingService
- **Subscribers:** PaymentService, NotificationService, ReportingService
- **Properties:** BillId, PurchaseOrderId, SupplierId, BillAmount, DueDate, ApprovedAt, ApprovedBy
- **Usage:** Schedule payment, record AP

### JournalEntryPostedEvent
- **Occurs:** Journal entry posted to general ledger
- **Publisher:** AccountingService
- **Subscribers:** ReportingService
- **Properties:** JournalEntryId, EntryDate, Lines[] (Account, Debit, Credit), PostedAt, PostedBy
- **Usage:** Update GL balances, calculate financial reports

### FiscalPeriodClosedEvent
- **Occurs:** Month/quarter/year-end close completed
- **Publisher:** AccountingService
- **Subscribers:** ReportingService, NotificationService
- **Properties:** FiscalPeriodId, ClosingDate, TrialBalance, Differences[], ClosedAt, ClosedBy
- **Usage:** Lock transactions, generate financial statements

---

## 6. CRM Service Events (crm-topic)

### LeadCreatedEvent
- **Occurs:** New sales lead recorded
- **Publisher:** CRMService
- **Subscribers:** SalesOrderService (for salesperson assignment), NotificationService
- **Properties:** LeadId, CompanyName, ContactName, Email, Phone, Source, CreatedAt
- **Usage:** Assign to sales team, schedule follow-up

### LeadQualifiedEvent
- **Occurs:** Lead qualified as sales prospect
- **Publisher:** CRMService
- **Subscribers:** SalesOrderService, NotificationService
- **Properties:** LeadId, QualifiedAt, QualifiedBy, EstimatedValue, SalesStage
- **Usage:** Move to opportunity, create sales order

### OpportunityCreatedEvent
- **Occurs:** Sales opportunity created (from qualified lead)
- **Publisher:** CRMService
- **Subscribers:** SalesOrderService, NotificationService
- **Properties:** OpportunityId, LeadId, Amount, Probability, SalesStage, CloseDate
- **Usage:** Track sales pipeline, forecast revenue

### OpportunityWonEvent
- **Occurs:** Sales opportunity closed (won)
- **Publisher:** CRMService
- **Subscribers:** SalesOrderService, NotificationService, ReportingService
- **Properties:** OpportunityId, Amount, ClosedAt, ClosedBy
- **Usage:** Create sales order, update revenue forecast

### CaseResolvedEvent
- **Occurs:** Customer support case closed
- **Publisher:** CRMService
- **Subscribers:** NotificationService
- **Properties:** CaseId, CustomerId, ResolutionType, Resolution, ResolvedAt, CSAT
- **Usage:** Notify customer, track satisfaction

---

## 7. Human Resources Service Events (hrm-topic)

### EmployeeHiredEvent
- **Occurs:** New employee hired
- **Publisher:** HRMService
- **Subscribers:** AccountingService, PayrollService, NotificationService
- **Properties:** EmployeeId, FirstName, LastName, Email, Department, Position, HireDate, Salary
- **Usage:** Create GL employee record, setup payroll

### EmployeeTerminatedEvent
- **Occurs:** Employee terminated
- **Publisher:** HRMService
- **Subscribers:** AccountingService, PayrollService, NotificationService
- **Properties:** EmployeeId, TerminationDate, Reason, FinalPayment, TerminatedBy
- **Usage:** Disable access, process final payment

### PaySlipGeneratedEvent
- **Occurs:** Monthly pay slip generated
- **Publisher:** HRMService
- **Subscribers:** AccountingService, PaymentService, NotificationService
- **Properties:** PaySlipId, EmployeeId, PayPeriod, GrossSalary, Deductions, NetSalary, GeneratedAt
- **Usage:** Record salary expense, initiate payment

### LeaveApprovedEvent
- **Occurs:** Employee leave request approved
- **Publisher:** HRMService
- **Subscribers:** NotificationService
- **Properties:** LeaveRequestId, EmployeeId, StartDate, EndDate, Days, ApprovedAt, ApprovedBy
- **Usage:** Update calendar, notify employee

---

## 8. Product Service Events (product-topic)

### ProductCreatedEvent
- **Occurs:** New product added to catalog
- **Publisher:** ProductService
- **Subscribers:** SalesOrderService, POSService, ReportingService
- **Properties:** ProductId, Name, Sku, ProductType, Price, CategoryId, CreatedAt
- **Usage:** Make available for sales

### ProductPublishedEvent
- **Occurs:** Product activated (ready for sale)
- **Publisher:** ProductService
- **Subscribers:** SalesOrderService, POSService, NotificationService, ReportingService
- **Properties:** ProductId, Name, PublishedAt
- **Usage:** List on storefront, make available in POS

### ProductPriceChangedEvent
- **Occurs:** Product price updated
- **Publisher:** ProductService
- **Subscribers:** SalesOrderService, POSService, PromotionService, ReportingService
- **Properties:** ProductId, OldPrice, NewPrice, EffectiveDate, ChangedAt, ChangedBy
- **Usage:** Update prices in systems, notify of discounts

### ProductDiscontinuedEvent
- **Occurs:** Product removed from sale
- **Publisher:** ProductService
- **Subscribers:** SalesOrderService, POSService, PromotionService, NotificationService
- **Properties:** ProductId, Name, DiscontinuedAt, DiscontinuedBy, Reason
- **Usage:** Remove from catalogs, notify customers

---

## 9. Payment Service Events (payment-topic)

### PaymentInitiatedEvent
- **Occurs:** Payment transaction started
- **Publisher:** PaymentService
- **Subscribers:** SalesOrderService, AccountingService, NotificationService
- **Properties:** PaymentId, InvoiceId, Amount, PaymentMethod, InitiatedAt
- **Usage:** Lock order, await payment confirmation

### PaymentProcessedEvent
- **Occurs:** Payment successfully captured
- **Publisher:** PaymentService
- **Subscribers:** SalesOrderService, AccountingService, CRMService, NotificationService, ReportingService
- **Properties:** PaymentId, InvoiceId, Amount, ProcessedAt, ConfirmationNumber, GatewayTransactionId
- **Critical Path:** Completes payment
- **Usage:** Mark invoice paid, unlock shipment

### PaymentRefundedEvent
- **Occurs:** Payment refunded to customer
- **Publisher:** PaymentService
- **Subscribers:** AccountingService, SalesOrderService, NotificationService
- **Properties:** PaymentId, RefundId, Amount, Reason, RefundedAt, Status
- **Usage:** Reverse transaction, update AR

### PaymentFailedEvent
- **Occurs:** Payment declined or failed
- **Publisher:** PaymentService
- **Subscribers:** SalesOrderService, NotificationService
- **Properties:** PaymentId, InvoiceId, Amount, FailureReason, FailedAt, RetryCount
- **Usage:** Retry or escalate payment

---

## 10. Shipping Service Events (shipping-topic)

### ShipmentCreatedEvent
- **Occurs:** Shipment prepared from warehouse
- **Publisher:** ShippingService
- **Subscribers:** SalesOrderService, NotificationService, ReportingService
- **Properties:** ShipmentId, SalesOrderId, TrackingNumber, CarrierId, Items[], ShipDate
- **Usage:** Generate shipping label, notify customer

### TrackingUpdatedEvent
- **Occurs:** Shipment tracking status updated
- **Publisher:** ShippingService (from carrier sync)
- **Subscribers:** SalesOrderService, NotificationService
- **Properties:** ShipmentId, TrackingNumber, Status, Location, UpdatedAt
- **Usage:** Update customer, show delivery progress

### DeliveryConfirmedEvent
- **Occurs:** Shipment delivered and signed
- **Publisher:** ShippingService
- **Subscribers:** SalesOrderService, AccountingService, CRMService, NotificationService
- **Properties:** ShipmentId, SalesOrderId, DeliveryDate, DeliveredAt, Signature
- **Critical Path:** Closes order fulfillment
- **Usage:** Mark order complete, unlock payment settlement

---

## 11. POS Service Events (pos-topic)

### POSTransactionCreatedEvent
- **Occurs:** Retail sale completed at POS terminal
- **Publisher:** POSService
- **Subscribers:** WarehouseService, AccountingService, PromotionService, NotificationService, ReportingService
- **Properties:** TransactionId, TerminalId, Amount, Items[], PaymentMethod, TransactionDate
- **Usage:** Decrement inventory, record sale

### CashShiftClosedEvent
- **Occurs:** End of day cash shift completed
- **Publisher:** POSService
- **Subscribers:** AccountingService, NotificationService
- **Properties:** ShiftId, TerminalId, OpenAmount, CloseAmount, Sales[], Variance, ClosedAt
- **Usage:** Reconcile cash, post to GL

### RegisterBalancedEvent
- **Occurs:** Register reconciliation completed
- **Publisher:** POSService
- **Subscribers:** AccountingService, NotificationService
- **Properties:** RegisterId, TerminalId, ExpectedAmount, ActualAmount, Variance, BalancedAt, BalancedBy
- **Usage:** Post adjustment to GL

---

## 12. Promotion Service Events (promotion-topic)

### PromotionCreatedEvent
- **Occurs:** New promotion/discount defined
- **Publisher:** PromotionService
- **Subscribers:** SalesOrderService, POSService, NotificationService
- **Properties:** PromotionId, Name, DiscountType (%), DiscountValue, StartDate, EndDate, CreatedAt
- **Usage:** Activate discount rules

### PromotionActivatedEvent
- **Occurs:** Promotion becomes active
- **Publisher:** PromotionService
- **Subscribers:** SalesOrderService, POSService, NotificationService
- **Properties:** PromotionId, ActivatedAt
- **Usage:** Apply discounts in transaction processing

### CouponRedeemedEvent
- **Occurs:** Coupon code applied to transaction
- **Publisher:** PromotionService
- **Subscribers:** SalesOrderService, AccountingService, ReportingService
- **Properties:** CouponId, PromotionId, Amount, RedeemedAt
- **Usage:** Calculate discount, track usage

### LoyaltyPointsEarnedEvent
- **Occurs:** Customer earns loyalty points
- **Publisher:** PromotionService
- **Subscribers:** CRMService, NotificationService, ReportingService
- **Properties:** CustomerId, TransactionId, PointsEarned, TotalPoints, EarnedAt
- **Usage:** Update loyalty balance, notify customer

---

## Event Flow Diagram: Order-to-Cash Cycle

```
Customer Places Order
    ↓
SalesOrderCreatedEvent (SalesService)
    ├→ WarehouseService: ReserveInventory()
    │   ↓
    │   InventoryReservedEvent (WarehouseService)
    │   └→ SalesService: Confirm reservation
    │
    ├→ AccountingService: CreateInvoice()
    │   ↓
    │   InvoiceCreatedEvent (AccountingService)
    │   └→ PaymentService: Wait for payment
    │
    ├→ CRMService: TrackOpportunity()
    │
    └→ NotificationService: Send confirmation email
    
User Approves Order
    ↓
SalesOrderApprovedEvent (SalesService)
    ├→ WarehouseService: Fulfill order
    │   ├→ StockMovedEvent
    │   └→ ShippingService: CreateShipment()
    │       ↓
    │       ShipmentCreatedEvent
    │       └→ NotificationService: Send shipping notification
    │
    └→ PaymentService: Request payment
    
PaymentProcessedEvent (PaymentService)
    ├→ AccountingService: PostPaymentReceipt()
    │   ↓
    │   JournalEntryPostedEvent
    │
    ├→ SalesOrderService: UpdateOrderStatus()
    │   ↓
    │   SalesOrderDeliveredEvent (when shipped)
    │
    └→ ReportingService: Update FactSalesOrder table

Complete Order-to-Cash Duration:
- User creates order: SalesOrderCreatedEvent → InventoryReservedEvent (2-5 sec)
- Manager approves: SalesOrderApprovedEvent → ShipmentCreatedEvent (1-2 sec)
- Payment processed: PaymentProcessedEvent (2-5 sec)
- Delivery confirmed: DeliveryConfirmedEvent (varies by carrier)
- GL posted: JournalEntryPostedEvent (real-time)
- Analytics updated: ReportingService denormalized (batch 1-5 min)

Total Path: Order creation → Cash receipt: 2-10 minutes
Payment settlement: 1-3 days (depends on payment processor)
```

---

## Event Pattern: Fanout (Notification & Reporting)

```
All 48 events from 12 topics
    ↓
NotificationService subscribes to all
    ├→ Filter by notification preferences
    ├→ Select channel (Email, SMS, Push)
    └→ Send notification (async)

All 48 events from 12 topics
    ↓
ReportingService subscribes to all
    ├→ Denormalize to fact tables
    ├→ Update dimension tables
    └→ Populate BI dashboard (near real-time)
```

---

## Reliability & Guarantees

### Outbox Pattern (Guaranteed Delivery)
- Every event stored in OutboxMessages table before Outbox Processor publishes
- Processor retries every 5 seconds
- Kafka consumer deduplicates via EventId (GUID)
- No events lost, no duplicate processing

### Event Ordering
- Events within a single aggregate root ordered by OccurredOnUtc
- Cross-aggregate ordering: "eventual consistency" (acceptable for business workflows)
- Example: SalesOrderCreated → InventoryReserved may not be instantaneous

### Consumer Responsibilities
- Implement idempotent event handlers (safe to retry)
- Use EventId for deduplication if needed
- Handle out-of-order events gracefully
- Log event processing for audit trail

---

## Event Versioning

**Current:** All events are v1 (no versioning yet)

**Versioning Strategy (when needed):**
```csharp
public abstract class IntegrationEvent
{
    public string EventType { get; set; } // e.g., "SalesOrderCreated:v1"
    public int EventVersion { get; set; } = 1;
    public DateTime OccurredAt { get; set; }
}

// New version adds field:
public class SalesOrderCreatedEventV2 : IntegrationEvent
{
    // ... all v1 fields
    public string CurrencyCode { get; set; } = "USD"; // NEW in v2
}

// Handler:
if (e.EventVersion == 1)
{
    // Handle as v1
}
else if (e.EventVersion == 2)
{
    // Handle as v2
}
```

---

## Event Naming Standards

| Event Type | Format | Example |
|-----------|--------|---------|
| Created | `{AggregateRoot}CreatedEvent` | `ProductCreatedEvent` |
| Updated | `{AggregateRoot}UpdatedEvent` | `ProductUpdatedEvent` |
| Status Change | `{AggregateRoot}{StatusName}Event` | `SalesOrderApprovedEvent`, `OrderShippedEvent` |
| Relationship | `{AggregateRoot}{Relation}Event` | `CustomerAssignedEvent` |
| Deleted | `{AggregateRoot}DeletedEvent` | `ProductDeletedEvent` |
| Error | `{AggregateRoot}{Error}Event` | `PaymentFailedEvent` |

---

## Kafka Topics Configuration

```yaml
# Topics: 12 service topics + 1 DLQ
Topics:
  accounting-topic:
    partitions: 3
    replication-factor: 3
    retention-ms: 604800000 (7 days)
    
  crm-topic:
    partitions: 3
    replication-factor: 3
    retention-ms: 604800000
    
  hrm-topic:
    partitions: 2
    replication-factor: 3
    retention-ms: 604800000
    
  # ... (repeat for all 12 services)
  
  dlq-topic:
    partitions: 1
    replication-factor: 3
    retention-ms: 2592000000 (30 days)
    purpose: Dead letter queue for failed events
```

---

**Created:** 2026-06-13  
**Event Count:** 48 integration events (12 services publishing, 2 consumers fanout)  
**Kafka Topics:** 12 + 1 DLQ  
**Event Naming Convention:** {AggregateRoot}{Action}Event  
**Delivery Guarantee:** Exactly-once via Outbox Pattern
