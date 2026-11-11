# Warehouse - internal stock API for Manufacturing (W5 of the Warehouse upgrade)

Added 2026-11-10. Decision record: `docs/architecture/adr-warehouse-upgrade.md`. Follows `ReservationsBySource.md` (W4) and `ProductionMovements.md` (W3). Manufacturing's own use of this API (production orders) arrives in its Phase 3; W5 delivers and proves the Warehouse side. **First real caller (2026-11-10, P3-2):** releasing, unreleasing, cancelling and resizing a production order read availability and make / read / release the production reservation through this API (`docs/business/Manufacturing/ProductionOrders.md` part 2). **Issue and return (2026-11-10, P3-3):** Manufacturing books `ProductionIssue` (against the order's reservation and from free stock, with lot) and `ProductionReturn` through this API, one movement per document line under `<DocNo>#<n>` (free part `...F`, reversal `...R`); see `docs/business/Manufacturing/MaterialIssueReturn.md`.

## 1. Purpose and actors
Until W5 the production movements and the production reservation could only be reached through user endpoints, which a service cannot call. W5 gives ManufacturingService a service-to-service door - `internal/v1/stock/*` - that is safe to retry, honours the item's lot / serial rules and never lets one document line move stock twice.

| Actor | What it does with the API |
|---|---|
| ManufacturingService (caller `manufacturing`) | Plans against availability, reserves components, issues / returns / scraps material, receives finished goods (under quality hold), releases or blocks lots. |
| Production planner | Sees the effect in the Admin stock pages (balances per warehouse and lot, movement history, reservations). |
| Storekeeper | Still issues by hand through the user endpoints; the same ledger and the same rules apply. |
| Warehouse manager | Knows the contract: key format, replay, refusals; watches reconciliation. |
| Quality inspector | Puts a lot on hold / blocks it; releases it by a QcRelease movement. |

## 2. Requirements and acceptance criteria
| ID | Requirement | Acceptance |
|---|---|---|
| PI-1 | A service token with caller `manufacturing` is the only way in; no user token, no anonymous call, not routed by the Gateway. | Call without a token: 401 (checked from inside the network); `/wh/internal/...` through the Gateway: 404. |
| PI-2 | Every stock movement carries an idempotency key `<document number>#<line>` (e.g. `MO-2611-00001#3`), at most 100 characters. | `MO-1`, `#1`, `MO-1#`, empty: 400 `WH_REFERENCE_FORMAT`. |
| PI-3 | The same key with the same payload answers the first result again and changes nothing. | Second identical issue: `replayed: true`, same movement id, stock unchanged. |
| PI-4 | The same key with another quantity, warehouse or lot is a caller error, not a replay. | Issue 3 then 4 under one key: HTTP 409 `WH_IDEMPOTENCY_CONFLICT`, stock unchanged. |
| PI-5 | Two parallel identical calls book one movement. | One answers 201, the other 409 (race) or the replay; after a retry exactly one movement exists. |
| PI-6 | Only the six production movement types are accepted. | `Sold`, `Received`, unknown: 400 `WH_MOVEMENT_TYPE`. |
| PI-7 | A serial-controlled item (flag passed by the caller) is refused for every stock-moving production type. | 400 `WH_SERIAL_NOT_SUPPORTED`, nothing changes. |
| PI-8 | A lot-controlled item must be moved by lot: a receipt needs a lot number; return, scrap and subcontract transfer name the lot; an issue names the lot or comes from a reservation whose lots cover it. | 400 `WH_LOT_REQUIRED`; an unflagged item is unaffected. |
| PI-9 | Every movement answer carries the unit cost, the value, the warehouse and the lot booked. | Issue 3 at lot cost 4: unit cost 4, value -12. |
| PI-10 | A lot can be put on `QcHold` or `Blocked`; the way back is the `QcRelease` movement. | Hold: not suggested, not reservable, not issuable; release: allocatable again; asking for `Available` here: 400 `WH_LOT_STATUS`. |
| PI-11 | A production reservation is made, read and released through the API; release is idempotent. | Release twice: both answer Released, stock freed once; unknown id: 404. |
| PI-12 | Availability and lot suggestion are readable per warehouse. | Balances per warehouse / lot with status and expiry; suggestion first-expiry-first with shortfall. |
| PI-13 | (W7) A **part** of a lot can move to another warehouse: the part becomes a sub-lot. | Lot of 10 on hold, move 4: the parent keeps 6, sub-lot `<lot>-S1` of 4 sits in the target warehouse with the same cost, expiry, status and a `parentLotId`; the item total is still 10; balances 6 + 4. |
| PI-14 | (W7) A production movement answers the lots it really used. | Issue of 52.5 over two lots: `lots` = A 30 and B 22.5 with their costs, `lotId` empty; the same key again answers the same rows in the same order; the movement history shows them. |
| PI-15 | (W7) A lot on quality hold can be issued for rework, on request only. | Without the flag: 400 `WH_LOT_NOT_USABLE`; with `allowHeldLot`, the lot and a reason: booked, the reason is on the movement; a Blocked or expired lot is still refused; a user calling the public endpoint cannot set the flag. |

