# Warehouse - production movements, cost on the movement, idempotency (W3 of the Warehouse upgrade)

Added 2026-11-10. Decision record: `docs/architecture/adr-warehouse-upgrade.md`. Follows `StockByWarehouseAndLot.md` (W2). Reservations by source, partial consume and FEFO suggestion come in W4; the internal write API for Manufacturing (with its own permission and the lot / serial checks) in W5.

## 1. Purpose and actors
Manufacturing must move stock in ways the shop flows never did: issue material to a production order, take back what was not used, receive the finished good (maybe into a lot waiting for quality control), scrap, release a lot after the check, send stock to a subcontractor. The Warehouse now records these as six new movement types, stores **the cost and value on every movement** (until now they existed only inside the event) and makes a repeated request harmless.

| Actor | What they do |
|---|---|
| Production / planner (via Manufacturing, from W5) | Issues, returns, receives and scraps against a production order document. |
| Storekeeper | Sees warehouse, lot, cost and value of each movement; hands a lot over to quality control. |
| QC inspector | Releases a lot on hold (`QcRelease`). |
| Cost accountant | Reads the persisted unit cost and value of every movement; knows production movements are not in the ledger yet. |
| Warehouse manager | Sends stock to a subcontract warehouse. |

## 2. Requirements and acceptance criteria
| ID | Requirement | Acceptance |
|---|---|---|
| WM-1 | Six production types exist: ProductionIssue 7, ProductionReturn 8, ProductionReceipt 9, ProductionScrap 10, QcRelease 11, SubcontractTransfer 12. | Each is accepted by `POST movements` and changes stock as the table in 3 says. |
| WM-2 | Every movement stores warehouse, lot, unit cost, value and its signed effect on the item total (`StockDelta`). Older rows stay empty ("not recorded"). | History shows the fields for new rows of every type, also the older ones (manual sale). |
| WM-3 | A production movement carries a reference `<document number>#<line>`; the same reference twice is one movement. | Second call answers `replayed: true` with the first movement id and stock is unchanged; a missing reference answers 400. |
| WM-4 | Two identical requests at the same time never create two movements. | Live: one answers 201, the other 409 (it retries and gets the replay); exactly one row exists. |
| WM-5 | A receipt may start a lot in QcHold; a lot on hold is never allocated or issued until `QcRelease`. | Reservation skips a held lot although it expires first; after the release it is taken first. |
| WM-6 | Issue / scrap are valued at the cost in force; return / receipt at the cost given; release and transfer carry no value. | Issue 2.5 kg at cost 4 = value -10; receipt 5 kg at 9 = +45; scrap 0.5 kg of a lot at 9 = -4.5. |
| WM-7 | Production movements are not booked by Accounting until the WIP posting of Manufacturing phase 6. | The journal has no entry for them while a manual sale still is booked; the handler logs the skip. |
| WM-8 | Stock stays consistent: the balances add up to the item total and each lot to its balance after every movement. | `inventory/reconciliation` shows no quantity or lot drift. |

## 3. Business rules
| ID | Rule |
|---|---|
| WM-R1 | `Quantity` of a production movement is always positive; the type gives the direction (the older types keep their mixed signs, so `StockDelta` is the reliable signed figure). |
| WM-R2 | **ProductionIssue**: item total, the named lot and its balance go down; refused when the stock is not enough unless `allowNegative`; refused for a lot that is not Available. Without a lot, the lot-less stock of the named warehouse is used first (W2 pool rule). |
| WM-R3 | **ProductionScrap** is like an issue but is allowed on a lot on hold (a failed lot must be written off). |
| WM-R4 | **ProductionReturn** goes back into the named lot (or lot-less); the item cost is re-averaged at the cost given (the issue cost) under Average valuation. |
| WM-R5 | **ProductionReceipt** adds stock at the cost given (default: the item cost); with a lot number it creates a lot (`Source = Production`, expiry, manufacture date) in the named warehouse, optionally starting QcHold or Blocked. |
| WM-R6 | **QcRelease** names the lot; the lot must be on hold or blocked, otherwise refused; it becomes Available. The movement records what is left in the lot; stock and value do not change. |
| WM-R7 | **SubcontractTransfer** moves lot-less stock, or a whole lot, from one warehouse to another (`warehouseId` to `toWarehouseId`); the item total does not change; a lot moves only as a whole. |
| WM-R8 | Cost of an issue: Average valuation = the item's average cost; FIFO / LIFO = the cost of the lot taken from, else the item cost. |
| WM-R9 | The unique index `UX_StockMovements_Idempotency (tenant, product, reference, type)` protects **only the production types** (type >= 7). The older types keep their pre-check because the live data holds 750 repeated (product, reference, type) groups. |
| WM-R10 | One reference = one line: a document issuing the same item from two lots uses `#1` and `#2`. |

