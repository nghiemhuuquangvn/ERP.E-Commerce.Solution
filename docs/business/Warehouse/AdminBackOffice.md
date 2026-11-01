# Warehouse — Admin back office (screens under `Inventory`)

Business analysis of the Admin screens of the Warehouse module as shipped on 2026-11-01 (ROADMAP P2-9, U3 batch 5): **Inventory** (stock list and product stock detail), **Locations**, **Warehouses**, **Stock Adjustments**, **Stocktakes**, **Transfers**, **Barcode**, **Valuation Snapshots**. The older [module-07-warehouse.md](module-07-warehouse.md) is an *aspirational* design (goods receipt, goods issue, FEFO picking, bin put-away, warehouse-to-warehouse transfers); this document describes **what the shipped service and screens actually do**. Replenishment rules: [Replenishment.md](Replenishment.md). Vietnamese user guide: [docs/user-guides/inventory.md](../../user-guides/inventory.md).

## 1. Purpose and scope
Let warehouse staff see how much of each product the shop holds and where, correct the numbers in a controlled way (approval), move stock between locations, keep the replenishment policy, and freeze the inventory value at period end. In scope: the screens above and the WareHouseService API behind them. **Out of scope today (no screen):** goods receipt and put-away (stock arrives through Purchasing receipts and POS / Sales events), goods issue and picking, reservations list, stock-movement history, lot / serial maintenance, choosing the costing method (the service holds FIFO / LIFO / Average), warehouse-to-warehouse transfers (transfers are between locations).

## 2. Actors and permissions
| Actor | Does | Permission |
|---|---|---|
| Warehouse clerk | Reads stock; maintains locations; starts, counts, completes and cancels stocktakes; creates and ships transfers; maps barcodes; sets the stock policy | `inventory:view`, `inventory:manage` |
| Stock requester | Asks for a quantity correction on a product | `inventory:adjustment:create` |
| Warehouse manager | Approves / rejects adjustments; approves stocktake variances; closes a valuation period | `inventory:adjustment:approve`, `inventory:stocktake:approve`, `inventory:valuation:close` |
| Warehouse admin | Creates / edits / deactivates warehouses | `inventory:warehouse:manage` (seeded, enforced by the API) |
| Buyer | Reads low stock; Purchasing turns alerts into purchase proposals | `inventory:view` |
Buttons appear only with the permission; a user without `inventory:view` sees empty / forbidden pages.

## 3. Process overview
```
Warehouse ── Location (zone, aisle, bin, shelf, capacity)
Stock  = on hand − reserved − locked = AVAILABLE   (per product, split by location, lot, serial)
Stock changes by:  receipt / sale / POS / return (automatic events)
                   Adjustment request ──approve──> stock changed        (manual, needs a second person)
                   Stocktake count ──complete──> pending ──approve──> stock changed by the variances
                   Transfer order ──ship──> in transit ──complete──> stock moved between locations
Value: FIFO / LIFO / Average cost → Valuation snapshot closes a period
Policy: min / safety / max per product → low-stock alert → Purchasing proposal
```
Movement types recorded by the service: Received, Sold, Adjusted, Damaged, Transferred, Returned, ReturnedToVendor.

## 4. State models and rules
### 4.1 Stock adjustment (`Pending → Approved | Rejected`)
Requested on the **product stock detail** page (not on the adjustments page): quantity change ≠ 0 (negative = loss), reason Damaged / Lost / Correction / Other, optional notes. **It does not change stock until approved** (the dialog says so). The adjustments page lists requests (default filter Pending): **Approve** applies the change and publishes `StockAdjustmentApproved`; **Reject** needs a written reason and publishes `StockAdjustmentRejected`. Approved / Rejected are final.

### 4.2 Stocktake (`InProgress → PendingApproval → Completed`, or `Cancelled`)
**Start** counts the **whole warehouse** (every product line starts with the system quantity). Staff enter the counted quantity per line and press Record (variance = counted − system). **Complete Count** locks counting and computes variances — **stock is not changed yet**. **Approve Variance** (manager) is the only step that posts the variances to stock and cannot be undone. **Cancel** is allowed until Completed; counted quantities are discarded, stock unchanged. Screen: Complete, Approve and Cancel each ask for confirmation and state the consequence.

