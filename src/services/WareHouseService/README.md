# Warehouse Service

Manages inventory, stock movements, and inventory reservations for the ERP E-Commerce Solution.

## Overview

The Warehouse Service is the central hub for inventory management. It provides real-time inventory visibility, manages reservations for sales orders, processes goods receipt, and maintains accurate stock levels.

**Key Functions:**
- Real-time inventory tracking, at both a per-product rollup AND a per-location (bin-level) breakdown
- Reserve inventory for sales orders, with FEFO lot allocation for lot/expiry-tracked products
- Process goods receipt
- Record all stock movements (receive, issue, adjust, transfer)
- Monitor reorder points and generate alerts
- Track warehouse sites and locations/bins
- Resolve a scanned barcode to a product for handheld scanners
- Enforce Warehouse Manager approval before a stock adjustment or stocktake variance can affect Inventory

## Responsibilities

This service deliberately merges what a stricter design might call two bounded contexts —
"Warehouse" (physical movement) and "Inventory" (stock balance/valuation) — into one. There
is no separate Inventory service anywhere in this system; `Inventory` here IS the stock
ledger, updated directly by the same handlers that record the movement. See
`docs/business/Warehouse/module-07-warehouse.md`'s reconciliation notes for the reasoning.

### Inventory Management
- Maintain current stock levels by product (`Inventory`, the cross-location rollup) AND by
  product+location (`InventoryLocationStock`, additive bin-level breakdown)
- Track inventory across multiple warehouse sites/locations
- Monitor product costs (FIFO/LIFO/Average) and reorder levels

### Reservation Management
- Reserve inventory for pending sales orders — FEFO lot allocation (earliest expiry first)
  when the product has tracked lots, first-fit location allocation when bin-level stock
  data exists for the product, falling back to the product-level rollup otherwise
- Release reservations when orders cancel (releases both the location-level hold and any
  depleted lot quantity)
- Prevent overselling via a two-phase all-or-nothing check across every line in a request

### Stock Adjustment (approval-gated)
- Requesting an adjustment (`StockAdjustment`, Status Pending) never touches Inventory
- Only `Approve` posts the change to `Inventory`/`InventoryLocationStock` and records the
  `StockMovement` — `Reject` discards it. This is a hard business rule, not optional.

### Stocktake (cycle count, approval-gated)
- `Complete()` locks counts and computes variance but does not post it
- A separate `Approve()` step is the only place variance actually reaches Inventory —
  mirrors the Stock Adjustment approval gate

### Warehouse Operations
- `Warehouse` (physical site) as the parent of `WarehouseLocation` (zone/aisle/bin/shelf)
- Support transfers between locations, with a source-location availability check
- Resolve barcodes to products (cached, for scanner speed)

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

### Stock Adjustments (`api/v1/stock-adjustments`) — approval-gated

```http
POST /api/v1/stock-adjustments                  # request only — no Inventory change yet
{ "productId": "...", "locationId": null, "quantityDelta": -5, "reason": "Damaged", "notes": "..." }

POST /api/v1/stock-adjustments/{id}/approve      # the ONLY endpoint that posts to Inventory
POST /api/v1/stock-adjustments/{id}/reject
{ "reason": "..." }
```

### Warehouses (`api/v1/warehouses`) — physical sites, parent of Locations

```http
GET    /api/v1/warehouses
GET    /api/v1/warehouses/{id}
POST   /api/v1/warehouses                        # { "code": "HN", "name": "Hanoi Warehouse", "address": "..." }
PUT    /api/v1/warehouses/{id}
POST   /api/v1/warehouses/{id}/activate
POST   /api/v1/warehouses/{id}/deactivate
```

`POST /api/v1/locations` and `PUT /api/v1/locations/{id}` now also accept an optional
`warehouseId` to attach a location to a site.

### Stocktakes — approval step added

```http
POST /api/v1/stocktakes/{id}/complete   # locks counts, computes variance — no Inventory change yet
POST /api/v1/stocktakes/{id}/approve    # the ONLY endpoint that posts variance to Inventory
```

### Barcode (`api/v1/barcode`)

```http
GET  /api/v1/barcode/{code}/resolve     # cached (60s) — product + available quantity
POST /api/v1/barcode                    # { "barcode": "...", "productId": "...", "lotId": null, "serialNumber": null }
DELETE /api/v1/barcode/{id}
```

## Published Events

All published via RabbitMQ topic exchange `warehouse_exchange` (routing key
`warehouse.{EventTypeName}`), staged transactionally into the `OutboxMessages` table by
command handlers (`IEventOutboxWriter.Stage(...)`) and delivered by the background
`WarehouseEventOutboxProcessor` — not published synchronously from the handler anymore
(that was the previous behavior; a crash between the DB write and the publish could
silently drop the event).

- `InventoryReservedEvent` — reservation succeeded (now includes the FEFO-allocated `LotId` per item where applicable)
- `InventoryInsufficientEvent` — one or more lines in a reservation request couldn't be fully reserved
- `InventoryUpdatedEvent` — balance changed (any movement type)
- `ReceiptCompletedEvent` — goods received
- `StockMovementRecordedEvent` — any movement recorded (receive/sell/adjust/transfer)
- `StockAlertEvent` — a product dropped to/below its reorder point
- `StockAdjustmentApprovedEvent` / `StockAdjustmentRejectedEvent` — outcome of the approval-gated adjustment workflow
- `StocktakeCompletedEvent` — stocktake variance approved and posted
- `TransferOrderCompletedEvent` — transfer finished, both source and destination `InventoryLocationStock` updated

**Note:** these are plain classes, not `IntegrationEventBase`-derived like most other
services' events — a pre-existing inconsistency, not something introduced by this pass.

