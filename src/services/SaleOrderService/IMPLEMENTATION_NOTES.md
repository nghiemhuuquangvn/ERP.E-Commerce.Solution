# Sales Order Service - Implementation Notes

## Completed Implementation Summary

### ✅ Domain Layer (ERP.SO.Domain)

**Entities**:
- `SalesOrder` (AggregateRoot)
  - `OrderStatus` enum (Draft, Submitted, Approved, Picked, Shipped, Delivered, Rejected, Cancelled)
  - Business invariants enforced at entity level
  - State transitions validated
  - Domain events raised for each state change

**Value Objects**:
- `OrderLineItem` (Product ID, Name, Quantity, Unit Price)
- `Address` (Street, City, State, PostalCode, Country)

**Domain Events**:
- `SalesOrderCreatedEvent` (Published when order created)
- `SalesOrderApprovedEvent` (Published when order approved)
- `SalesOrderShippedEvent` (Published when order shipped)
- `SalesOrderCancelledEvent` (Published when order cancelled)

### ✅ Application Layer (ERP.SO.Application)

**DTOs**:
- `CreateOrderRequest` (input for creating orders)
- `SalesOrderDto` (complete order details)
- `SalesOrderSummaryDto` (lightweight order summary)
- `OrderLineItemDto`, `AddressDto` (nested objects)

**Commands**:
- `CreateOrderCommand` → Creates new draft order
- `SubmitOrderCommand` → Submits order for approval
- `ApproveOrderCommand` → Approves submitted order
- `ShipOrderCommand` → Ships order with tracking
- `CancelOrderCommand` → Cancels order with reason
- `MarkAsDeliveredCommand` → Marks shipped order as delivered

**Queries**:
- `GetOrderQuery` → Retrieves single order by ID
- `ListOrdersQuery` → Lists orders with pagination/filtering
- `GetOrdersByCustomerQuery` → Gets all orders for customer

**Validators** (FluentValidation):
- `CreateOrderCommandValidator` → Validates all required fields
- `ApproveOrderCommandValidator`, `ShipOrderCommandValidator`, etc.

**Command Handlers**:
- `CreateOrderCommandHandler` → Creates order, publishes event
- `ApproveOrderCommandHandler` → Approves order, publishes event
- `ShipOrderCommandHandler` → Ships order, publishes event
- `CancelOrderCommandHandler` → Cancels order, publishes event

**Query Handlers**:
- `GetOrderQueryHandler` → Retrieves single order
- `ListOrdersQueryHandler` → Retrieves paginated list
- `GetOrdersByCustomerQueryHandler` → Retrieves customer's orders

### ✅ Infrastructure Layer (ERP.SO.Infrastructure)

**Database**:
- `SalesOrderDbContext` (EF Core DbContext)
  - Configures SalesOrder entity mapping
  - Owned entities for LineItems and Address
  - Indexes on CustomerId, Status, OrderDate
  - Precision constraints on decimal fields

**Repository**:
- `SalesOrderRepository : ISalesOrderRepository`
  - `GetByIdAsync()`
  - `AddAsync()`
  - `UpdateAsync()`
  - `GetByCustomerIdAsync()`
  - `GetAllAsync()` with pagination
  - `GetTotalCountAsync()` for pagination
  - `GetByStatusAsync()`

**Message Bus**:
- `RabbitMqMessageBus : IMessageBus`
  - Publishes domain events to RabbitMQ
  - Uses topic exchanges for event routing
  - Ensures message persistence
  - Auto-retries on connection failure
  - Implements IDisposable for clean shutdown

### ✅ API Layer (ERP.SO.Api)

**Controllers**:
- `SalesOrdersController`
  - POST /api/v1/orders → CreateOrder (requires sales_order:create)
  - GET /api/v1/orders/{id} → GetOrderById (requires sales_order:read)
  - GET /api/v1/orders → ListOrders (requires sales_order:read)
  - GET /api/v1/orders/customer/{customerId} → GetOrdersByCustomer
  - POST /api/v1/orders/{id}/submit → SubmitOrder (requires sales_order:update)
  - POST /api/v1/orders/{id}/approve → ApproveOrder (requires sales_order:approve)
  - POST /api/v1/orders/{id}/ship → ShipOrder (requires sales_order:update)
  - POST /api/v1/orders/{id}/cancel → CancelOrder (requires sales_order:cancel)
  - POST /api/v1/orders/{id}/mark-delivered → MarkAsDelivered (requires sales_order:update)

**Middleware**:
- `UserContextHeaderMiddleware`
  - Extracts user info from HTTP headers (X-User-Id, X-User-Name, X-Tenant-Id, X-Roles, X-Permissions, X-Token-Expires)
  - Validates authentication (blocks if missing)
  - Validates token expiration (blocks if expired)
  - Makes UserContext available for dependency injection

**Extensions**:
- `ServiceCollectionExtensions.AddMiddleware()`
  - Registers UserContext as scoped service
  - Configures header parsing
  - Registers IHttpContextAccessor