### 4.3 Transfer order (`Draft → InTransit → Completed`, or `Cancelled`)
Between two **different locations** (the same location is refused), at least one line (product, quantity > 0), optional notes. **Ship** (from Draft) takes the quantities out of the source; **Complete** (from InTransit, i.e. goods received) adds them to the destination and publishes `TransferOrderCompleted`; **Cancel** is offered on Draft only (the service would allow more). Ship, Complete and Cancel ask for confirmation.

### 4.4 Locations and warehouses (active ⇄ inactive)
Location: code (unique, not editable later), zone, aisle, bin, shelf, capacity; the list shows occupancy as `current / capacity` with a bar that turns red from 90 %. Warehouse: code, name, address. **Deactivate** asks for confirmation (never deletes); **Activate** is immediate.

### 4.5 Stock policy (per product)
`0 ≤ safety ≤ minimum < maximum`. Low stock = available ≤ minimum. One alert per drop below, one when stock recovers, a daily rescan; Purchasing proposes `maximum − (available + on order)`. The product detail card validates before sending.

### 4.6 Valuation snapshot
**Close period** (period code such as `2026-07`, optional warehouse) freezes the stock value per product (quantity × unit cost) as an immutable snapshot; **a period can be closed only once**. The screen asks for confirmation ("the period is locked"). A closed period is viewed by code.

### 4.7 Barcode
Resolve a scanned code to product (and lot / serial when mapped); managers add code → product mappings.

## 5. Requirements with acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| WH-1 | A stock-moving or irreversible step is never one click | Approve Variance, Complete Count, Cancel Stocktake, Ship / Complete / Cancel transfer, Close Period, Deactivate warehouse / location open a dialog that states the consequence; no request is sent until confirmed; Esc / Cancel leave everything unchanged |
| WH-2 | A request for stock correction is clearly pending | After submitting, a toast says "waiting for approval"; the request appears under Pending; stock is unchanged |
| WH-3 | Rejecting needs a reason | Empty reason → "Bắt buộc", nothing sent; with a reason the request leaves the Pending list |
| WH-4 | Every dialog and form validates before sending | Required fields starred with linked "Bắt buộc"; transfer: source / destination required and different, at least one line |
| WH-5 | Failures are explained | The server's reason (or the first validation message) appears in the dialog / a toast |
| WH-6 | Variances are readable | Shortage in red with a minus sign, surplus in blue with a plus sign (the sign carries the meaning, not only colour) |
| WH-7 | Lists are usable | Empty state; filter with no result → "Không có kết quả" + Xóa lọc; numeric columns right-aligned; the transfers list shows location codes, not ids |
| WH-8 | Accessible | Dialogs trap focus; tabs have roles; every input has a label; axe finds no violation |

## 6. Use cases
**UC-1 Correct a quantity:** open the product → Request adjustment (−7, Damaged, notes) → manager opens Stock Adjustments → Approve (stock changes) or Reject with a reason. *Exception:* quantity 0 → refused; server refusal → shown in the dialog.
**UC-2 Count a warehouse:** Start Stocktake → enter counted quantities line by line → Complete Count (confirm) → manager Approve Variance (confirm; stock changes) or Cancel Stocktake (confirm; nothing changes). *Alternative:* cancel at any open step.
**UC-3 Move stock:** New Transfer (source, destination, lines) saved as Draft → Ship (confirm) → when goods arrive Complete (confirm). *Alternative:* Cancel a Draft (confirm). *Exception:* same source and destination; not enough stock at source (server refusal shown).
**UC-4 Add a storage location:** Warehouses → add; Locations → add with warehouse, code, aisle, bin, shelf, capacity.
**UC-5 Close a period:** Valuation Snapshots → period code → Close Period (confirm) → the snapshot appears when looked up by code. *Exception:* already closed → refusal.
**UC-6 Set the replenishment policy:** product detail → minimum / safety / maximum → Save (disabled until valid and changed).

