# Sales Order Service API Documentation

## Overview

The Sales Order Service (Port 5002) manages the complete lifecycle of sales orders, from creation to delivery. It implements Clean Architecture, DDD, CQRS, and Event-Driven Architecture with comprehensive authentication and authorization.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         API Gateway (5000)                       │
│  (Routes requests, adds authentication headers, rate limits)     │
└───────────────────────────────┬─────────────────────────────────┘
                                │
                    ┌───────────┴───────────┐
                    │                       │
        ┌───────────▼────────────┐ ┌────────▼──────────────┐
        │   Identity Service     │ │ Sales Order Service   │
        │       (5001)           │ │      (5002)           │
        └────────────────────────┘ └───┬──────────────┬────┘
                                       │              │
                        ┌──────────────┴──┐    ┌──────┴────────┐
                        │                 │    │               │
                    ┌───▼──┐      ┌──────▼─┐  │ ┌────────────┐ │
                    │ RabbitMQ    │ Database│  │ │ EF Core    │ │
                    │ (Events)    │ (SQL)   │  │ │ DbContext  │ │
                    └────────┘    └────────┘  │ └────────────┘ │
                                              │                │
                        ┌─────────────────────┤ Consumers:    │
                        │                     │ - Warehouse   │
                        ▼                     │ - Accounting  │
                    ┌────────────┐            │ - Shipping    │
                    │ Warehouse  │            │ - CRM         │
                    │ Service    │            │ - Reporting   │
                    │ (5004)     │            │               │
                    └────────────┘            └───────────────┘
```

## Authentication & Authorization

### Authentication Flow

1. **User Login** (Identity Service):
   ```
   POST /api/v1/auth/login
   Body: { email, password }
   Response: { token, userId, roles, permissions, expiresAt }
   ```

2. **Request to Sales Order Service**:
   ```
   GET /api/v1/orders
   Headers:
     X-User-Id: <userId>
     X-User-Name: <userName>
     X-Tenant-Id: <tenantId>
     X-Tenant-Name: <tenantName>
     X-Roles: Sales_Manager,Sales_User
     X-Permissions: sales_order:read,sales_order:create
     X-Token-Expires: <unix_timestamp>
   ```

### Authorization Rules

Permissions are checked at the controller level:

| Permission | Description | Endpoint |
|-----------|-------------|----------|
| `sales_order:create` | Create new orders | POST /api/v1/orders |
| `sales_order:read` | View orders | GET /api/v1/orders/* |
| `sales_order:update` | Update orders, ship, mark delivered | POST /api/v1/orders/{id}/ship, /mark-delivered |
| `sales_order:approve` | Approve submitted orders | POST /api/v1/orders/{id}/approve |
| `sales_order:cancel` | Cancel orders | POST /api/v1/orders/{id}/cancel |

### User Context

The `UserContext` is injected into controllers and contains:

```csharp
public class UserContext
{
    public string UserId { get; set; }
    public string UserName { get; set; }
    public string TenantId { get; set; }
    public string TenantName { get; set; }
    public string[] Roles { get; set; }
    public string[] Permissions { get; set; }
    public DateTime ExpiresAt { get; set; }
    
