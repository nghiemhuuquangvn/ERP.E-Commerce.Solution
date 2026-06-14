# Purchase Order Service

Manages supplier purchase orders, purchasing workflows, and procurement processes for the ERP E-Commerce Solution.

## Overview

The Purchase Order Service handles all purchasing operations from supplier order creation to goods receipt and invoicing. It coordinates with Warehouse Service for inventory replenishment and maintains supplier relationships.

**Key Functions:**
- Create and manage supplier purchase orders
- Track purchase order status through approval and receipt workflow
- Manage supplier information and performance metrics
- Process goods receipt and quality inspection
- Generate purchasing reports and analytics

## Responsibilities

### Purchase Order Management
- Create new purchase orders with supplier validation
- Edit pending purchase orders
- Request quotes from suppliers
- Approve and confirm orders with suppliers
- Cancel orders and process refunds
- Track purchase order status changes

### Workflow Orchestration
- Process purchase approval workflow
- Coordinate goods receipt with warehouse
- Handle goods inspection and acceptance/rejection
- Manage receiving exceptions
- Process supplier invoices

### Supplier Management
- Maintain supplier profiles and contact info
- Track supplier performance metrics
- Manage supplier agreements and terms
- Monitor supplier ratings and reviews
- Process supplier communications

### Procurement Analytics
- Generate purchasing reports
- Track procurement KPIs
- Analyze supplier performance
- Monitor cost trends
- Generate budget forecasts

## Architecture

### Technology Stack
- **Framework**: ASP.NET Core 10
- **ORM**: Entity Framework Core
- **Database**: SQL Server
- **Patterns**: DDD, CQRS (MediatR), Event-Driven
- **Message Bus**: RabbitMQ
- **Caching**: Redis (optional)

### Project Structure
```
PurchaseOrderService/
├── API/                         # REST endpoints
├── Application/                 # Commands, Queries, Handlers
├── Domain/                      # Entities, aggregates
├── Infrastructure/              # Repositories, external services
├── Shared/                      # DTOs
└── Tests/
```

## API Endpoints

### Purchase Order Management

#### Create Purchase Order
```http
POST /api/v1/purchase-orders
Authorization: Bearer <token>
Content-Type: application/json

{
  "supplierId": "550e8400-e29b-41d4-a716-446655440000",
  "orderDate": "2026-06-13T00:00:00Z",
  "expectedDeliveryDate": "2026-06-20T00:00:00Z",
  "lineItems": [
    {
      "productId": "660e8400-e29b-41d4-a716-446655440000",
      "quantity": 100,
      "unitPrice": 45.00
    }
  ],
  "deliveryAddress": {
    "street": "456 Warehouse Ave",
    "city": "Chicago",
    "state": "IL",
    "zipCode": "60601"
  },
  "notes": "Rush order - needed by June 20"
}

Response: 201 Created
{
  "purchaseOrderId": "770e8400-e29b-41d4-a716-446655440000",
  "supplierId": "550e8400-e29b-41d4-a716-446655440000",
  "status": "Draft",
  "totalAmount": 4500.00,
  "createdAt": "2026-06-13T10:30:00Z"
}
```

#### Get Purchase Order
```http
GET /api/v1/purchase-orders/{purchaseOrderId}
Authorization: Bearer <token>

Response: 200 OK
{
  "purchaseOrderId": "770e8400-e29b-41d4-a716-446655440000",
  "supplierId": "550e8400-e29b-41d4-a716-446655440000",
  "supplierName": "Supplier Inc.",
  "status": "Approved",
  "totalAmount": 4500.00,
  "lineItems": [ ... ],
  "approvedAt": "2026-06-13T11:00:00Z"
}
```

#### Approve Purchase Order
```http
POST /api/v1/purchase-orders/{purchaseOrderId}/approve
Authorization: Bearer <token>

Response: 200 OK
{
  "purchaseOrderId": "770e8400-e29b-41d4-a716-446655440000",
  "status": "Approved",
  "approvedAt": "2026-06-13T11:00:00Z"
}
```

