# Warehouse - decimal quantities (W1 of the Warehouse upgrade)

Added 2026-11-09. Decision record: `docs/architecture/adr-warehouse-upgrade.md` (W0). Scope of this document: milestone **W1 only** - every stock quantity is decimal; stock per warehouse / lot, production movements, reservations by source and the internal write API follow in W2-W5.

## 1. Purpose and actors
Manufacturing (and later Purchasing in kilograms, litres, cubic metres) needs fractional stock: 52.5 litres of paint, 1.1 m3 of wood. The Warehouse counted whole pieces only.

| Actor | What changes for them |
|---|---|
| Warehouse clerk / manager | Can receive, adjust, count, transfer and set stock policy in fractions (up to 6 decimals). Whole numbers look exactly as before. |
| Purchasing planner | A low-stock alert and a replenishment proposal carry decimal quantities (e.g. current 1.75, suggested 10.75). |
| Sales / POS | Unchanged: order lines and POS lines are whole numbers. Sales reads the Warehouse availability as a decimal. |
| Reporting analyst | Inventory movement facts and the product stock dashboard hold decimals. |
| Integrator | Event fields that carried an integer quantity now carry a decimal; an integer in an older message is still read. |

## 2. Requirements and acceptance criteria
| ID | Requirement | Acceptance |
|---|---|---|
| WQ-1 | Every quantity in the Warehouse (stock, reserved, locked, reorder / min / max / safety, lot, bin stock, movement, adjustment, stocktake, transfer, cost log, valuation line, location capacity, low-stock threshold) is `decimal(18,6)`. | Receive 2.5 kg -> stock 2.5; reserve 1.25 -> available 1.25; the lot holds 2.5. |
| WQ-2 | A whole-number flow behaves exactly as before. | Order for 2 pieces accepted, order for 50 refused with the stock message, reserve / release / manual sale unchanged (live script `wh-w1.js`). |
| WQ-3 | Events carry decimals; each consumer reads both integer and decimal. | Product shows `stockQuantity` 1.75 after an adjustment of -0.25; Purchasing request shows current 1.75 and suggests 10.75. |
| WQ-4 | No consumer stops or loses data when the Warehouse starts sending `5.000000`. | Sales still accepts orders (its Warehouse client is fail-closed); Reporting stores the real quantity, not 0; no consumer loops on a bad message. |
| WQ-5 | The database migration is idempotent and keeps defaults and nullability. | `010_DecimalQuantities.sql` run twice changes nothing; `LiveSchemaDriftTests` reports "No drift found" for every service. |
| WQ-6 | Screens accept fractions. | Admin inputs for adjustment, count, stock policy, transfer, location capacity use `step="any"`; the staff app accepts "2,5" and "2.5"; the storefront treats stock <= 0 as out of stock. |

## 3. Business rules
| ID | Rule |
|---|---|
| WQ-R1 | Quantities have at most 6 decimals. Decimal add and subtract are exact, so a count of 9.5 against 10 is a variance of exactly -0.5 (no rounding residue). |
| WQ-R2 | The Warehouse does not round a quantity; the unit-of-measure decimals (MasterData `DecimalPlaces`) are enforced by the caller (Manufacturing, Sales). |
| WQ-R3 | Money is still rounded to 2 decimals: `ValueDelta = round(quantity x unit cost, 2)`. For a whole quantity the result is unchanged; for a fraction it can differ by a cent from a hand calculation. |
| WQ-R4 | Order lines (`int`) and POS lines (`int`) stay whole numbers; stock may be fractional but cannot yet be *sold* in fractions through Sales or POS. |
| WQ-R5 | A purchase receipt or vendor return line keeps the decimal quantity from the purchase order (the old `(int)` cast that dropped the fraction is removed). |
| WQ-R6 | Location capacity and occupancy are decimals too, so that every quantity follows one rule. |

## 4. Use cases
**UC-1 Receive a fractional lot (main).** The clerk receives 2.5 kg with lot `LOT-K1` and expiry; stock 2.5, lot remaining 2.5, movement `Received` 2.5. *Alternative:* a whole quantity - same as before. *Exception:* a negative or zero lot quantity is refused as before.
**UC-2 Reserve and release a fraction.** Reserve 1.25 kg for an order; available 1.25, the lot shows 1.25 reserved; release restores 2.5 available.
**UC-3 Adjust by a fraction.** Request -0.25, the manager approves; stock 1.75; the event reaches Product (1.75) and, when below the minimum, Purchasing.
**UC-4 Low-stock proposal in decimals.** Stock policy minimum 3.5, maximum 12.5, safety 1.25; with 1.75 available the proposal suggests 10.75.
**UC-5 Count and transfer.** A stocktake line 10 counted 9.5 shows a variance of -0.5; a transfer line may be 0.1.