    public bool IsAuthenticated { get; }
    public bool IsTokenExpired { get; }
    public bool HasPermission(string permission);
    public bool HasAllPermissions(params string[] permissions);
    public bool HasAnyPermission(params string[] permissions);
}
```

## API Endpoints

### 1. Create Sales Order

**Endpoint**: `POST /api/v1/orders`

**Required Permission**: `sales_order:create`

**Request**:
```json
{
  "customerId": "550e8400-e29b-41d4-a716-446655440000",
  "customerName": "John Doe",
  "customerEmail": "john.doe@example.com",
  "lineItems": [
    {
      "productId": "660e8400-e29b-41d4-a716-446655440000",
      "productName": "Product A",
      "quantity": 5,
      "unitPrice": 99.99
    }
  ],
  "shippingAddress": {
    "street": "123 Main St",
    "city": "New York",
    "state": "NY",
    "postalCode": "10001",
    "country": "US"
  },
  "notes": "Urgent delivery needed"
}
```

**Response** (201 Created):
```json
{
  "orderId": "770e8400-e29b-41d4-a716-446655440000",
  "customerId": "550e8400-e29b-41d4-a716-446655440000",
  "customerName": "John Doe",
  "customerEmail": "john.doe@example.com",
  "status": "Draft",
  "totalAmount": 499.95,
  "orderDate": "2026-06-14T10:30:00Z",
  "approvedAt": null,
  "shippedAt": null,
  "deliveredAt": null,
  "cancelledAt": null,
  "lineItems": [
    {
      "productId": "660e8400-e29b-41d4-a716-446655440000",
      "productName": "Product A",
      "quantity": 5,
      "unitPrice": 99.99
    }
  ],
  "shippingAddress": {
    "street": "123 Main St",
    "city": "New York",
    "state": "NY",
    "postalCode": "10001",
    "country": "US"
  },
  "notes": "Urgent delivery needed"
}
```

**Data Integrity Checks**:
- ✓ Customer ID must be valid (GUID)
- ✓ Customer name must be ≤200 characters
- ✓ Email must be valid format
- ✓ At least one line item required
- ✓ Product quantities must be > 0
- ✓ Unit prices must be > 0
- ✓ Shipping address required and complete
- ✓ Total amount calculated from line items (no manipulation possible)

**Domain Events Published**:
- `SalesOrderCreatedEvent` → Triggers warehouse inventory reservation

---

### 2. Get Order by ID

**Endpoint**: `GET /api/v1/orders/{id}`

**Required Permission**: `sales_order:read`

**Path Parameters**:
- `id` (Guid): Order ID

**Response** (200 OK): Same as Create endpoint

**Error Responses**:
- 404 Not Found: Order does not exist
- 401 Unauthorized: Missing authentication
- 403 Forbidden: No read permission

---

### 3. List Orders (with Pagination & Filtering)

**Endpoint**: `GET /api/v1/orders?customerId=&status=&skip=0&take=20`

**Required Permission**: `sales_order:read`

**Query Parameters**:
- `customerId` (Guid, optional): Filter by customer
- `status` (string, optional): Filter by status (Draft, Submitted, Approved, Picked, Shipped, Delivered, Rejected, Cancelled)
- `skip` (int, default=0): Number of records to skip
- `take` (int, default=20): Number of records to return (max=100)

**Response** (200 OK):
```json
{
  "total": 150,
  "items": [
    {
      "orderId": "770e8400-e29b-41d4-a716-446655440000",
      "customerId": "550e8400-e29b-41d4-a716-446655440000",
      "customerName": "John Doe",
      "status": "Approved",
      "totalAmount": 499.95,
      "orderDate": "2026-06-14T10:30:00Z"
    }
  ]
}
```

---

### 4. Get Orders by Customer

**Endpoint**: `GET /api/v1/orders/customer/{customerId}`

**Required Permission**: `sales_order:read`

**Path Parameters**:
- `customerId` (Guid): Customer ID

**Response** (200 OK):
```json
[
  {
    "orderId": "770e8400-e29b-41d4-a716-446655440000",
    "customerId": "550e8400-e29b-41d4-a716-446655440000",
    "customerName": "John Doe",
    "status": "Approved",
    "totalAmount": 499.95,
    "orderDate": "2026-06-14T10:30:00Z"
  }
]
```

---

### 5. Submit Order

**Endpoint**: `POST /api/v1/orders/{id}/submit`

**Required Permission**: `sales_order:update`

**Path Parameters**:
- `id` (Guid): Order ID

**Preconditions**:
- Order must be in "Draft" status
- Must have at least one line item
- Total amount must be > 0

**Response** (200 OK): Order with status = "Submitted"

**Error Responses**:
- 409 Conflict: Order not in Draft status

---

### 6. Approve Order

**Endpoint**: `POST /api/v1/orders/{id}/approve`

**Required Permission**: `sales_order:approve`

**Path Parameters**:
- `id` (Guid): Order ID

**Preconditions**:
- Order must be in "Submitted" status
- Inventory must be available (checked by Warehouse Service)

**Response** (200 OK): Order with status = "Approved"

**Domain Events Published**:
- `SalesOrderApprovedEvent` → Warehouse fulfills, Accounting generates invoice

**Error Responses**:
- 409 Conflict: Order not in Submitted status
- 400 Bad Request: Inventory unavailable (handled by orchestration)

---

### 7. Ship Order

**Endpoint**: `POST /api/v1/orders/{id}/ship`

**Required Permission**: `sales_order:update`

**Path Parameters**:
- `id` (Guid): Order ID

**Request Body**:
```json
{
  "trackingNumber": "FEDEX123456789",
  "carrier": "FedEx"
}
```

**Preconditions**:
- Order must be in "Picked" status
- Tracking number must not be empty
- Carrier must not be empty

**Response** (200 OK): Order with status = "Shipped"

**Domain Events Published**:
- `SalesOrderShippedEvent` → Warehouse updates, CRM notifies customer, Reporting updates metrics

**Error Responses**:
- 409 Conflict: Order not in Picked status

---

### 8. Cancel Order

**Endpoint**: `POST /api/v1/orders/{id}/cancel`

**Required Permission**: `sales_order:cancel`

**Path Parameters**:
- `id` (Guid): Order ID

**Request Body**:
```json
{
  "reason": "Customer requested cancellation"
}
```

**Cancellable Statuses**:
- Draft
- Submitted
- Approved
- Picked

**Non-Cancellable Statuses**:
- Shipped (physical goods in transit)
- Delivered (customer has received)
- Cancelled (already cancelled)

**Response** (200 OK): Order with status = "Cancelled"

**Domain Events Published**:
- `SalesOrderCancelledEvent` → Warehouse releases reserved inventory, Accounting issues refund

**Error Responses**:
- 409 Conflict: Order in non-cancellable status

---

### 9. Mark Order as Delivered

**Endpoint**: `POST /api/v1/orders/{id}/mark-delivered`

**Required Permission**: `sales_order:update`

**Path Parameters**:
- `id` (Guid): Order ID

**Preconditions**:
- Order must be in "Shipped" status

**Response** (200 OK): Order with status = "Delivered"

**Error Responses**:
- 409 Conflict: Order not in Shipped status

---

## Order Status Workflow

```
┌─────────┐
│ Draft   │ ← Initial state after creation
└────┬────┘
     │ submit()
     ▼