#### Receive Purchase Order
```http
POST /api/v1/purchase-orders/{purchaseOrderId}/receive
Authorization: Bearer <token>
Content-Type: application/json

{
  "receivedItems": [
    {
      "productId": "660e8400-e29b-41d4-a716-446655440000",
      "quantityReceived": 100,
      "quantityAccepted": 98,
      "quantityRejected": 2,
      "notes": "2 units damaged in shipping"
    }
  ],
  "invoiceNumber": "SUP-2026-001234"
}

Response: 200 OK
```

#### List Purchase Orders
```http
GET /api/v1/purchase-orders?status=Approved&supplierId=550e8400&skip=0&take=20
Authorization: Bearer <token>

Response: 200 OK
{
  "total": 45,
  "items": [ ... ]
}
```

### Supplier Endpoints

#### List Suppliers
```http
GET /api/v1/suppliers?search=inc&skip=0&take=20
Authorization: Bearer <token>

Response: 200 OK
{
  "total": 15,
  "items": [
    {
      "supplierId": "550e8400-e29b-41d4-a716-446655440000",
      "name": "Supplier Inc.",
      "email": "contact@supplier.com",
      "phone": "+1-555-0100",
      "rating": 4.5,
      "totalOrders": 25,
      "totalSpent": 125000.00
    }
  ]
}
```

## Published Events

### PurchaseOrderCreatedEvent
Published when a new purchase order is created.

```csharp
public class PurchaseOrderCreatedEvent : IntegrationEvent
{
    public Guid PurchaseOrderId { get; set; }
    public Guid SupplierId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public List<PurchaseLineItem> LineItems { get; set; }
    public DateTime ExpectedDeliveryDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Subscribers**: Warehouse Service (receiving preparation)

### PurchaseOrderApprovedEvent
Published when purchase order is approved.

```csharp
public class PurchaseOrderApprovedEvent : IntegrationEvent
{
    public Guid PurchaseOrderId { get; set; }
    public Guid SupplierId { get; set; }
    public DateTime ApprovedAt { get; set; }
    public decimal TotalAmount { get; set; }
}
```

### PurchaseOrderReceivedEvent
Published when goods are received and inspected.

```csharp
public class PurchaseOrderReceivedEvent : IntegrationEvent
{
    public Guid PurchaseOrderId { get; set; }
    public Guid SupplierId { get; set; }
    public List<ReceivedLineItem> ReceivedItems { get; set; }
    public DateTime ReceivedAt { get; set; }
}
```

**Subscribers**: Warehouse Service (inventory update), Accounting Service

## Consumed Events

- `UserCreatedEvent` - Initialize supplier profile
- `InventoryUpdatedEvent` - Monitor stock levels and trigger reorders

## Database Schema

### Key Tables

**PurchaseOrders**
- PurchaseOrderId (PK)
- SupplierId (FK)
- OrderDate
- Status (Draft, Submitted, Approved, Receiving, Closed)
- TotalAmount
- ExpectedDeliveryDate
- DeliveryAddress
- ApprovedAt, ReceivedAt

**PurchaseOrderItems**
- PurchaseOrderItemId (PK)
- PurchaseOrderId (FK)
- ProductId
- Quantity
- UnitPrice
- QuantityReceived
- QuantityAccepted
- QuantityRejected

**Suppliers**
- SupplierId (PK)
- UserId (FK to Identity Service)
- Name, Email, Phone
- ContactPerson
- Address
- PaymentTerms
- IsActive

## Purchase Workflow

```
Draft → Submitted → Approved → Receiving → Closed
         ↓                        ↓
      Rejected              Partially Received
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

# Service runs on http://localhost:5003
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
- Orders require supplier approval before confirmation
- Received quantities must be <= ordered quantities
- Rejected items trigger refund process
- Purchase orders locked after approval

### Customization
- Add approval workflows
- Implement RFQ (Request for Quote) process
- Add supplier rating system
- Custom business rules

## Dependencies
- SQL Server (PurchasingDB)
- RabbitMQ (events)
- Identity Service (user validation)
- Warehouse Service (inventory)

---

**Last Updated**: 2026-06-13  
**Version**: 1.0.0