## 5. Data and event model
**SQL `src/Database/Warehouse/010_DecimalQuantities.sql`** changes (INT -> DECIMAL(18,6), nullability and default values kept, defaults renamed `DF_<Table>_<Column>`): `Inventories` (Total, Reserved, Locked, ReorderPoint, ReorderQuantity, MinStock, MaxStock, SafetyStock), `InventoryLots` (Received, Remaining, Reserved), `InventoryLocationStocks` (3), `StockMovements.Quantity`, `Reservations.Quantity` (legacy), `Reservation_ReservedItems.Quantity`, `StockAdjustments.QuantityDelta`, `StocktakeLines` (System, Counted), `TransferOrderLines.Quantity`, `CostConsumptionLogs.Quantity`, `InventoryValuationSnapshotLines.Quantity`, `WarehouseLocations` (Capacity, CurrentOccupancy), `TenantInventorySettings.LowStockThreshold`. The persisted computed column `Inventories.AvailableQuantity` and the indexes `IX_Inventory_LowStock`, `IX_InventoryLots_Product` are dropped and recreated.
**Consumer scripts (deploy first):** `Product/019` (`Products.StockQuantity`), `Purchasing/014` (`PurchaseRequests` 8 quantity columns), `Reporting/010` (`FactInventoryMovements.Quantity`, `DimProducts.StockQuantity`).
**Events** (decimal fields): `StockMovementRecorded.Quantity`, `InventoryUpdated.PreviousQuantity / CurrentQuantity`, `InventoryReserved` item quantity, `InventoryInsufficient`, `ReceiptCompleted.QuantityReceived`, `StockAdjustmentApproved.QuantityDelta`, `StockAlert` (current, reorder point / quantity, available, min, max, safety). Counters such as `LineCount` stay integers.

## 6. Integrations (who reads what)
| Consumer | Change |
|---|---|
| ProductService | `InventoryUpdated.CurrentQuantity` and `Product.StockQuantity` decimal. |
| PurchaseOrderService | `StockAlert` payload, `PurchaseRequest` quantities, replenishment calculator and the on-order quantity (no more rounding up) decimal. |
| SaleOrderService | `IWarehouseServiceClient.CheckAvailabilityAsync(decimal)`; availability payload decimal; the refusal message prints the quantity without trailing zeros. Order lines stay int. |
| ReportingService | `JDoc.GetDecimal` for the movement quantity; fact and dimension columns decimal; dashboard DTOs decimal. |
| Accounting | Reads only `ValueDelta`: no change. |
| Shipping, Notification, ChatBot, Manufacturing | No quantity field read: no change (ChatBot passes the JSON through). |

## 7. Permissions
Unchanged (`inventory:*`).

## 8. Deployment order (important)
1. Run `Product/019`, `Purchasing/014`, `Reporting/010`; deploy Product, Purchasing, Sales and Reporting (they must read decimals first: an integer reader throws on `5.000000`, the Sales client is fail-closed and would reject every order, Reporting would silently store 0).
2. Run `Warehouse/010`, deploy the Warehouse.
3. Deploy the Admin and Storefront; the mobile apps are code only (no build was made).

## 9. Assumptions and open issues
- Reporting's Kafka consumer matches the type names `StockMoved / InventoryMoved / InventoryAdjusted / ReceiptCompleted / InventoryReserved`; `StockMovementRecordedEvent` does not contain any of them, so the Warehouse movement event may not reach `FactInventoryMovements` at all (pre-existing; recorded, not changed).
- Stock cannot be *sold* in fractions yet (Sales and POS lines are integers); a Sales / POS quantity change is a separate task.
- The weighted average cost is still stored with 2 decimals (`Inventories.Cost`); 6-decimal unit costs arrive with W3.
- No Warehouse integration tests exist; the live script and the schema-drift test stand in.
- The mobile apps and the Storefront changes are covered by their unit tests only (no device build).

## 10. UAT
1. Receive 2.5 of a kilogram product with a lot: stock 2.5, lot 2.5.
2. Reserve 1.25 through *inventory/reserve*: available 1.25; release: 2.5.
3. Request an adjustment of -0.25 and approve: stock 1.75; the product list shows 1.75.
4. Set the stock policy 3.5 / 12.5 / 1.25: the Purchasing proposal shows current 1.75, suggested 10.75.
5. Create a sales order for 2 whole pieces of a stocked product: accepted; for more than the stock: refused with the stock message.
6. In the Admin stock adjustment dialog type 0.25: accepted (no "step" warning).