┌─────────┐
│Submitted│ ← Ready for approval
└────┬────┘
     ├─ approve() → ┌──────────┐
     │              │ Approved │ ← Ready for fulfillment
     │              └────┬─────┘
     │                   │
     │              picked() [auto by warehouse]
     │                   │
     │              ┌────▼─────┐
     │              │ Picked    │ ← Ready to ship
     │              └────┬─────┘
     │                   │
     │              ship() → ┌────────┐
     │                       │ Shipped│ ← In transit
     │                       └────┬───┘
     │                            │
     │                      markDelivered()
     │                            │
     │                       ┌────▼────┐
     │                       │Delivered│ ← Final state
     │                       └─────────┘
     │
     └─ reject() → ┌────────┐
                   │Rejected│
                   └────────┘

cancel() can be called from:
Draft, Submitted, Approved, Picked (releases inventory, issues refund)
```

## Integration with Other Services

### 1. Warehouse Service (5004)

**Events Published to Warehouse**:
- `SalesOrderCreatedEvent` → Warehouse reserves inventory
- `SalesOrderApprovedEvent` → Warehouse confirms reservation
- `SalesOrderCancelledEvent` → Warehouse releases reservation

**Events Consumed from Warehouse**:
- `InventoryReservedEvent` → Order can proceed
- `InventoryReservedFailedEvent` → Order approval rejected

---

### 2. Accounting Service (5005)

**Events Published to Accounting**:
- `SalesOrderCreatedEvent` → Log sales transaction
- `SalesOrderApprovedEvent` → Generate invoice, record AR
- `SalesOrderCancelledEvent` → Issue refund, reverse AR

**Events Consumed from Accounting**:
- `InvoiceGeneratedEvent` → Update order with invoice ID
- `PaymentReceivedEvent` → Update order payment status

---

### 3. Shipping Service (5010)

**Events Published to Shipping**:
- `SalesOrderApprovedEvent` → Request shipment creation
- `SalesOrderCancelledEvent` → Cancel shipment if created

**Events Consumed from Shipping**:
- `ShipmentCreatedEvent` → Store tracking info
- `TrackingUpdatedEvent` → Update order status

---

### 4. CRM Service (5006)

**Events Consumed from CRM**:
- Receives `SalesOrderCreatedEvent` → Create customer record if new
- Receives `SalesOrderShippedEvent` → Send customer notification
- Receives `SalesOrderCancelledEvent` → Log cancellation reason

---

### 5. Reporting Service (5014)

**Events Consumed by Reporting** (read-only analytics):
- All events (SalesOrderCreatedEvent, ApprovedEvent, ShippedEvent, CancelledEvent)
- Populates fact tables for BI/analytics

---

## Data Integrity & Consistency

### Event Sourcing with Outbox Pattern

```
┌──────────────────────────────────────┐
│ SalesOrderService                    │
├──────────────────────────────────────┤
│ Domain Event:                        │
│   - SalesOrderCreatedEvent           │
│   - SalesOrderApprovedEvent          │
│   - SalesOrderShippedEvent           │
│   - SalesOrderCancelledEvent         │
└────────────┬────────────┬────────────┘
             │            │
             ▼            ▼
      ┌──────────┐  ┌──────────┐
      │ OutboxDB │  │ SalesDB  │
      │ (Queue)  │  │ (State)  │
      └────┬─────┘  └──────────┘
           │
      ┌────▼────────────────┐
      │ RabbitMQ Publisher  │ ← Guaranteed delivery
      │ (Background Service)│
      └─────┬───────────────┘
            │
      ┌─────▼──────────────────────┐
      │ Consumer Services:          │
      │ - Warehouse                 │
      │ - Accounting                │
      │ - Shipping                  │
      │ - CRM                       │
      │ - Reporting                 │
      └────────────────────────────┘