## 4. Use cases
**UC-1 Issue material (main).** Manufacturing issues 2.5 kg from lot LA for `MO-2611-00001#1`: stock 10 -> 7.5, lot LA 7.5, value -10 at cost 4. *Alternative:* no lot named - taken from the lot-less stock of the warehouse. *Exception:* not enough stock - 400/409 and nothing changes; lot on hold - refused.
**UC-2 Repeat the same request.** The network failed after the first call; the caller sends it again: the answer is the first one with `replayed: true`, stock unchanged. *Exception:* two copies at the same moment - one answers 409; retrying gives the replay.
**UC-3 Receive the finished good on hold.** Receipt of 5 kg into lot `FG-1` with `initialLotStatus = QcHold`; sales and production cannot take it. QC inspects, `QcRelease` makes it Available.
**UC-4 Return unused material.** Return 1 kg into the same lot at the issue cost; the item cost is re-averaged.
**UC-5 Scrap.** Scrap 0.5 kg of a lot (even one on hold) at its cost; value goes down.
**UC-6 Send to a subcontractor.** Move 1 kg from warehouse A to the subcontract warehouse B; totals unchanged.

## 5. Data model
`StockMovements` + `WarehouseId`, `LotId`, `UnitCost` decimal(18,6), `ValueDelta` decimal(18,2), `StockDelta` decimal(18,6), all nullable. `MovementType` int enum extended with 7-12. Filtered unique index `UX_StockMovements_Idempotency` (`Reference IS NOT NULL AND MovementType >= 7`). SQL `src/Database/Warehouse/012_StockMovementCostAndIdempotency.sql` (idempotent; prints the count of repeated groups among the older types, changes nothing). `StockMovementRecordedEvent` gains optional `WarehouseId`, `LotId`, `StockDelta`; `UnitCost` / `ValueDelta` now carry the persisted values.

## 6. Integrations
- **Accounting**: receives `StockMovementRecorded` as before; the six production names produce no posting (`PostingRules.ForStockMovement` returns null) and are logged at Information as "not booked until WIP posting exists". Tested for each name.
- **Reporting**: its warehouse consumer does not match `StockMovementRecorded`, so nothing changes.
- **Manufacturing**: will call these through the internal API of W5; until then the movements are reachable through the manual `POST movements` endpoint with its existing permission.
- **Admin**: no screen lists movement history today; the API type was added (`StockMovement`) for the future screen.

## 7. Permissions
Unchanged: the production types use the permission of `POST movements`. W5 adds the internal service permission.

## 8. Reports
`GET movements/{productId}` returns warehouse, lot, unit cost, value and stock delta for each row (empty for rows older than W3).

## 9. Assumptions and open issues
- **GL gap**: stock value and the general ledger differ by the production movements until Manufacturing phase 6 books WIP. Accounting logs each skipped movement with its value.
- Older rows have no cost, warehouse or lot: history before W3 is incomplete.
- Legacy idempotency stays a racy pre-check (750 repeated groups prevent a unique index).
- A caller must keep the `DocNo#line` convention; W5 enforces it in the internal API.
- A lot moves between warehouses only as a whole; splitting a lot is not built.
- `QcRelease` has no screen; there is no Admin screen for movement history.
- `ValueDelta` is rounded to 2 decimals per movement and may differ by a cent from quantity x unit cost for fractional quantities.

## 10. UAT
1. Receive 10 kg in lot LA at cost 4. Post `ProductionIssue` 2.5 kg, lot LA, reference `MO-1#1`: stock 7.5, value -10.
2. Post the same request again: `replayed: true`, same movement id, stock 7.5.
3. Post `ProductionReceipt` 5 kg, lot FG-1, expiry earlier than LA, cost 9, `initialLotStatus = QcHold`: lot reads QcHold.
4. Reserve 6 kg: nothing is taken from FG-1.
5. Post `QcRelease` for FG-1: Available; reserve 6 kg again: 5 from FG-1, 1 from LA; release the reservation.
6. `ProductionReturn` 1 kg into LA at cost 4, `ProductionScrap` 0.5 kg of FG-1: values +4 and -4.5.
7. `SubcontractTransfer` 1 kg of a lot-less item from warehouse A to B: A 2, B 1, total 3.
8. Open `inventory/reconciliation`: no quantity or lot line. Check the journal: no entry for the production movements.

## Addendum W7 (2026-11-11): the lots of a movement
A production movement used to answer one `LotId` (null when an issue drew from several lots) and stored nothing more, so which lots a reservation issue consumed was lost. Since W7 the movement keeps **rows** (`StockMovementLots`: lot, lot number as it was, quantity, unit cost, warehouse, parent lot, position) and answers them as `lots[]`; `LotId` keeps its meaning (the lot named in the request). Rows per type: reservation issue = one per lot taken; issue / scrap / return / quality release = the lot; receipt = the created lot; split transfer = the new sub-lot; lot-less = none. The unit cost of a row is the cost that values its share, so `valueDelta` of a reservation issue equals the sum of row quantity x row cost plus the lot-less remainder at the item cost. The same movement key answers the same rows in the same order. Details and acceptance criteria: ProductionIntegration.md (PI-13..PI-15, PI-R11..PI-R14).

## Addendum W6 (2026-11-10): expired lots
A `ProductionIssue` from an expired lot is refused `WH_LOT_EXPIRED` (TC-MAT-009 of the Manufacturing test book); `ProductionScrap`, `ProductionReturn`, `SubcontractTransfer` and `QcRelease` of an expired lot are still allowed. See ProductionIntegration.md (PI-R10).