## Consumed Events

- `sales_order.ReserveInventoryCommand` (RabbitMQ, from SaleOrderService's `OrderFulfillmentSaga`, fired post-payment — not directly off `SalesOrderCreatedEvent`, to avoid double-reserving)
- `SalesOrderCancelledEvent` — release reservations
- `SalesOrderShippedEvent` — update inventory (sold)
- `PurchaseOrderReceivedDomainEvent` (Kafka `purchasing-topic`, matched by `ProductSku` string) — triggers `ReceiveStockCommand`

**Note:** CLAUDE.md's claimed list (`SalesOrderCreated`, `PurchaseOrderCreated`) and this
README's previous list (`PurchaseOrderCreatedEvent`, `PurchaseOrderReceivedEvent`) were
both stale — neither matches what's actually consumed today.

## Database Schema

### Key Tables

**Inventories** (PK: ProductId — this is the cross-location rollup)
- ProductId (PK), ProductName, ProductSku
- TotalQuantity, ReservedQuantity, LockedQuantity, AvailableQuantity (computed)
- ReorderPoint, ReorderQuantity, Cost, ValuationMethod (FIFO/LIFO/Average)
- LocationId (nullable — legacy single-location hint, superseded by InventoryLocationStocks below)
- LastMovementDate, UpdatedAt

**InventoryLocationStocks** — bin-level breakdown, additive alongside Inventories
- Id (PK), ProductId, LocationId (unique together), Quantity, ReservedQuantity, LockedQuantity, UpdatedAt

**StockMovements** — append-style log (coexists with, does not replace, the Inventories balance)
- MovementId (PK), ProductId, Quantity, MovementType (Received/Sold/Adjusted/Damaged/Transferred)
- FromLocation/ToLocation (free-text), Reference, Reason, SourceOrderId, RecordedAt

**Reservations** / **Reservation_ReservedItems** (owned collection)
- ReservationId (PK), OrderId, Status (Reserved/Fulfilled/Released/Expired), HoldUntil, ReservedAt, ReleasedAt, ReleaseReason
- Per item: ProductId, Quantity, WarehouseLocation, LotId (FEFO-allocated lot, nullable)

**InventoryLots** — LotId (PK), ProductId, LotNumber, ReceivedQuantity, RemainingQuantity, UnitCost, ReceivedDate, ExpiryDate, PurchaseOrderId

**InventorySerials** — SerialId (PK), ProductId, SerialNumber (unique), LotId, Status (InStock/Reserved/Sold/Damaged)

**TransferOrders** / **TransferOrderLines** — Draft→InTransit→Completed/Cancelled, FromLocationId/ToLocationId, Lines(ProductId, Quantity)

**Stocktakes** / **StocktakeLines** — InProgress→PendingApproval→Completed (or Cancelled); lines carry SystemQuantity, CountedQuantity, VarianceReason

**StockAdjustments** — Id (PK), ProductId, LocationId (nullable), QuantityDelta, Reason, Notes, Status (Pending/Approved/Rejected), RequestedBy/RequestedAt, ApprovedBy/ApprovedAt, RejectionReason

**Warehouses** — Id (PK), Code (unique), Name, Address, IsActive — parent of:

**WarehouseLocations** — LocationId (PK), WarehouseId (nullable FK), LocationCode, Zone, Aisle, Bin, Shelf, Capacity, CurrentOccupancy, IsActive

**BarcodeMappings** — Id (PK), Barcode (unique), ProductId, LotId, SerialNumber

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
- Cannot reserve more than available quantity (checked two-phase, all-or-nothing across every line in a request)
- Reservation picks FEFO lots (earliest expiry first) and, where bin-level data exists, a single location whose own stock covers the full line — never split across bins for one line
- A stock adjustment or stocktake variance **must be approved** before it can affect Inventory — there is no direct-write path anymore
- A transfer's source location is checked for availability (falls back to the product-level rollup if no bin-level data exists yet for that product/location)
- Reservations auto-expire if not fulfilled

### Recently added (2026-07-27) — see `docs/business/Warehouse/module-07-warehouse.md` reconciliation notes for full rationale
- Bin-level stock breakdown (`InventoryLocationStocks`), additive alongside the product-level rollup
- Stock Adjustment and Stocktake approval workflows (previously direct-write)
- `Warehouse` (physical site) entity — did not exist anywhere before
- FEFO lot allocation on reservation (previously hardcoded to a single `"DEFAULT_LOCATION"`)
- Barcode resolve endpoint
- Outbox pattern for all published events (previously synchronous, non-transactional RabbitMQ publish)
- Granular permissions for adjustment/stocktake approval, barcode, warehouse management

### Still-open items (deferred, not built this round)
- True multi-location-per-product stock is additive/opportunistic today — not backfilled for old data, and a single reservation line still can't split across two bins
- No deeper Zone→Aisle→Rack→Bin hierarchy under `Warehouse` (only the site level was added)
- No consumer for `PurchaseOrderCreatedEvent`/`PurchaseOrderApprovedEvent` (only the actual-receipt event is consumed)

## Dependencies
- SQL Server (WarehouseDB)
- RabbitMQ (`warehouse_exchange` outbound, `sales_order_events`/`warehouse_exchange` inbound consumers)
- Kafka (`purchasing-topic`, analytics)
- Redis (`ICacheService` — barcode resolve caching, inventory query caching)
- Identity Service (JWT validation)

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

**Last Updated**: 2026-07-27
**Version**: 1.1.0 — Bin-level stock tracking, Stock Adjustment/Stocktake approval workflow, Warehouse (site) entity, FEFO lot allocation, Barcode resolve, and Outbox retrofit added on top of the original Inventory implementation.