```

### Transaction Guarantees

- **Write to DB + Outbox**: Single transaction (ACID)
- **Idempotent Event Publishing**: Events include EventId for deduplication
- **Ordered Event Processing**: Same order events processed in order
- **Compensating Transactions**: Cancel order triggers refund in Accounting

### No Cross-Database Queries

✓ Sales Order DB is owned exclusively by Sales Order Service  
✓ Other services access via REST API only  
✓ No direct table access from other services  
✓ Data consistency through event choreography  

---

## Testing

### Example: Create and Approve Order

```bash
# 1. Create order
curl -X POST http://localhost:5002/api/v1/orders \
  -H "Content-Type: application/json" \
  -H "X-User-Id: 550e8400-e29b-41d4-a716-446655440001" \
  -H "X-User-Name: john_manager" \
  -H "X-Tenant-Id: 550e8400-e29b-41d4-a716-446655440000" \
  -H "X-Tenant-Name: ACME Corp" \
  -H "X-Roles: Sales_Manager" \
  -H "X-Permissions: sales_order:create,sales_order:read,sales_order:approve" \
  -H "X-Token-Expires: 1818282000" \
  -d '{
    "customerId": "550e8400-e29b-41d4-a716-446655440000",
    "customerName": "Jane Smith",
    "customerEmail": "jane@example.com",
    "lineItems": [
      {
        "productId": "660e8400-e29b-41d4-a716-446655440000",
        "productName": "Widget A",
        "quantity": 10,
        "unitPrice": 29.99
      }
    ],
    "shippingAddress": {
      "street": "456 Oak Ave",
      "city": "Boston",
      "state": "MA",
      "postalCode": "02101"
    }
  }'

# Response: Order created with ID "770e8400-..." and status "Draft"
# Domain Event Published: SalesOrderCreatedEvent → RabbitMQ

# 2. Get order details
curl -X GET http://localhost:5002/api/v1/orders/770e8400-e29b-41d4-a716-446655440000 \
  -H "X-User-Id: 550e8400-e29b-41d4-a716-446655440001" \
  -H "X-Permissions: sales_order:read" \
  -H "X-Token-Expires: 1818282000"

# 3. Approve order (after inventory reserved by Warehouse)
curl -X POST http://localhost:5002/api/v1/orders/770e8400-e29b-41d4-a716-446655440000/approve \
  -H "X-User-Id: 550e8400-e29b-41d4-a716-446655440001" \
  -H "X-Permissions: sales_order:approve" \
  -H "X-Token-Expires: 1818282000"

# Response: Order status = "Approved"
# Domain Event Published: SalesOrderApprovedEvent → RabbitMQ
# → Warehouse confirms reservation
# → Accounting generates invoice
# → Shipping creates shipment
```

---

## Error Handling

### HTTP Status Codes

| Code | Scenario |
|------|----------|
| 200 | Successful operation |
| 201 | Order created |
| 400 | Validation error (invalid input) |
| 401 | Unauthorized (missing/invalid auth) |
| 403 | Forbidden (insufficient permissions) |
| 404 | Order not found |
| 409 | Conflict (invalid state transition) |
| 500 | Server error |

### Example Error Response

```json
{
  "message": "Cannot approve order in Submitted status",
  "timestamp": "2026-06-14T10:35:00Z",
  "traceId": "0HN1GD7A63P5V:00000001"
}
```

---

## Quotations, Credit Limit & Commission (added 2026-07-27)

### Permissions

Reuses the same `sales_order:*` permission strings as Orders — no dedicated Quotation/Commission/CustomerSalesProfile permission constants exist yet.

| Permission | Endpoints |
|-----------|-------------|
| `sales_order:create` | `POST /api/v1/quotations`, `POST /api/v1/quotations/{id}/convert-to-order` |
| `sales_order:read` | `GET /api/v1/quotations*`, `GET /api/v1/customer-sales-profiles/{customerId}`, `GET /api/v1/commission-plans`, `GET /api/v1/commissions` |
| `sales_order:update` | `POST /api/v1/quotations/{id}/send`, `/accept`, `/reject` |
| `sales_order:approve` | `PUT /api/v1/customer-sales-profiles/{customerId}`, `POST /api/v1/commission-plans`, `POST /api/v1/commission-plans/{id}/deactivate` |

### Quotations

```http
POST /api/v1/quotations
Authorization: Bearer <token>

