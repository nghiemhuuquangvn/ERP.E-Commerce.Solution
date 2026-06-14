# Business Domains Index - Phase 2 Complete

**Status:** Phase 2 documentation foundation complete via service README.md files.

**Reference:** All business domain specifications are documented in the comprehensive README.md files for each service. This index serves as a navigation guide.

---

## Business Documentation by Domain

### ERP Core (Back-Office)

#### Identity & Access Management
- **Location:** `src/services/IdentityService/README.md`
- **Topics:** User management, role-based access control (RBAC), permission management, multi-tenancy
- **Workflows:** User registration → Role assignment → Permission grant → Access control
- **Entities:** User, Role, Permission, Tenant, UserRole, RolePermission

#### Accounting & Financial Management
- **Location:** `src/services/AccountingService/README.md`
- **Topics:** General ledger (GL), accounts receivable (AR), accounts payable (AP), invoicing, journal entries
- **Workflows:** 
  - Order-to-Invoice: SalesOrder → Invoice → JournalEntry → GLPosting
  - Bill-to-Payment: PurchaseOrder → Bill → JournalEntry → GLPosting
- **Entities:** Account, JournalEntry, Invoice, Bill, FiscalPeriod, GLTransaction

#### Human Resources & Payroll
- **Location:** `src/services/HRMService/README.md`
- **Topics:** Employee management, departments, attendance tracking, leave management, payroll processing, pay slip generation
- **Workflows:** Employee hire → Department assignment → Attendance tracking → Leave approval → PayslipGeneration
- **Entities:** Employee, Department, LeaveRequest, PaySlip, Attendance, Shift

#### Purchase Order & Procurement
- **Location:** `src/services/PurchaseOrderService/README.md`
- **Topics:** Purchase order creation, supplier management, purchase workflows, receiving, approval processes
- **Workflows:** PurchaseOrder creation → Approval → Supplier confirmation → Receiving → Closing
- **Entities:** PurchaseOrder, Supplier, SupplierContact, PurchaseOrderItem, ReceiptSchedule

---

### Supply Chain & Product Management

#### Product Catalog & Pricing
- **Location:** `src/services/ProductService/README.md`
- **Topics:** Product catalog management, product variants (SKUs), categories, pricing, images, lifecycle management
- **Workflows:** ProductCreation → Variant definition → Category assignment → Price setting → Publication → Discontinuation
- **Entities:** Product, ProductVariant, Category, ProductImage, PriceList, ProductAttribute

#### Warehouse & Inventory
- **Location:** `src/services/WareHouseService/README.md`
- **Topics:** Inventory tracking, stock movements, reservation management, warehouse locations, reorder points
- **Workflows:** ReceiveGoods → StockMovement → Reservation → Issue → Adjustment
- **Entities:** Inventory, StockMovement, Reservation, WarehouseLocation, InventoryBalance

#### Shipping & Logistics
- **Location:** `src/services/ShippingService/README.md`
- **Topics:** Shipment management, carrier integration, shipping rate calculation, tracking, delivery confirmation
- **Workflows:** CreateShipment → AssignCarrier → GenerateLabel → TrackingUpdates → DeliveryConfirmation
- **Entities:** Shipment, Carrier, ShippingRate, TrackingEvent, Manifest, DeliveryProof

---

### E-Commerce & Sales

#### Sales Order & Customer Orders
- **Location:** `src/services/SaleOrderService/README.md`
- **Topics:** Sales order creation, customer order processing, order workflows, fulfillment tracking, order status management
- **Workflows:** OrderCreation → Approval → InventoryReservation → Fulfillment → Shipment → Delivery
- **Entities:** SalesOrder, Customer, OrderItem, OrderStatus, OrderLine, OrderShipment

#### Customer Relationship Management (CRM)
- **Location:** `src/services/CRMService/README.md`
- **Topics:** Contact management, lead tracking, opportunity management, case management, activity logging, customer interactions
- **Workflows:** LeadCreation → LeadQualification → OpportunityCreation → OpportunityClosure → SalesOrder
- **Entities:** Contact, Lead, Opportunity, Case, Activity, InteractionLog

#### Payment Processing & Reconciliation
- **Location:** `src/services/PaymentService/README.md`
- **Topics:** Payment transaction processing, payment method management, refund handling, settlement reconciliation, payment authorization
- **Workflows:** PaymentInitiation → Authorization → Capture → Settlement → Reconciliation
- **Entities:** PaymentTransaction, PaymentMethod, Refund, Settlement, PaymentGatewayResponse, Reconciliation

#### Promotions & Loyalty
- **Location:** `src/services/PromotionService/README.md`
- **Topics:** Promotion management, coupon redemption, flash sales, loyalty program management, discount rules, eligibility
- **Workflows:** PromotionCreation → PromotionActivation → CouponRedeem → LoyaltyPointsEarn → LoyaltyRedemption
- **Entities:** Promotion, Coupon, FlashSale, LoyaltyProgram, LoyaltyAccount, DiscountRule

---

### Retail (Point-of-Sale)

#### Point-of-Sale (POS) & Retail Operations
- **Location:** `src/services/POSService/README.md`
- **Topics:** POS terminal management, retail transaction processing, line item management, cash shift management, register reconciliation
- **Workflows:** TerminalSetup → CashShiftOpen → TransactionCreate → PaymentProcess → CashShiftClose → RegisterReconciliation
- **Entities:** POSTerminal, POSTransaction, LineItem, CashShift, Register, PaymentTender

---

### Cross-Cutting Concerns

#### Notification & Communication
- **Location:** `src/services/NotificationService/README.md`
- **Topics:** Multi-channel notifications (email, SMS, push), notification templates, preference management, notification history
- **Workflows:** EventOccurs → TemplateSelect → NotificationCompose → ChannelSend → DeliveryTracking
- **Entities:** NotificationTemplate, Notification, NotificationPreference, NotificationChannel, DeliveryLog

#### Reporting & Analytics
- **Location:** `src/services/ReportingService/README.md`
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
