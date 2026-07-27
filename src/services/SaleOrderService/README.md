# Sales Order Service

Manages customer sales orders, sales workflows, and order fulfillment processes for the ERP E-Commerce Solution.

## Overview

The Sales Order Service is responsible for the complete sales order lifecycle from creation to delivery. It coordinates with Warehouse Service for inventory management and publishes events for downstream processing.

**Key Functions:**
- Create quotations and convert them into sales orders
- Create and manage customer sales orders, with a full audit trail
- Check stock availability synchronously, before payment is ever charged (added 2026-07-27, Module 8 reconciliation — see below)
- Track order status through fulfillment workflow (order approval → payment → inventory reservation → shipment saga)
- Enforce a per-customer credit limit (this service owns `CreditLimit` — no other service in the system has it)
- Calculate sales-rep commission when an order is actually paid

## Responsibilities

### Quotation Management
- Create quotations with snapshotted line-item pricing
- Draft → Sent → Accepted/Rejected/Expired → Converted lifecycle
- Convert an Accepted quotation into a real SalesOrder (`QuotationId` set on the resulting order)

### Order Management
- Create new sales orders with validation (price snapshotted at creation, never re-read from PriceList later)
- Edit pending orders (Draft/Submitted only)
- Cancel orders and release credit-limit exposure
- Track order status changes with a full audit trail (`OrderStatusHistory`)
- Manage order line items, including partial shipment per line
- Human-readable `OrderNo` (`SO-yyyyMM-00001`, per-tenant/month sequence)
- Attribution fields: `OrderSource` (Manual/Ecommerce/POS), `DepartmentId`, `SalesRepId` — all optional, ID-only references (no local Department/User entity)

### Workflow Orchestration
- Process order approval workflow (`OrderFulfillmentSaga`: Stock pre-check → Order created → Payment → Inventory Reservation → Shipment)
- Coordinate with Warehouse (stock pre-check, inventory reservation), Payment (processed/failed), Shipping (shipment created) via a synchronous HTTP client (pre-check only) plus Kafka/RabbitMQ consumers (everything else)
- Handle order cancellation, return request/completion (refund amount recorded, no local Payment/CreditNote entity — Accounting/Payment own those)
- Track order milestones via `OrderStatusHistoryEntry`

### Stock Pre-Check (added 2026-07-27, Module 8 reconciliation)
- **Fixed a real gap**: previously this service had zero inventory awareness before
  charging payment — an order was created and paid for, and only *afterward* did the saga
  ask WareHouseService to reserve stock (post-payment). If stock was short, the saga
  correctly refunded and cancelled — but only after the customer had already been charged.
