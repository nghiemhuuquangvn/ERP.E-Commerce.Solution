# Warehouse Service

Manages inventory, stock movements, and inventory reservations for the ERP E-Commerce Solution.

## Overview

The Warehouse Service is the central hub for inventory management. It provides real-time inventory visibility, manages reservations for sales orders, processes goods receipt, and maintains accurate stock levels.

**Key Functions:**
- Real-time inventory tracking
- Reserve inventory for sales orders
- Process goods receipt and quality inspection
- Record all stock movements (receive, issue, adjust)
- Monitor reorder points and generate alerts
- Track warehouse locations and inventory bins

## Responsibilities

### Inventory Management
- Maintain current stock levels by product and location
- Track inventory in multiple warehouses/locations
- Manage product variants and SKUs
- Monitor product costs and valuations
- Track inventory turnover metrics

### Reservation Management
- Reserve inventory for pending sales orders
- Release reservations when orders cancel
- Manage reservation hold times
- Prevent overselling
- Support partial reservations

### Stock Movement Processing
- Record all inventory transactions
- Track movement types (received, sold, adjusted, damaged, transferred)
- Maintain complete audit trail
- Calculate FIFO/LIFO valuations
- Generate inventory reports

### Warehouse Operations
- Manage warehouse locations and bins
- Track inventory levels by location
- Support transfers between locations
- Manage cycle counting and physical inventory
- Generate picking and packing lists

## Architecture

### Technology Stack
- **Framework**: ASP.NET Core 10
- **ORM**: Entity Framework Core
- **Database**: SQL Server
- **Patterns**: DDD, CQRS (MediatR), Event-Driven
- **Message Bus**: RabbitMQ, Kafka
- **Caching**: Redis (for real-time stock queries)

### Project Structure
```
WareHouseService/
├── API/                         # REST endpoints
├── Application/                 # Commands, Queries, Handlers
├── Domain/                      # Entities, aggregates
├── Infrastructure/              # Repositories, external services
├── Shared/                      # DTOs
└── Tests/
```

## API Endpoints

### Inventory Endpoints

#### Get Current Inventory
```http
GET /api/v1/inventory/{productId}
Authorization: Bearer <token>

Response: 200 OK
{
  "productId": "660e8400-e29b-41d4-a716-446655440000",
  "productName": "Product A",
  "totalQuantity": 500,
  "availableQuantity": 450,
  "reservedQuantity": 50,
  "lockedQuantity": 0,
  "locations": [
    {
      "location": "AISLE-A-01",
      "quantity": 250,
      "reserved": 25
    },
    {
      "location": "AISLE-B-02",
      "quantity": 250,
      "reserved": 25
    }
  ],
  "lastUpdated": "2026-06-13T15:30:00Z"
}
```

#### List Inventory
```http
GET /api/v1/inventory?search=product&lowStockOnly=false&skip=0&take=50
Authorization: Bearer <token>

Response: 200 OK
{
  "total": 1250,
  "items": [
    {
      "productId": "660e8400-e29b-41d4-a716-446655440000",
      "productName": "Product A",
      "totalQuantity": 500,
      "availableQuantity": 450,
      "reorderPoint": 100,
      "status": "OK"
    }
  ]
}
```

#### Get Low Stock Items
```http
GET /api/v1/inventory/low-stock
Authorization: Bearer <token>

Response: 200 OK
{
  "items": [
    {
      "productId": "660e8400-e29b-41d4-a716-446655440000",
      "productName": "Product A",
      "currentQuantity": 75,
      "reorderPoint": 100,
      "reorderQuantity": 500
    }
  ]
}
```

### Reservation Endpoints

#### Reserve Inventory
```http
POST /api/v1/inventory/reserve
Authorization: Bearer <token>
Content-Type: application/json

{
  "orderId": "770e8400-e29b-41d4-a716-446655440000",
  "items": [
    {
      "productId": "660e8400-e29b-41d4-a716-446655440000",
      "quantity": 50
    }
  ],
  "holdUntil": "2026-06-20T00:00:00Z"
}

Response: 201 Created
{
  "reservationId": "880e8400-e29b-41d4-a716-446655440000",
  "orderId": "770e8400-e29b-41d4-a716-446655440000",
  "status": "Reserved",
  "reservedItems": [
    {
      "productId": "660e8400-e29b-41d4-a716-446655440000",
      "reservedQuantity": 50,
      "location": "AISLE-A-01"
    }
  ],
  "reservedAt": "2026-06-13T15:30:00Z"
}
```

#### Release Reservation
```http
POST /api/v1/inventory/release
Authorization: Bearer <token>

{
  "reservationId": "880e8400-e29b-41d4-a716-446655440000",
  "reason": "Order cancelled"
}

Response: 200 OK
```

### Stock Movement Endpoints

#### Record Stock Movement
```http
POST /api/v1/movements
Authorization: Bearer <token>
Content-Type: application/json

{
  "movementType": "Received",
  "productId": "660e8400-e29b-41d4-a716-446655440000",
  "quantity": 100,
  "fromLocation": null,
  "toLocation": "AISLE-A-01",
  "reference": "PO-2026-001234",
  "notes": "Goods received from supplier"
}

Response: 201 Created
{
  "movementId": "990e8400-e29b-41d4-a716-446655440000",
  "movementType": "Received",
  "quantity": 100,
  "recordedAt": "2026-06-13T15:30:00Z"
}
```