## 3. Business rules
| ID | Rule |
|---|---|
| PI-R1 | The key is the movement `Reference`. The unique index of W3 (production types) is the final guard; the service checks first and returns the replay. |
| PI-R2 | A replay compares quantity, warehouse and lot (when both sides name them); a `QcRelease` skips the quantity comparison because its quantity is the lot's remainder. |
| PI-R3 | The conflict is HTTP 409 **with** `errorCode: WH_IDEMPOTENCY_CONFLICT` and is never retried; a 409 **without** a code is an optimistic-locking conflict and may be retried (the repeat of an idempotent write returns the first result). |
| PI-R4 | The lot / serial flags are read by the caller from Product (fail closed) and passed in the request; the Warehouse has no copy of product master data. The user endpoint `POST movements` does not check them. |
| PI-R5 | Per-serial tracking is not on the production path: a serial-controlled item is refused rather than moved without its serials. |
| PI-R6 | A receipt can start a lot in `QcHold`; a held lot is neither suggested nor reserved nor issuable until released. |
| PI-R7 | The reversal of a receipt is an issue of the lot under a new key (`<doc>#FG1-REV`); there is no separate reversal type. |
| PI-R8 | Production movements are not booked in the general ledger until the WIP posting of Manufacturing phase 6 (W3 rule, unchanged). |
| PI-R9 | Releasing a reservation that is no longer open (released, expired, fulfilled) answers its state and frees nothing. |
| PI-R10 | (W6) A `ProductionIssue` from an **expired** lot - named, or drawn from a reservation whose lot expired after the reservation was made - is refused `WH_LOT_EXPIRED` (TC-MAT-009). Scrap, return, transfer and quality release of an expired lot stay allowed so that it can be written off or moved. |
| PI-R11 | (W7) **A part of a lot moves as a sub-lot.** `SubcontractTransfer` of a lot: the whole quantity moves the lot itself (its reserved quantity with it); a part (0 < quantity < remaining) is split: only the part that is **not reserved** (`remaining - reserved`) may leave (more: `WH_LOT_RESERVED`; more than the lot holds: `WH_LOT_QUANTITY`). The parent loses the quantity from remaining **and** received quantity (the tree still adds up to the original receipt). The sub-lot keeps product, unit cost, expiry, manufacture date, source, purchase order, **status** (a held lot's piece stays held until it is released) and received date (first-expiry-first ties stay stable). No value moves and no event is raised. |
| PI-R12 | (W7) **Sub-lot numbers** are `<root>-S<n>`: the root is the number of the topmost ancestor (a sub-lot of a sub-lot is named from the same root), n the smallest positive integer not used by any lot of the product - used-up lots included, so a number is never reused (longer than 100 characters: `WH_LOT_NUMBER_LENGTH`). Two parallel splits that pick the same number meet the unique lot-number index: the loser answers 409 and reads again. Rework lots `-R<n>` are Manufacturing's (Phase 4) and travel as an ordinary `lotNumber` of a receipt. |
| PI-R13 | (W7) **The lots of a movement.** `lotId` of the answer stays the lot **named in the request** (null when an issue drew from several lots), so a replay compares equal; `lots[]` says which lots were used: one row per lot taken by a reservation issue (a lot-less remainder is not a row, so the rows may add up to less than the quantity), the lot of a single-lot issue / scrap / return / quality release, the created lot of a receipt, and for a split transfer the **new sub-lot** (with `parentLotId`). Each row has the cost used for that lot's share of the value (the lot's cost under FIFO / LIFO, else the item cost; the lot's own cost for movements that carry no value) - the same figure that values the movement - and a position, so a replay lists the rows exactly as the first answer did. Movements booked before W7 have no rows (`lots: []`). |
| PI-R14 | (W7) **Issue of a held lot.** `allowHeldLot = true` applies to `ProductionIssue` only (other types: `WH_MOVEMENT_FLAG`), needs the named lot (`WH_LOT_REQUIRED`) and a non-empty `reason`, stored on the movement as the audit trail (`WH_REASON_REQUIRED`). It lets a lot on **QcHold** through; **Blocked** (withdrawn) and expired lots are still refused. Only the internal service API sets it: the user endpoint resets it to false before the command is sent (an architecture test proves it), because a user holding `inventory:manage` must not be able to issue held stock. |

## 4. Use cases
**UC-1 Produce one order (main).** Manufacturing asks availability and lot suggestion, reserves the components in the raw-material warehouse, issues them in pieces against the reservation (`MO#1`, `MO#2`), receives the finished goods under quality hold at the actual cost (`MO#FG1`), releases the lot (`MO#QC1`).
*Alternative A - give back / scrap:* a return at the issue cost (`MO#R1`) and a scrap (`MO#S1`) name the lot.
*Alternative B - failed inspection:* the lot is blocked (`PUT lots/{id}/status`), later released by a QcRelease movement.
*Alternative C - undo:* the receipt is reversed by an issue of the lot under a new key.
**UC-2 Retry after a lost answer.** The caller repeats the call with the same key and payload and receives the first result; no second movement.
**UC-3 Wrong retry.** The caller reuses a key with another quantity: 409 `WH_IDEMPOTENCY_CONFLICT`; the caller fixes its document line number.
**UC-4 Controlled items.** A lot-controlled receipt without a lot number, or a serial-controlled item, is refused with its code; Manufacturing shows the code to the planner.
**UC-5 Cancel the order.** Manufacturing releases the reservation; the unconsumed rest is freed once, a second release changes nothing.
**UC-6 (W7) Decide a non-conforming lot in parts.** A lot of 100 sits on hold in the quarantine warehouse. Manufacturing moves 85 to the finished-goods warehouse (sub-lot `-S1`, still held), releases that sub-lot alone (`QcRelease`), scraps 5 from the parent (`ProductionScrap`) and keeps 10 on hold for rework. *Alternative:* more than the free part is asked while some is reserved - `WH_LOT_RESERVED`, nothing changes. *Exception:* a retry of the same key returns the same sub-lot, never a third lot.
**UC-7 (W7) Rework consumes the held lot.** The rework order issues the 10 pieces of the held lot (`ProductionIssue`, named lot, `allowHeldLot`, reason `Rework RW-...`). Without the flag the Warehouse refuses; the stock leaves the lot, which stays on hold until the reworked pieces are inspected.
**UC-8 (W7) Trace what an issue used.** An issue over lots A and B answers both rows; traceability (Manufacturing Phase 4) stores them next to its document line.

## 5. Endpoints (route `internal/v1/stock`, caller `manufacturing`)
| Route | Purpose |
|---|---|
| `GET {productId}/availability?warehouseId=` | item total / reserved / locked / available, unit cost, balances per warehouse and lot |
| `GET {productId}/suggest-lots?quantity=&warehouseId=` | first-expiry-first lots, shortfall, lot-less stock |
| `POST reservations` | hold components for a production order in one warehouse (one open reservation per order; repeat = same, `replayed`) |
| `GET reservations/{sourceType}/{sourceId}` | reservation with consumed / remaining per line and lot (404 when none) |
| `POST reservations/{reservationId}/release` | free the unconsumed rest (idempotent) |
| `POST movements` | one production movement; body = movement type, product, quantity, `idempotencyKey`, optional warehouse / to-warehouse / lot / lot number / expiry / manufacture date / unit cost / initial lot status / reservation / source line / allow negative / `requiresLot` / `requiresSerial` / (W7) `allowHeldLot` + `reason`; answers the movement, cost, value, `warehouseId`, `lotId`, (W7) `lots[]` {lotId, lotNumber, quantity, unitCost, warehouseId, parentLotId} and `replayed` |
| `GET {productId}/lots` | every lot of the item including used-up ones; (W7) with `parentLotId` |
| `PUT lots/{lotId}/status` | `QcHold` or `Blocked` (same status = unchanged) |

Errors: 400 with `errorCode` (`WH_MOVEMENT_TYPE`, `WH_REFERENCE_FORMAT`, `WH_SERIAL_NOT_SUPPORTED`, `WH_LOT_REQUIRED`, `WH_LOT_STATUS`, `WH_LOT_NOT_USABLE`, `WH_INSUFFICIENT_STOCK`, `WH_RESERVATION_*`, (W7) `WH_LOT_QUANTITY`, `WH_LOT_RESERVED`, `WH_LOT_NUMBER_LENGTH`, `WH_REASON_REQUIRED`, `WH_MOVEMENT_FLAG` ...), 404 unknown record, 409 (`WH_IDEMPOTENCY_CONFLICT` with code; optimistic locking without).

## 6. Data and integrations
W5 added no schema (it uses the columns and the unique index of W2-W4). **W7 (SQL `Warehouse/014_LotSplitAndMovementLots.sql`):** `InventoryLots.ParentLotId` (self reference, filtered index) and the table `StockMovementLots` (tenant, movement, **position**, lot id, lot number as it was, quantity, unit cost, warehouse, parent lot; removed with the movement). Nothing is backfilled. Manufacturing side: port `IWarehouseStockClient` (`ERP.MFG.Application/Abstractions/StockPorts.cs`) and `WarehouseStockClient` (no cache, 15 s timeout, retries an uncoded 409 three times, maps a Warehouse refusal to a business rule carrying the `WH_*` code, an unreachable service to `MFG_INT_001`). Events are unchanged: movements still raise `StockMovementRecorded` and `InventoryUpdated`; a production reservation raises none.

## 7. Permissions
Service-to-service only (`[AuthorizeInternalService("manufacturing")]` on every action). The user endpoints keep `inventory:view` / `inventory:manage`.

## 8. Dev-only cycle runner
`POST /mfg/api/v1/manufacturing/seed/stock-cycle` (404 unless `Manufacturing:EnableDemoSeed`; permission `manufacturing:settings:manage`) plays one order through the real API: product flags, availability, suggestion, reserve (+ replay), issue in two pieces (+ replay + conflict probe), reservation fulfilled, return, scrap, receipt under QC hold, hold not allocatable, release, block, release again, reverse, three refusal probes (legacy type, key format, missing lot number), end state. It returns the step list and writes nothing in Manufacturing's tables.

`POST /mfg/api/v1/manufacturing/seed/lot-cycle` (same switch and permission; W7) plays the lot scenario through the same internal API, which is not reachable from outside: a lot received on hold, a partial transfer (split, replay, too much), release of the sub-lot alone, the hold / release states, an issue of the held lot refused without the flag and without a reason and booked for rework with both, and - when a component is named - a reservation issue over several lots with its breakdown and its replay.

## 9. Assumptions and open issues
- The flags are trusted from the caller; a Manufacturing defect could skip the lot rule (the user endpoint has no check at all).
- Serial-controlled items cannot be produced or consumed until a serial epic exists (the Warehouse has a serial register for receipts, but it is not on the production path).
- The key format is enforced on the internal route only; the user route keeps any non-empty reference.
- Legacy movement types remain only pre-checked (750 historical duplicate groups, W3 risk).
- Warehouse and Manufacturing documents are not one transaction: Manufacturing's pending-document / retry pattern (Phase 3) completes the picture; W5 makes every Warehouse call safe to repeat.
- A forgotten production reservation holds stock for ever (R202); release is now callable, nothing calls it yet.
- (W7) `allowHeldLot` is one flag guarded by "internal API only + a reason"; the Warehouse has no per-caller allow-list (the service token already limits the route to Manufacturing).
- (W7) A split moves only the free part; moving "everything but what is reserved" is the caller's two-step decision. A whole-lot move still carries the lot's reservation to the new warehouse.
- (W7) The breakdown starts with W7: genealogy built on it covers documents posted after the deployment (older movements have no rows).
- (W7) `StockMovementRecorded` does not carry the lot rows (no consumer needs the split); Accounting and Reporting are unaffected.

## 10. UAT
1. Receive two lots of a lot-controlled raw material in warehouse A (LB earlier expiry) and one in B; receive 1 of the finished good (so it has a stock record).
2. Run the cycle (`wh-w5.js`) - every step ok: reserve 8 in A (LB 4 then LA 4, B untouched), issue 3 (value -12) and again (replay), issue 4 under the same key (409), issue the last 5 (Fulfilled), return 1, scrap 0.5.
3. Finished lot received in QcHold at 6.40 (32 / 5 units): not suggested; released: suggested; blocked then released; reversed.
4. Raw total 7.5, nothing reserved; finished total 1; reconciliation has no line for these items; Accounting has no entry for the production movements.
5. A serial-controlled component stops the cycle with `WH_SERIAL_NOT_SUPPORTED` and leaves stock untouched.
6. Without a service token: 401; through the Gateway: 404.
7. (W6) Issue 5 from an expired lot: 400 `WH_LOT_EXPIRED`, stock unchanged; scrap the same lot: accepted.
8. (P3-4) Backflush of an operation report is the second caller of `ProductionIssue`: keys `BF-yymm-nnnnn#n` (reserved part) and `...#nF` (free stock, one line per lot); a shortage is refused before anything is booked, a flagged negative line is sent with `AllowNegative`; a refused line is reversed with `ProductionReturn` under `...R`. See `docs/business/Manufacturing/OperationsAndBackflush.md`.
9. (P3-5) `GET internal/v1/stock/{productId}/lots` lists every lot of an item including used-up ones; the first `ProductionReceipt` of an item with no inventory record creates it from the optional product name and SKU (any other movement of such an item: `WH_NOT_FOUND`). A duplicate lot number is not refused by the Warehouse; the caller checks the list. See `docs/business/Manufacturing/FinishedGoodsAndClosing.md`.
10. (W7, `wh-w7.js`) Receive 10 of a lot-controlled finished good on QcHold in the quarantine warehouse; move 4 to finished goods: parent 6 / sub-lot `-S1` 4 (same cost and expiry, still held, `parentLotId`), item total unchanged, balances 6 and 4, reconciliation clean; repeat the call: same sub-lot, no third lot; another quantity under the key: 409; a second split: `-S2`; 50 asked: `WH_LOT_QUANTITY`. Release the sub-lot: only it becomes Available. The public endpoint with `allowHeldLot: true`: still `WH_LOT_NOT_USABLE`. Run the lot cycle: the held lot is refused without the flag and without a reason, booked for rework with both (the lot keeps its hold, the reason is in the movement history); an issue of 52.5 of paint over reservation lots A / B answers two rows (A 30, B 22.5) and the history shows them; a replay lists them in the same order.