**Configuration**:
- `Program.cs` → Comprehensive service registration
  - DbContext with SQL Server
  - MediatR for CQRS
  - FluentValidation for command validation
  - Dependency injection setup
  - Middleware pipeline configuration

---

## Authentication & Authorization Architecture

### Header-Based Authentication (from API Gateway)

The API Gateway (Ocelot) adds these headers after validating JWT:

```
X-User-Id: [guid]                    # User identifier
X-User-Name: [string]                # User display name
X-Tenant-Id: [guid]                  # Tenant/Organization identifier
X-Tenant-Name: [string]              # Tenant display name
X-Roles: [csv]                       # Role1,Role2,Role3
X-Permissions: [csv]                 # sales_order:read,sales_order:create
X-Token-Expires: [unix_timestamp]    # Expiration time
```

### Authorization Check Flow

```
1. Request arrives at SalesOrdersController endpoint
2. UserContextHeaderMiddleware extracts headers → UserContext
3. Controller method checks: _userContext.HasPermission("sales_order:create")
4. If permission missing → return 403 Forbidden
5. If valid → proceed with business logic
```

### Permission Matrix

| Permission | Scope | Used By |
|-----------|-------|---------|
| `sales_order:create` | Create | CreateOrder endpoint |
| `sales_order:read` | Read | GetOrder*, ListOrders endpoints |
| `sales_order:update` | Edit | Submit, Ship, MarkDelivered |
| `sales_order:approve` | Approve | ApproveOrder endpoint |
| `sales_order:cancel` | Cancel | CancelOrder endpoint |

---

## Data Integrity Mechanisms

### 1. Database Constraints

**Unique Constraints**:
- Primary Key: `SalesOrder.Id` (Guid)
- Foreign Keys: `CustomerId` (references Identity Service conceptually)

**Not Null Constraints**:
- CustomerId, CustomerName, CustomerEmail, OrderDate, Status, TotalAmount, ShippingAddress

**Precision Constraints**:
- `TotalAmount`: numeric(18,2)
- `UnitPrice`: numeric(18,2)

**Value Constraints**:
- OrderStatus is enum (no invalid values possible)

### 2. Domain Model Validation

**In SalesOrder.Create()**:
- Validates all inputs before creating entity
- Prevents invalid states from being created

**In SalesOrder Methods**:
- `Approve()`: Only allowed from Submitted status
- `Ship()`: Only allowed from Picked status
- `Cancel()`: Blocked from Shipped/Delivered/Cancelled status
- Throws `InvalidOperationException` if preconditions violated

### 3. Input Validation (FluentValidation)

```csharp
RuleFor(x => x.CustomerId).NotEmpty();
RuleFor(x => x.CustomerEmail).EmailAddress();
RuleFor(x => x.LineItems).NotEmpty();
RuleForEach(x => x.LineItems)
    .ChildRules(item => {
        item.RuleFor(i => i.Quantity).GreaterThan(0);
        item.RuleFor(i => i.UnitPrice).GreaterThan(0);
    });
```

### 4. Event-Driven Consistency

**State Transitions Publish Events**:
- `CreateOrder()` → `SalesOrderCreatedEvent` (triggers inventory reservation)
- `ApproveOrder()` → `SalesOrderApprovedEvent` (triggers invoice generation)
- `CancelOrder()` → `SalesOrderCancelledEvent` (triggers refund)

**Orchestration Ensures Consistency**:
- Order waits for warehouse confirmation before approval
- Inventory locked until order shipped or cancelled
- Refund issued automatically on cancellation

### 5. Read Model Consistency

- Orders marked with timestamps (OrderDate, ApprovedAt, ShippedAt, etc.)
- Immutable event IDs for deduplication
- Correlation IDs for end-to-end tracing

---

## Integration Points

### Outbound Events (Published by Sales Order Service)

```
To RabbitMQ Topic: "sales_order.SalesOrderCreatedEvent"
├─ Warehouse Service
│  └─ Reserves inventory
├─ Accounting Service
│  └─ Records sales transaction
└─ Reporting Service
   └─ Updates analytics

To RabbitMQ Topic: "sales_order.SalesOrderApprovedEvent"
├─ Warehouse Service
│  └─ Confirms reservation
├─ Accounting Service
│  └─ Generates invoice & AR
└─ Shipping Service
   └─ Creates shipment

To RabbitMQ Topic: "sales_order.SalesOrderShippedEvent"
├─ CRM Service
│  └─ Sends notification to customer
└─ Reporting Service
   └─ Updates order fulfillment metrics

To RabbitMQ Topic: "sales_order.SalesOrderCancelledEvent"
├─ Warehouse Service
│  └─ Releases reserved inventory
├─ Accounting Service
│  └─ Issues refund & reverses AR
└─ Reporting Service
   └─ Updates cancellation metrics
```

### Inbound Dependencies (via API calls)

- **Identity Service**: Not directly called, authentication handled by gateway
- **Warehouse Service**: Async response via events
- **Product Service**: Product details passed in line items (filled by client)