- `IWarehouseServiceClient` (mirrors `IPromotionServiceClient`'s `HttpClient` idiom) now
  checks every line's `AvailableQuantity` against WareHouseService's
  `GET /inventory/{productId}/availability` **before** `CreateOrderCommandHandler`
  persists the order or the saga ever fires `InitiatePaymentCommand`. Any line that's
  short — or an unreachable WareHouseService — rejects order creation outright.
- Unlike the coupon-validation client (an outage there just skips the discount), this
  client **fails closed**: it cannot treat "can't reach Warehouse" as "assume in stock"
  without reintroducing the exact problem it exists to close.
- This narrows the race window but does not eliminate it — the actual
  `ReserveInventoryCommand` still fires post-payment, unchanged, and
  `OrderFulfillmentSaga.HandleInventoryInsufficientAsync`'s refund+cancel compensation
  stays exactly as the safety net for whatever residual race remains (e.g. a concurrent
  order between this check and the real reservation).

### Credit Limit (CustomerSalesProfile)
- This service is the **source of truth** for `CreditLimit` — no other service in the system has this field (CRMService has no Customer entity; IdentityService's User has none)
- `CurrentBalance` denormalizes open (Approved..Delivered, not yet Cancelled) order exposure
- `Approve` is rejected with 409 if it would push `CurrentBalance + OrderTotal` over `CreditLimit` — but only once a profile exists for that customer; no profile means no limit is enforced

### Commission
- `CommissionPlan` (simple percent-rate plans) + `CommissionRecord`, created automatically when `PaymentProcessedEvent` arrives for an order with a `SalesRepId` set — there is no `InvoicePaid` event anywhere in this system, so payment confirmation is the real trigger for "the order was actually paid, not just confirmed"
- Idempotent: a second delivery of the same payment event does not create a duplicate record for the same order

### Promotion Integration
- Validates coupon codes against PromotionService at order creation (`PromotionServiceClient`)
- Persists which `PromotionId` produced the applied `DiscountAmount` (previously discarded after validation — now traceable)

## Architecture

### Technology Stack
- **Framework**: ASP.NET Core 10
- **ORM**: Entity Framework Core
- **Database**: SQL Server
- **Patterns**: DDD, CQRS (MediatR), Event-Driven
- **Message Bus**: RabbitMQ
- **Data Streaming**: Kafka (optional)
- **Caching**: Redis (optional)

### Project Structure
```
SaleOrderService/
├── API/                         # REST endpoints
├── Application/                 # Commands, Queries, Handlers
│   ├── Commands/
│   ├── Queries/
│   └── Handlers/
├── Domain/                      # Entities, aggregates
│   ├── Aggregates/
│   ├── ValueObjects/
│   └── Events/
├── Infrastructure/              # Repositories, services
├── Shared/                      # DTOs
└── Tests/
```

## API Endpoints

### Order Management

#### Create Sales Order
```http
POST /api/v1/orders
Authorization: Bearer <token>
Content-Type: application/json

{
  "customerId": "550e8400-e29b-41d4-a716-446655440000",
  "orderDate": "2026-06-13T00:00:00Z",
  "lineItems": [
    {
      "productId": "660e8400-e29b-41d4-a716-446655440000",
      "quantity": 5,
      "unitPrice": 99.99
    }
  ],
  "shippingAddress": {
    "street": "123 Main St",
    "city": "New York",
    "state": "NY",
    "zipCode": "10001"
  },
  "notes": "Urgent delivery needed"
}

Response: 201 Created
{
  "orderId": "770e8400-e29b-41d4-a716-446655440000",
  "customerId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Draft",
  "totalAmount": 499.95,
  "createdAt": "2026-06-13T10:30:00Z"
}
```

#### Get Order Details
```http
GET /api/v1/orders/{orderId}
Authorization: Bearer <token>

Response: 200 OK
{
  "orderId": "770e8400-e29b-41d4-a716-446655440000",
  "customerId": "550e8400-e29b-41d4-a716-446655440000",
  "customerName": "John Doe",
  "status": "Approved",
  "totalAmount": 499.95,
  "lineItems": [
    {
      "productId": "660e8400-e29b-41d4-a716-446655440000",
      "productName": "Product A",
      "quantity": 5,
      "unitPrice": 99.99,
      "lineTotal": 499.95
    }
  ],
  "shippingAddress": { ... },
  "createdAt": "2026-06-13T10:30:00Z",
  "approvedAt": "2026-06-13T11:00:00Z"
}
```

#### List Orders
```http
GET /api/v1/orders?customerId=550e8400&status=Approved&skip=0&take=20
Authorization: Bearer <token>

Response: 200 OK
{
  "total": 150,
  "items": [
    {
      "orderId": "770e8400-e29b-41d4-a716-446655440000",
      "customerId": "550e8400-e29b-41d4-a716-446655440000",
      "customerName": "John Doe",
      "status": "Approved",
      "totalAmount": 499.95,
      "createdAt": "2026-06-13T10:30:00Z"
    }
  ]
}
```

#### Approve Order
```http
POST /api/v1/orders/{orderId}/approve
Authorization: Bearer <token>

Response: 200 OK
{
  "orderId": "770e8400-e29b-41d4-a716-446655440000",
  "status": "Approved",
  "approvedAt": "2026-06-13T11:00:00Z"
}
```

#### Cancel Order
```http
POST /api/v1/orders/{orderId}/cancel
Authorization: Bearer <token>

{
  "reason": "Customer requested cancellation"
}

Response: 200 OK
```

#### Ship Order
```http
POST /api/v1/orders/{orderId}/ship
Authorization: Bearer <token>

{
  "trackingNumber": "FEDEX123456789",
  "carrier": "FedEx"
}

Response: 200 OK
```

### Quotations (`api/v1/quotations`)

```http
POST   /api/v1/quotations                       # create (Draft)
GET    /api/v1/quotations/{id}
GET    /api/v1/quotations?customerId=&page=&pageSize=
POST   /api/v1/quotations/{id}/send              # Draft -> Sent
POST   /api/v1/quotations/{id}/accept            # Sent -> Accepted (customer approval)
POST   /api/v1/quotations/{id}/reject            # Sent -> Rejected
POST   /api/v1/quotations/{id}/convert-to-order  # Accepted -> Converted, creates a real SalesOrder
```

### Credit Limit (`api/v1/customer-sales-profiles`)

```http
GET  /api/v1/customer-sales-profiles/{customerId}   # 404 if no profile — treated as "no limit"
PUT  /api/v1/customer-sales-profiles/{customerId}   # { "creditLimit": 50000 } — creates or updates
```

### Commission (`api/v1/commission-plans`, `api/v1/commissions`)

```http
GET  /api/v1/commission-plans
POST /api/v1/commission-plans                    # { "name": "Standard 5%", "rateValue": 0.05 }
POST /api/v1/commission-plans/{id}/deactivate
GET  /api/v1/commissions?salesRepId=&from=&to=    # CommissionRecords, created automatically on PaymentProcessedEvent
```

## Published Events

All published via RabbitMQ topic exchange `sales_order_events` (routing key `sales_order.{EventTypeName}`), staged transactionally into the `OutboxMessages` table by the repository (`DomainEventOutboxWriter`) and delivered by the background `SalesOrderEventOutboxProcessor` — not published synchronously from the command handler anymore, so a crash between the DB write and the publish can no longer silently drop an event.

- `SalesOrderCreatedEvent` — new order created
- `SalesOrderApprovedEvent` — order approved (also increases the customer's `CustomerSalesProfile.CurrentBalance`)
- `SalesOrderShippedEvent` / `SalesOrderPartiallyShippedEvent` — full or partial fulfillment
- `SalesOrderDeliveredEvent` — order delivered (releases credit-limit exposure)
- `SalesOrderCancelledEvent` — order cancelled (releases credit-limit exposure if it had been Approved)
- `SalesOrderReturnedEvent` — return completed, carries `RefundAmount`
- `SalesQuotationCreatedEvent` / `SalesQuotationAcceptedEvent` — quotation lifecycle

**Not published here** (published by the service that actually owns the concept): Invoice events (AccountingService auto-creates an Invoice from `SalesOrderCreatedEvent`), Payment events (PaymentService), Shipment tracking events (ShippingService).

## Consumed Events

**Note:** the stock pre-check (added 2026-07-27) is a synchronous HTTP call
(`IWarehouseServiceClient` → `GET /inventory/{productId}/availability`), not an event —
listed here for completeness since it's the other way this service talks to Warehouse.

- Warehouse (RabbitMQ `warehouse_exchange`): `InventoryReservedEvent`, `InventoryInsufficientEvent`
- Payment (Kafka `payment-topic`): `PaymentProcessedEvent` (drives the fulfillment saga and triggers Commission), `PaymentFailedEvent`
- Shipping (Kafka `shipping-topic`): `ShipmentCreatedEvent`
- Product (Kafka `product-topic`): `ProductPriceChangedDomainEvent` (flags open orders holding the product — never retroactively changes the snapshotted `UnitPrice`)
- SharedMasterDataService (Kafka `mst-topic`): `SettingsChangedIntegrationEvent` filtered to Currency settings (cache-invalidation only, no visible effect yet)

## Database Schema

### Key Tables

**SalesOrders**
- Id (PK), CustomerId, CustomerName, CustomerEmail (denormalized — no local Customer entity anywhere in this system)
- OrderNo (unique, `SO-yyyyMM-00001`), OrderSource (Manual/Ecommerce/POS), DepartmentId, SalesRepId, QuotationId — all nullable/optional
- Status (Draft, Submitted, Approved, Picked, Shipped, PartiallyShipped, Delivered, Rejected, Cancelled, ReturnRequested, Returned)
- TotalAmount, PromotionId, DiscountAmount
- ShippingAddress (owned), TrackingNumber, Carrier
- CreatedAt/ApprovedAt/ShippedAt/DeliveredAt/CancelledAt, ReturnRequestedAt/ReturnedAt/ReturnReason/RefundAmount

**SalesOrderItems** (owned collection) — ProductId, ProductName, Quantity, UnitPrice (snapshot, never re-read from PriceList), ShippedQuantity

**OrderNotes**, **OrderStatusHistory** — owned collections (internal notes, full audit trail of every status transition)

**SalesQuotations** / **SalesQuotationItems** — CustomerId/Name/Email, QuotationNo (unique), Status (Draft/Sent/Accepted/Rejected/Expired/Converted), ValidUntil, TotalAmount, ConvertedOrderId; line items are a plain snapshot (no shipment tracking)

**CustomerSalesProfiles** — CustomerId (unique), CreditLimit, CurrentBalance (denormalized open exposure), UpdatedAt

**CommissionPlans** — Name, RateType (Percent), RateValue, IsActive

**CommissionRecords** — PlanId, SalesRepId, OrderId (unique — one record per order), Amount, Status (Pending/Approved/Paid), CalculatedAt

**OrderNumberSequences** — per-tenant/prefix/year/month counter backing `OrderNo`/`QuotationNo` generation (atomic `MERGE...OUTPUT`, not a plain read-then-update)

**OutboxMessages** — shared by two independent publish paths, discriminated purely by `Type`: `SettingsOutboxProcessor` (Kafka, Settings-changed events only) and `SalesOrderEventOutboxProcessor` (RabbitMQ, everything else — SalesOrder/SalesQuotation domain events)

## Order Workflow

```
Draft → Submitted → Approved → Picked → Shipped/PartiallyShipped → Delivered → ReturnRequested → Returned
         ↓                        ↓
      Rejected              Cancelled
```

## Quotation Workflow

```
Draft → Sent → Accepted → Converted (creates a SalesOrder, QuotationId set)
          ↓        
       Rejected / Expired
```

## Getting Started

### Prerequisites
- .NET 10 SDK
- SQL Server 2019+
- RabbitMQ
- Identity Service

### Running

```bash
# Run migrations
dotnet ef database update

# Start service
dotnet run

# Service runs on http://localhost:5002
```

## Testing

```bash
# Unit tests
dotnet test --filter "Category=Unit"

# Integration tests  
dotnet test --filter "Category=Integration"
```

## Development Notes

### Key Business Rules
- Every line's stock availability is checked against WareHouseService before an order is
  persisted or payment is initiated — fails closed (rejects order creation) if any line is
  short or WareHouseService can't be reached
- Order line prices are snapshotted at creation — never re-read from PriceList later, even if the catalog price changes (only flags `HasPriceChangeWarning`)
- `Approve` enforces the customer's credit limit, but only if a `CustomerSalesProfile` exists for them — no profile means no limit
- Commission is only created on an actual `PaymentProcessedEvent`, never on a merely-confirmed order
- A quotation must be `Accepted` (customer approval recorded) before it can convert to an order
- Cancelled orders release credit-limit exposure only if they had reached `Approved` first

### Explicitly out of scope for this service
- **Invoice** — AccountingService auto-creates one from `SalesOrderCreatedEvent`; do not build it here
- **Payment** — PaymentService's job; this service only consumes `PaymentProcessedEvent`/`PaymentFailedEvent`
- **Shipment/Delivery as a separate aggregate** — ShippingService owns tracking; this service only records `TrackingNumber`/`Carrier` on the order itself
- **Promotion rule engine** — PromotionService's job; this service only validates coupons and persists the outcome
- **Sales Forecast** — ReportingService's analytics/BI responsibility

## Dependencies
- SQL Server (SalesDB)
- RabbitMQ (`sales_order_events` exchange, `warehouse_exchange`)
- Kafka (`payment-topic`, `shipping-topic`, `product-topic`, `mst-topic`, `sale-order-settings-topic`)
- Identity Service (JWT validation)
- Warehouse Service (inventory reservation)
- Payment Service, Shipping Service, Promotion Service (client/consumer integration)

---

**Last Updated**: 2026-07-27
**Version**: 1.1.0 — Quotation, CustomerSalesProfile (credit limit), Commission, Outbox retrofit added on top of the original Order Management implementation.
