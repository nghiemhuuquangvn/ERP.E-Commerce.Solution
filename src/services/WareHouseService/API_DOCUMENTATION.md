# Warehouse Service API Documentation

**Base URL:** `http://localhost:5004/api/v1`

## Endpoints

### Inventory Management

#### 1. Get Inventory by Product ID
```http
GET /inventory/{productId}
```

**Response (200 OK):**
```json
{
  "productId": "660e8400-e29b-41d4-a716-446655440000",
  "productName": "Product A",
  "totalQuantity": 500,
  "availableQuantity": 450,
  "reservedQuantity": 50,
  "lockedQuantity": 0,
  "reorderPoint": 100,
  "reorderQuantity": 500,
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

#### 2. List All Inventory
```http
GET /inventory?search=product&lowStockOnly=false&skip=0&take=50
```

**Query Parameters:**
- `search` (optional): Search by product name
- `lowStockOnly` (optional): Filter only low stock items (default: false)
- `skip` (optional): Number of items to skip (default: 0)
- `take` (optional): Number of items to return (default: 50)

**Response (200 OK):**
```json
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

#### 3. Get Low Stock Items
```http
GET /inventory/low-stock
```

**Response (200 OK):**
```json
{
  "total": 5,
  "items": [
    {
      "productId": "660e8400-e29b-41d4-a716-446655440000",
      "productName": "Product A",
      "totalQuantity": 75,
      "availableQuantity": 50,
      "reorderPoint": 100,
      "status": "Low Stock"
    }
  ]
}
```

### Reservation Management

#### 4. Reserve Inventory
```http
POST /inventory/reserve
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
```

**Response (201 Created):**
```json
{
  "reservationId": "880e8400-e29b-41d4-a716-446655440000",
  "orderId": "770e8400-e29b-41d4-a716-446655440000",
  "reservedItems": [
    {
      "productId": "660e8400-e29b-41d4-a716-446655440000",
      "reservedQuantity": 50,
      "location": "AISLE-A-01"
    }
  ],
  "status": "Reserved",
  "reservedAt": "2026-06-13T15:30:00Z"
}
```

**Error (400 Bad Request):**
```json
{
  "message": "Cannot reserve 50 units of Product A. Available: 30"
}
```

#### 5. Release Reservation
```http
POST /inventory/release
Content-Type: application/json

{
  "reservationId": "880e8400-e29b-41d4-a716-446655440000",
  "reason": "Order cancelled"
}
```

**Response (200 OK):**
```json
{
  "reservationId": "880e8400-e29b-41d4-a716-446655440000",
  "status": "Released",
  "releasedAt": "2026-06-13T16:00:00Z"
}
```

### Stock Movements

#### 6. Record Stock Movement
```http
POST /movements
Content-Type: application/json

{
  "productId": "660e8400-e29b-41d4-a716-446655440000",
  "quantity": 100,
  "movementType": "Received",
  "fromLocation": null,
  "toLocation": "AISLE-A-01",
  "reference": "PO-2026-001234",
  "reason": "Goods received from supplier",
  "sourceOrderId": null
}
```

**Movement Types:**
- `Received`: Stock received from supplier
- `Sold`: Stock sold to customer
- `Adjusted`: Manual inventory adjustment
- `Damaged`: Stock marked as damaged
- `Transferred`: Stock transferred between locations

**Response (201 Created):**
```json
{
  "movementId": "990e8400-e29b-41d4-a716-446655440000",
  "movementType": "Received",
  "quantity": 100,
  "recordedAt": "2026-06-13T15:30:00Z"
}
```

#### 7. Get Movement History
```http
GET /movements/{productId}?startDate=2026-06-01&endDate=2026-06-30&skip=0&take=50
```

**Query Parameters:**
- `startDate` (optional): Filter movements after this date
- `endDate` (optional): Filter movements before this date
- `skip` (optional): Number of items to skip (default: 0)
- `take` (optional): Number of items to return (default: 50)

**Response (200 OK):**
```json
{
  "total": 125,
  "items": [
    {
      "movementId": "990e8400-e29b-41d4-a716-446655440000",
      "productId": "660e8400-e29b-41d4-a716-446655440000",
      "movementType": "Received",
      "quantity": 100,
      "fromLocation": null,
      "toLocation": "AISLE-A-01",
      "reference": "PO-2026-001234",
      "reason": "Goods received from supplier",
      "recordedAt": "2026-06-13T15:30:00Z"
    }
  ]
}
```

## Integration Events

### Published Events

#### InventoryReservedEvent
Published when inventory is successfully reserved for a sales order.

**Event Structure:**
```json
{
  "reservationId": "880e8400-e29b-41d4-a716-446655440000",
  "orderId": "770e8400-e29b-41d4-a716-446655440000",
  "reservedItems": [
    {
      "productId": "660e8400-e29b-41d4-a716-446655440000",
      "quantity": 50,
      "warehouseLocation": "AISLE-A-01"
    }
  ],
  "reservedAt": "2026-06-13T15:30:00Z"
}
```

**Subscribers:** Sales Service, Accounting Service

#### InventoryUpdatedEvent
Published when inventory levels change due to stock movement.

**Event Structure:**
```json
{
  "productId": "660e8400-e29b-41d4-a716-446655440000",
  "previousQuantity": 500,
  "currentQuantity": 600,
  "movementType": "Received",
  "referenceId": "po-123",
  "updatedAt": "2026-06-13T15:30:00Z"
}
```

**Subscribers:** Sales Service, Purchasing Service, Analytics (Kafka)

#### StockMovementRecordedEvent
Published when a stock movement is recorded.

**Event Structure:**
```json
{
  "movementId": "990e8400-e29b-41d4-a716-446655440000",
  "productId": "660e8400-e29b-41d4-a716-446655440000",
  "quantity": 100,
  "movementType": "Received",
  "fromLocation": null,
  "toLocation": "AISLE-A-01",
  "reason": "Goods received from supplier",
  "sourceOrderId": null,
  "recordedAt": "2026-06-13T15:30:00Z"
}
```

**Subscribers:** Accounting Service, Analytics

### Consumed Events

#### SalesOrderCreatedEvent
When a sales order is created, the Warehouse Service automatically reserves inventory.

#### SalesOrderCancelledEvent
When a sales order is cancelled, the Warehouse Service releases the reserved inventory.

#### SalesOrderShippedEvent
When a sales order is shipped, the Warehouse Service marks the reservation as fulfilled.

#### PurchaseOrderCreatedEvent
When a purchase order is created, the Warehouse Service prepares to receive the goods.

## Error Codes

| Status | Code | Description |
|--------|------|-------------|
| 400 | InvalidOperation | Invalid operation (e.g., insufficient stock) |
| 404 | NotFound | Resource not found |
| 500 | InternalError | Internal server error |

## Rate Limiting

No rate limiting is currently implemented. This may be added in future versions.

## Authentication

All endpoints require authentication. Include JWT token in Authorization header:
```
Authorization: Bearer <token>
```

---

**Last Updated:** 2026-06-14  
**API Version:** 1.0
