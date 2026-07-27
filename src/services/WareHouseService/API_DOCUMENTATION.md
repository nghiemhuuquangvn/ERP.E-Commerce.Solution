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

**Note (2026-07-27):** reservation is a non-destructive soft hold — `InventoryLot.RemainingQuantity`
is not touched here, only `ReservedQuantity` (at the product, location, and lot level).
Real depletion only happens when the order ships. Re-sending the same `orderId` while an
active reservation already exists for it is idempotent — the existing reservation is
returned rather than a duplicate being created.

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

### Stock Adjustments (added 2026-07-27) — approval-gated, replaces the old direct-write `POST /inventory/{productId}/adjust`

#### 8. Request an Adjustment
```http
POST /stock-adjustments
Content-Type: application/json

{ "productId": "660e8400-...", "locationId": null, "quantityDelta": -5, "reason": "Damaged", "notes": "Box crushed in transit" }
```
**Response (201 Created):** a `StockAdjustment` with `status: "Pending"` — Inventory is unchanged at this point.

#### 9. Approve / Reject
```http
POST /stock-adjustments/{id}/approve   -> posts the delta to Inventory, returns { productId, previousQuantity, currentQuantity, movementId, adjustedAt }
POST /stock-adjustments/{id}/reject
{ "reason": "Needs re-count first" }
```

### Warehouses (added 2026-07-27) — physical sites, parent of Locations

```http
GET    /warehouses
GET    /warehouses/{id}
POST   /warehouses            { "code": "HN", "name": "Hanoi Warehouse", "address": "..." }
PUT    /warehouses/{id}
POST   /warehouses/{id}/activate
POST   /warehouses/{id}/deactivate
```

### Stocktake approval (added 2026-07-27)

```http
POST /stocktakes/{id}/complete   -> locks counts, computes variance, status becomes "PendingApproval" — Inventory unchanged
POST /stocktakes/{id}/approve    -> the only step that posts variance to Inventory, status becomes "Completed"
```

### Barcode (added 2026-07-27)

```http
GET    /barcode/{code}/resolve   -> { productId, productName, availableQuantity, lotId, serialNumber } — cached 60s
POST   /barcode                  { "barcode": "...", "productId": "...", "lotId": null, "serialNumber": null }
DELETE /barcode/{id}
```

### Cost Consumption Log (added 2026-07-27, Module 8) — per-sale COGS audit trail

```http
GET /inventory/{productId}/cost-consumption?startDate=&endDate=
-> [{ id, productId, lotId, quantity, unitCost, totalCost, sourceOrderId, consumedAt }]
```

One row per lot consumed at ship time (or one row at the product's average cost for
non-lot-tracked products) — `StockMovement` alone never recorded a unit cost.

### Inventory Valuation Snapshots (added 2026-07-27, Module 8) — period-end, immutable close

```http
POST /inventory/valuation/snapshots        { "periodCode": "2026-07", "warehouseId": null }
GET  /inventory/valuation/snapshots/{period}?warehouseId=
```

`POST` is rejected (400) if the `(periodCode, warehouseId)` combination is already closed —
a closed period is never edited, only superseded by a correcting entry in a later period.
Requires `inventory:valuation:close`.

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

**Subscribers:** Sales Service. The "Accounting Service" subscriber previously listed here
is unconfirmed — AccountingService's real warehouse-movement consumer only checks that a
fiscal period is open and does no actual valuation posting yet (see that service's docs).

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

#### InventoryInsufficientEvent
Published instead of InventoryReservedEvent when one or more lines in a reservation request couldn't be fully covered — feeds SaleOrderService's saga compensation path.

#### ReceiptCompletedEvent
Published when goods are received (`ReceiveStockCommand`).

#### StockAlertEvent
Published when a product's quantity drops to/below its reorder point.

**Subscribers (as of 2026-07-27):** PurchaseOrderService — creates a suggestion-only
`PurchaseRequest` (see that service's docs). Previously unconsumed anywhere.

#### StockAdjustmentApprovedEvent / StockAdjustmentRejectedEvent (added 2026-07-27)
Published from the approval-gated Stock Adjustment workflow — see the Stock Adjustments section above.

#### StocktakeCompletedEvent
Published once a stocktake's variance is approved and posted.

#### TransferOrderCompletedEvent
Published when a transfer order completes — both source and destination `InventoryLocationStock` rows are updated.

#### InventoryValuationSnapshotClosed (added 2026-07-27, Module 8)
Published when a period-end Inventory Valuation Snapshot is closed.
```json
{
  "snapshotId": "...", "periodCode": "2026-07", "warehouseId": null,
  "snapshotDate": "2026-07-27T00:00:00Z", "totalValue": 125000.00, "lineCount": 42
}
```
**Subscribers:** None yet — see the Reliability/Still-open notes in the README.

**Reliability note (added 2026-07-27):** all of the above are now staged into an Outbox
table in the same transaction as the business write and delivered by a background
`WarehouseEventOutboxProcessor`, instead of being published synchronously from the
handler — a crash between the DB write and the publish can no longer silently drop an
event.

### Consumed Events

**Corrected 2026-07-27** — this section previously listed events that don't match what's
actually consumed (`SalesOrderCreatedEvent` directly, `PurchaseOrderCreatedEvent`); the
real bindings are below.

#### sales_order.ReserveInventoryCommand (RabbitMQ)
Fired by SaleOrderService's `OrderFulfillmentSaga` **after payment is confirmed**, not
directly off order creation — this binding was deliberately changed to avoid
double-reserving stock.

#### SalesOrderCancelledEvent
When a sales order is cancelled, releases the reserved inventory — reverses the soft hold
at the product, location, and every lot level, with no change to on-hand quantity.

#### SalesOrderShippedEvent
**Fixed 2026-07-27** — previously this only flipped the reservation to `Fulfilled` and
never actually reduced stock. Now consumes the exact lots that were reserved, reduces
`Inventory`/`InventoryLocationStock`, writes a `StockMovement` (Sold) and a
`CostConsumptionLog` row per lot, then marks the reservation fulfilled.

#### PurchaseOrderReceivedDomainEvent (Kafka `purchasing-topic`)
Matched to a product by `ProductSku` string (the event only carries a product code, not a shared id) — triggers `ReceiveStockCommand`. There is no consumer for `PurchaseOrderCreatedEvent`/`PurchaseOrderApprovedEvent`.
**Idempotent as of 2026-07-27:** a redelivered receipt for the same product+PO (matched by
`StockMovement.Reference`, e.g. `"PO-{purchaseOrderId}"`) is skipped rather than applied twice.

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

**Last Updated:** 2026-07-27
**API Version:** 1.1 — added Stock Adjustments (approval-gated), Warehouses, Stocktake approval, and Barcode endpoints; corrected the Consumed Events section; documented the Outbox reliability retrofit. Removed the old direct-write `POST /inventory/{productId}/adjust` endpoint (replaced by the Stock Adjustments workflow).
**API Version:** 1.2 (Module 8 reconciliation) — fixed `SalesOrderShippedEvent` never
reducing stock and the related reservation/lot double-depletion bug (reservation is now a
non-destructive soft hold); added Cost Consumption Log and Inventory Valuation Snapshot
endpoints/events; added idempotency to `ReserveInventoryCommand`/PO-driven receiving; added
`inventory:valuation:close` permission.