## 7. Data model (screen view)
Warehouse(Code, Name, Address, IsActive) · Location(LocationCode, Name, Zone, Aisle, Bin, Shelf, Capacity, CurrentOccupancy, WarehouseId, IsActive) · Inventory(ProductId, Total, Available, Reserved, Locked, ReorderPoint, Min, Max, Safety, IsLowStock, Locations[], Lots[], Serials[]) · StockAdjustment(ProductId, QuantityDelta, Reason, Notes, Status, RequestedBy/At, ApprovedBy/At, RejectionReason) · Stocktake(Number, Status, Lines(ProductId, SystemQuantity, CountedQuantity, Variance), StartedBy/At) · TransferOrder(Number, From, To, Status, Lines(ProductId, Quantity)) · ValuationSnapshot(PeriodCode, SnapshotDate, TotalValue, Lines) · BarcodeMapping(Barcode, ProductId). All rows carry the tenant.

## 8. Integrations
Published (RabbitMQ `warehouse.*`, Outbox): InventoryUpdated / Reserved / Insufficient, StockAdjustmentApproved / Rejected, StockAlert, StockMovementRecorded (Accounting books inventory from it), StocktakeCompleted, TransferOrderCompleted, ReceiptCompleted, InventoryValuationSnapshotClosed. Consumed: Sales order events (reserve / release / ship), Purchasing `PurchaseOrderReceived`, POS transactions. Jobs: daily replenishment scan, reservation expiry.

## 9. Reports
Low-stock flag and counters in the stock list; valuation snapshots by period (lines and total); no export.

## 10. Assumptions
Locations belong to a warehouse (the warehouse is optional on a location); quantities are whole numbers; products come from ProductService (the screens show the product name on the stock pages, the raw product id on adjustments, stocktake lines and valuation lines).

## 11. Open issues (also in DEFERRED-ISSUES §6)
- **Fixed in this batch:** the Admin sent the adjustment *reason* as a name ("Damaged") and the API, which takes the enum number, answered HTTP 400 — **no stock adjustment request could be created from the Admin**; now the number is sent. `serverMessage` now also reads a validation `errors` map, so such a refusal is no longer a blank "Failed to save".
- **R86 (security, backend):** the barcode lookup caches `wh:barcode:{barcode}` for 60 s and the inventory read `wh:inventory:{productId}` for 2 min **without the tenant in the key**; the same barcode value in two shops can return the other shop's product. Violates the multi-tenant caching rule.
- **R87:** product ids instead of names on adjustments, stocktake lines, transfer lines and valuation lines; a stocktake always covers the whole warehouse (no scope, no per-location count); transfers cannot be cancelled once shipped from the screen; no receipt / put-away, movement-history, reservations, lot / serial maintenance screens; the old `module-07` spec still lists them as if they existed.
- The warehouse container logged `RabbitMQ BrokerUnreachable` errors during the walk (broker was healthy): check the connection retry at start-up.

## 12. UAT scenarios
1. Product detail → Request adjustment → enter 0 → refused; enter −7, reason Damaged → toast "waiting for approval"; Stock Adjustments (Pending) lists −7 in red.
2. Reject it without a reason → "Bắt buộc"; with a reason → leaves the Pending list; stock on the product page is unchanged.
3. Stocktakes → Start → open it → Complete Count → the dialog says stock is not changed yet → Cancel → still In Progress → Cancel Stocktake → confirm → Cancelled, no buttons.
4. Transfers → New → same source and destination → "Vị trí nguồn và đích phải khác nhau"; empty lines → "Please add at least one line item"; valid → Draft listed with location codes.
5. Ship → dialog says goods leave the source → Esc → still Draft; Cancel → confirm → Cancelled.
6. Locations → filter by warehouse → rows narrow; filter with no match → "Không có kết quả" + Xóa lọc; occupancy ≥ 90 % shows a red bar.
7. Warehouses → deactivate → confirmation; reactivate is immediate.
8. Valuation Snapshots → period code → Close Period → dialog says the period is locked → Esc → nothing closed.
9. Product detail → Lots / Serials / Cost history tabs → each shows its table or "No records".