{
  "customerId": "550e8400-e29b-41d4-a716-446655440000",
  "customerName": "John Doe",
  "customerEmail": "john@example.com",
  "lineItems": [ { "productId": "...", "productName": "Product A", "quantity": 5, "unitPrice": 99.99 } ],
  "validUntil": "2026-08-15T00:00:00Z"
}

Response: 201 Created — status "Draft", quotationNo "QT-202607-00001"
```

```http
POST /api/v1/quotations/{id}/send      # Draft -> Sent
POST /api/v1/quotations/{id}/accept    # Sent -> Accepted (409 if past validUntil)
POST /api/v1/quotations/{id}/reject    # Sent -> Rejected

POST /api/v1/quotations/{id}/convert-to-order
{ "shippingAddress": { "street": "...", "city": "...", "state": "...", "postalCode": "...", "country": "US" } }

Response: 200 OK — a real SalesOrderDto, orderSource "Manual", quotationId set to the source quotation.
409 if the quotation is not in Accepted status.
```

### Credit Limit

```http
GET /api/v1/customer-sales-profiles/{customerId}
Response: 200 OK { "customerId": "...", "creditLimit": 50000, "currentBalance": 12500, "updatedAt": "..." }
Response: 404 — no profile configured, treated as "no limit enforced", not an error condition for Approve.

PUT /api/v1/customer-sales-profiles/{customerId}
{ "creditLimit": 50000 }
Response: 200 OK — creates the profile if none exists, otherwise updates CreditLimit only (CurrentBalance is server-managed).
```

`POST /api/v1/orders/{id}/approve` now returns `409 Conflict` if `CurrentBalance + TotalAmount > CreditLimit` for a customer that has a profile:
```json
{ "message": "Order exceeds customer's credit limit ($50,000.00); current open balance $48,000.00 + this order $5,000.00." }
```

### Commission

```http
GET /api/v1/commission-plans
POST /api/v1/commission-plans
{ "name": "Standard 5%", "rateValue": 0.05 }
POST /api/v1/commission-plans/{id}/deactivate

GET /api/v1/commissions?salesRepId=&from=&to=
```

`CommissionRecord`s are created automatically — never via a direct POST — when a `PaymentProcessedEvent` (Kafka `payment-topic`) arrives for an order that has `salesRepId` set and at least one active `CommissionPlan` exists. One record per order (idempotent against redelivery).

---

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "SalesDb": "Server=(local);Database=SalesDB;..."
  },
  "RabbitMQ": {
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "guest",
    "Password": "guest"
  }
}
```

### Database Migrations

```bash
# Add migration
dotnet ef migrations add AddSalesOrderTables

# Update database
dotnet ef database update

# Drop database (dev only)
dotnet ef database drop
```

---

## Performance Considerations

- **Indexes**: CustomerId, Status, OrderDate
- **Pagination**: Default take=20, max=100
- **Caching**: Consider Redis for frequently accessed orders
- **Batch Operations**: Not supported; create orders individually
- **Soft Deletes**: Not implemented; use Cancel instead

---

## Security

- ✓ All endpoints require authentication
- ✓ All endpoints require explicit permissions
- ✓ Input validation via FluentValidation
- ✓ SQL injection prevention (EF Core parameterized queries)
- ✓ XSS prevention (returns JSON, not HTML)
- ✓ CORS enabled (configurable)
- ✓ HTTPS required in production
- ✓ Token expiration validation

---

**Last Updated**: 2026-07-27
**Version**: 1.1.0 — added Quotations, Credit Limit, Commission endpoints and the Outbox-based reliable event publishing retrofit.