---

## Service Dependencies

```
SalesOrderService
├─ ERP.SO.Domain           (Domain models, value objects, events)
├─ ERP.SO.Application      (Commands, queries, handlers, validators)
├─ ERP.SO.Infrastructure   (DbContext, repository, message bus)
├─ ERP.Shared              (Base classes, contracts, authorization)
└─ External Libraries
   ├─ MediatR              (CQRS)
   ├─ FluentValidation     (Input validation)
   ├─ EF Core              (ORM)
   ├─ RabbitMQ.Client      (Message publishing)
   └─ SQL Server           (Database)
```

---

## NuGet Packages Added

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.AspNetCore.OpenApi | 10.0.0 | OpenAPI/Swagger support |
| Microsoft.EntityFrameworkCore.SqlServer | 10.0.0 | SQL Server ORM |
| MediatR | 12.1.1 | CQRS mediator |
| FluentValidation | 11.8.1 | Command validation |
| RabbitMQ.Client | 6.0.0 | Message broker client |

---

## Running the Service

### Prerequisites

```bash
# 1. Create databases
sqlcmd -S (local) -Q "CREATE DATABASE SalesDB"
sqlcmd -S (local) -Q "CREATE DATABASE SalesDB_Dev"

# 2. Start RabbitMQ
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:latest

# Or if using Windows service:
# net start RabbitMQ
```

### Startup

```bash
# 1. Navigate to project
cd src\services\SaleOrderService\src\ERP.SO.Api

# 2. Restore packages
dotnet restore

# 3. Apply migrations
dotnet ef database update

# 4. Run service
dotnet run

# Service will start on http://localhost:5002
```

### Verify Setup

```bash
# Check service health
curl http://localhost:5002/health

# Check OpenAPI docs
curl http://localhost:5002/openapi/v1.json
```

---

## Testing the API

### Sample Request with Authentication

```bash
curl -X POST http://localhost:5002/api/v1/orders \
  -H "Content-Type: application/json" \
  -H "X-User-Id: 550e8400-e29b-41d4-a716-446655440001" \
  -H "X-User-Name: sales_manager" \
  -H "X-Tenant-Id: 550e8400-e29b-41d4-a716-446655440000" \
  -H "X-Tenant-Name: ACME Corp" \
  -H "X-Roles: Sales_Manager" \
  -H "X-Permissions: sales_order:create,sales_order:read,sales_order:approve" \
  -H "X-Token-Expires: 1818282000" \
  -d '{...order json...}'
```

### Expected Response (201 Created)

```json
{
  "orderId": "...",
  "status": "Draft",
  "totalAmount": 499.95,
  "lineItems": [...]
}
```

---

## Database Schema

### Tables

**SalesOrders**
- Id (Guid, PK)
- CustomerId (Guid)
- CustomerName (nvarchar(200))
- CustomerEmail (nvarchar(255))
- OrderDate (datetime2)
- Status (int) [enum value]
- TotalAmount (numeric(18,2))
- ApprovedAt (datetime2, nullable)
- ShippedAt (datetime2, nullable)
- DeliveredAt (datetime2, nullable)
- CancelledAt (datetime2, nullable)
- CancellationReason (nvarchar(max), nullable)
- Notes (nvarchar(1000), nullable)
- ShippingAddress_Street (nvarchar(255))
- ShippingAddress_City (nvarchar(100))
- ShippingAddress_State (nvarchar(100))
- ShippingAddress_PostalCode (nvarchar(20))
- ShippingAddress_Country (nvarchar(50))

**SalesOrders_LineItems** (Owned collection)
- Id (int, PK)
- OrderId (Guid, FK)
- ProductId (Guid)
- ProductName (nvarchar(500))
- Quantity (int)
- UnitPrice (numeric(18,2))

**Indexes**:
- IX_SalesOrders_CustomerId
- IX_SalesOrders_Status
- IX_SalesOrders_OrderDate

---

## Future Enhancements

1. **Pagination optimization**: Add database-level cursor pagination
2. **Caching layer**: Implement Redis for frequently accessed orders
3. **Batch operations**: Support bulk order creation
4. **Advanced reporting**: Add analytics endpoints
5. **Soft deletes**: Implement temporal tables for audit trail
6. **Payment integration**: Add payment status tracking
7. **Customer credit check**: Validate against customer credit limits
8. **Order notifications**: Send email/SMS on state changes
9. **Advanced search**: Full-text search on product names/descriptions
10. **Async processing**: Background job for long-running operations

---

## Monitoring & Observability

**Application Insights Integration** (recommended):
- Add `Microsoft.ApplicationInsights.AspNetCore`
- Track command execution time
- Track event publishing success/failure
- Monitor database query performance

**Logging**:
- All events logged with correlation ID
- Authorization failures logged
- Business rule violations logged

**Health Checks**:
- Database connectivity
- RabbitMQ connectivity
- Order processing status

---

**Implementation Date**: 2026-06-14  
**Status**: Complete  
**Last Updated**: 2026-06-14