#### Get Movement History
```http
GET /api/v1/movements/{productId}?startDate=2026-06-01&endDate=2026-06-30&skip=0&take=50
Authorization: Bearer <token>

Response: 200 OK
{
  "total": 125,
  "items": [
    {
      "movementId": "990e8400-e29b-41d4-a716-446655440000",
      "productId": "660e8400-e29b-41d4-a716-446655440000",
      "movementType": "Received",
      "quantity": 100,
      "reference": "PO-2026-001234",
      "recordedAt": "2026-06-13T15:30:00Z"
    }
  ]
}
```

## Published Events

### InventoryReservedEvent
Published when inventory is successfully reserved for a sales order.

```csharp
public class InventoryReservedEvent : IntegrationEvent
{
    public Guid ReservationId { get; set; }
    public Guid OrderId { get; set; }
    public List<ReservedItem> ReservedItems { get; set; }
    public DateTime ReservedAt { get; set; }
}
```

**Subscribers**: Sales Service (order confirmation), Accounting Service

### InventoryUpdatedEvent
Published when inventory levels change.

```csharp
public class InventoryUpdatedEvent : IntegrationEvent
{
    public Guid ProductId { get; set; }
    public int PreviousQuantity { get; set; }
    public int CurrentQuantity { get; set; }
    public string MovementType { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**Subscribers**: Sales Service, Purchasing Service, Analytics (Kafka)

### StockMovementRecordedEvent
Published when any stock movement is recorded.

```csharp
public class StockMovementRecordedEvent : IntegrationEvent
{
    public Guid MovementId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public string MovementType { get; set; }
    public string Reason { get; set; }
    public DateTime RecordedAt { get; set; }
}
```

**Subscribers**: Accounting Service, Analytics

## Consumed Events

- `SalesOrderCreatedEvent` - Process inventory reservations
- `SalesOrderCancelledEvent` - Release reservations
- `SalesOrderShippedEvent` - Update inventory (sold)
- `PurchaseOrderCreatedEvent` - Prepare receiving
- `PurchaseOrderReceivedEvent` - Update inventory (received)

## Database Schema

### Key Tables

**Inventory**
- ProductId (PK)
- ProductName
- CurrentQuantity
- ReservedQuantity
- LockedQuantity
- ReorderPoint
- ReorderQuantity
- Cost
- LastMovementDate
- UpdatedAt

**StockMovements**
- MovementId (PK)
- ProductId (FK)
- Quantity
- MovementType (Received, Sold, Adjusted, Damaged, Transferred)
- FromLocation
- ToLocation
- ReferenceId
- Reason
- RecordedAt

**Reservations**
- ReservationId (PK)
- OrderId
- ProductId (FK)
- ReservedQuantity
- ReservationLocation
- HoldUntil
- Status (Reserved, Released, Fulfilled)
- ReservedAt
- ReleasedAt

**WarehouseLocations**
- LocationId (PK)
- LocationCode
- Aisle
- Bin
- Shelf
- Capacity
- IsActive

## Inventory Management Strategies

### Stock Levels
- **Safety Stock**: Minimum level to prevent stockouts
- **Reorder Point**: Level at which to place new orders
- **Reorder Quantity**: Standard order quantity
- **Maximum Stock**: Maximum storage capacity

### Stock Valuation
- **FIFO**: First In, First Out (oldest inventory first)
- **LIFO**: Last In, First Out (newest inventory first)
- **Weighted Average**: Average cost method
- **Specific Identification**: Track specific batches

### Reservation Policies
- **FIFO Allocation**: Allocate oldest stock first
- **Location-based**: Allocate from nearest location
- **Expiration Date**: Allocate items expiring soon
- **Manual**: Warehouse staff selects location

## Getting Started

### Prerequisites
- .NET 10 SDK
- SQL Server 2019+
- RabbitMQ & Kafka
- Identity Service

### Running

```bash
# Run migrations
dotnet ef database update

# Start service
dotnet run

# Service runs on http://localhost:5004
```

## Testing

```bash
# Unit tests
dotnet test --filter "Category=Unit"

# Integration tests
dotnet test --filter "Category=Integration"
```

## Performance Optimization

### Caching Strategy
- Cache inventory levels in Redis
- 5-minute TTL for stock queries
- Invalidate on movements
- Warm cache on startup

### Database Optimization
- Index ProductId, LocationId
- Partition StockMovements by month
- Archive old movements quarterly
- Real-time reporting via materialized views

## Development Notes

### Key Business Rules
- Cannot reserve more than available quantity
- Cannot exceed warehouse location capacity
- Stock movements must have valid source/destination
- Reservations auto-expire if not fulfilled

### Customization
- Implement multi-warehouse support
- Add barcode scanning
- Implement RFID tracking
- Add cycle counting workflows

## Dependencies
- SQL Server (WarehouseDB)
- RabbitMQ (events)
- Kafka (analytics)
- Redis (caching)
- Identity Service (user validation)

## Support & Troubleshooting

**Issue: Reservation fails with insufficient stock**
- Check available quantity
- Verify product exists
- Check other active reservations
- Process expired reservations

**Issue: Stock levels incorrect**
- Review recent movements
- Check for pending transactions
- Verify system entry vs physical count
- Run cycle count

---

**Last Updated**: 2026-06-13  
**Version**: 1.0.0
