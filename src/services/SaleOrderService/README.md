# Sales Order Service

Manages customer sales orders, sales workflows, and order fulfillment processes for the ERP E-Commerce Solution.

## Overview

The Sales Order Service is responsible for the complete sales order lifecycle from creation to delivery. It coordinates with Warehouse Service for inventory management and publishes events for downstream processing.

**Key Functions:**
- Create and manage customer sales orders
- Track order status through fulfillment workflow
- Reserve inventory with Warehouse Service
- Manage customer information and order history
- Generate sales reports and analytics

## Responsibilities

### Order Management
- Create new sales orders with validation
- Edit pending orders
- Cancel orders and release inventory
- Track order status changes
- Manage order line items
- Validate item availability before approval

### Workflow Orchestration
- Process order approval workflow
- Coordinate with warehouse for fulfillment
- Handle order cancellation and refunds
- Manage order holds and exceptions
- Track order milestones

### Customer Management
- Maintain customer profiles
- Track customer credit limits
- Record customer preferences
- Monitor customer order history
- Manage customer communication

### Reporting & Analytics
- Generate sales reports
- Track sales metrics and KPIs
- Analyze customer purchasing patterns
- Monitor order fulfillment performance
- Generate invoice data

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

## Published Events

### SalesOrderCreatedEvent
Published when a new sales order is created. Triggers inventory reservation.

### SalesOrderApprovedEvent
Published when order is approved and ready for fulfillment.

### SalesOrderShippedEvent
Published when order is shipped to customer.

### SalesOrderCancelledEvent
Published when order is cancelled.

## Consumed Events

- `UserCreatedEvent` - Initialize customer profile
- `InventoryReservedEvent` - Confirm reservation successful
- `PaymentReceivedEvent` - Update payment status

## Database Schema

### Key Tables

**SalesOrders**
- OrderId (PK)
- CustomerId (FK)
- OrderDate
- Status (Draft, Submitted, Approved, Shipped, Delivered, Cancelled)
- TotalAmount
- ShippingAddress
- CreatedAt, ApprovedAt, ShippedAt, CancelledAt

**OrderItems**
- OrderItemId (PK)
- OrderId (FK)
- ProductId
- Quantity
- UnitPrice
- LineTotal

**Customers**
- CustomerId (PK)
- UserId (FK to Identity Service)
- Name, Email, Phone
- CreditLimit, CreditUsed

## Order Workflow

```
Draft → Submitted → Approved → Picked → Shipped → Delivered
         ↓                        ↓
      Rejected              Cancelled
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
- Orders can only be approved if inventory is available
- Customers cannot exceed credit limit
- Cancelled orders must release inventory
- Shipped orders cannot be edited

### Customization
- Add approval workflows
- Implement discounts/promotions
- Add payment terms
- Custom business rules

## Dependencies
- SQL Server (SalesDB)
- RabbitMQ (events)
- Identity Service (user validation)
- Warehouse Service (inventory)

---

**Last Updated**: 2026-06-13  
**Version**: 1.0.0
