# Manufacturing - production orders (Phase 3: P3-1 creation, snapshot, schedule; P3-2 release, reservation, unrelease, cancel, hold, change of quantity)

Added 2026-11-10. Source: `docs/business/M/01_PRODUCTION_BA_SPEC.md` §4.2-4.3, §5.10 (BR-PO-01..06, BR-PO-19 data), §7.3; tasks T3.1, T3.2, T3.3, T3.11; tests TC-PO-001..010, 039..042, TC-SCH-001..005. Decision record: `docs/architecture/adr-manufacturing-phase3.md`. Release, reservation, unrelease, cancel, hold / resume and change of quantity (P3-2) are in part 2 at the end of this file (sections 12-22); material issue and return (P3-3) are in `MaterialIssueReturn.md`; operations, receipts and closing follow in P3-4..P3-5 and are documented here as they arrive.

## 1. Purpose and actors
A production order says *what to make, how many, by when* and freezes the recipe (BOM) and the route (routing) it will follow, so that later engineering changes do not move work already planned. In P3-1 an order can be created, adjusted while it is a Draft, scheduled and looked up; it does not touch stock yet.

| Actor | What they do here |
|---|---|
| Production planner | Creates orders, chooses quantity, due date, priority, BOM / routing version; reads the plan (start, end, late flag). |
| Production manager | Same, and later releases / holds / closes (next milestones). |
| Shop-floor supervisor, storekeeper, accountant | Read orders (`order:view`); act on them in later milestones. |
| Administrator | Grants the permissions (Roles screen). |

## 2. Process
Create (item, quantity, due date, optional priority / BOM / routing / output warehouse) -> the system checks the item, picks the BOM and routing, explodes the BOM one level into materials (phantoms passed through) and outputs, copies the routing into operations, schedules the order and numbers it `MO-yyMM-nnnnn` -> the order is a **Draft** -> the planner may change quantity / due date / priority / versions (a fresh snapshot or a new schedule) -> the status then moves along the diagram in section 5 (Release onwards: P3-2).

## 3. Requirements and acceptance criteria
| ID | Requirement | Acceptance (test) |
|---|---|---|
| PO-1 | An order has a number `MO-yyMM-nnnnn`, unique in the tenant, that nobody can edit. | First order of November 2026 is `MO-2611-00001`; the next `...00002`; December restarts at `...00001` of `2612` (`Order_Numbers_Count_Per_Month`); 20 parallel creations get 20 different numbers (live). |
| PO-2 | Quantity is greater than zero and has at most the decimals of the item's unit. | 0 and -5: `MFG_PO_001` (TC-PO-002); 10.5 pieces refused (TC-PO-003); 10.25 kg accepted. |
| PO-3 | The due date is not before today (plant time). Today is accepted and the order is late. | 2026-11-03 on 11-04: `MFG_PO_003` (TC-PO-004); 11-04: created, `isLate` (TC-PO-005). |
| PO-4 | Priority is 1 (highest) to 5, default 3. | 0 and 6 refused (TC-PO-010). |
| PO-5 | Only an active Make (or Both) item without serial control can be produced; every component is active and not serial-controlled. | Buy item `MFG_ITM_001`; inactive item / component `MFG_PO_006` naming the code; serial item / component `MFG_MAT_011`. |
| PO-6 | The BOM is the approved version effective on the reference date; a chosen version must be approved and effective. | No effective BOM `MFG_PO_004` with the date (TC-PO-006); chosen version not yet effective or a Draft `MFG_PO_005` (TC-PO-007). |
| PO-7 | Snapshot: one-level explosion with phantom pass-through, scrap and yield, unit conversion and round-up (BR-BOM-06); operations copied from the routing; by- and co-products become outputs. | MO-A (TP-BAN01 x 100): 7 materials MAT 100, CHAN 400, VIT 1 600, SON 52.50, KEO01 10, THUNG 100, XOP 200; no KEO02, no kit line; 3 operations; no output (TC-PO-001). BTP-MAT01 x 50: GO 1.1 m3 and output PP-MUN01 25 kg. |
| PO-8 | Each material is attached to the operation its BOM line names; if the routing has no such operation it goes to the first operation with a warning. | SON on operation 20, THUNG / XOP on operation 30 through the phantom kit. |
| PO-9 | Changing quantity, BOM or routing of a Draft rebuilds the snapshot; changing only due date, priority or notes keeps the lines and reschedules. | 100 -> 120: SON 63.00, CHAN 480, operation 10 = 15 + 120 x 10 = 1 215 minutes (TC-PO-008); the old lines are retired. |
| PO-10 | The snapshot does not follow later BOM versions. | A new approved BOM version does not change an existing order (TC-PO-009). |
| PO-11 | Only a Draft can be changed; a Closed order is read only (`MFG_PO_014`); an illegal status change is `MFG_PO_002` naming both statuses. | All 49 (from, to) pairs tested (TC-PO-041); Closed edit (TC-PO-039); Close from Released (TC-PO-040). |
| PO-12 | Every status change is kept in the history with who, why and when. | Six changes Draft -> ... -> Closed give six rows in order (TC-PO-042). |
| PO-13 | The schedule: backward from the end of the due date over the work centers' calendars; if that start lies before now, forward from now and the order is late. | Due Friday 13/11: start Wednesday 11/11, operation 20 on 12/11, operation 30 on 13/11 (TC-SCH-001); due 11/05: forward from now, late, ends after the due date (TC-SCH-002). |
| PO-14 | Sunday is skipped; a Saturday offers one shift. | Due Monday 16/11: nothing on 15/11, Saturday work only 06:00-13:30 (TC-SCH-003). |
| PO-15 | An order without routing starts its fixed lead time (working days) before the due date. | BTP-CHAN01 x 200, lead time 1, due Monday 16/11: start Saturday 14/11 (TC-SCH-004); too short a time: starts today, late. |
| PO-16 | A new due date of a Draft moves the whole schedule. | Due 11/20: start 11/18 (TC-SCH-005). |
| PO-17 | Orders are listed per plant with filters (status, item, due range, late, priority, text on number / item) and paging; one order comes with its lines; its history is available. A plant the caller may not see does not exist for them (404). | Query tests; live. |
| PO-18 | Creating and changing an order leave `ProductionOrderCreated` / `ProductionOrderStatusChanged` in the outbox in the same transaction. | Outbox tests; live: the row is published. |

## 4. Business rules (this milestone)
| ID | Rule | Code |
|---|---|---|
| BR-PO-01 | Number `MO-{yyMM}-{seq}`; **the counter is per tenant and month** (deviation from the per-plant counter of BR-GEN-04 - the number is the key the Warehouse deduplicates on); `OrderType` Standard / Rework / Subcontract, `SourceType` Manual / SalesOrder / Mrp / Ncr / ParentOrder (only Manual created in P3-1). | - |
| BR-PO-02 | Status changes follow section 5; history written. | `MFG_PO_002`, `MFG_PO_014` |
| BR-PO-03 | Item active Make / Both, quantity, due date, priority as in PO-2..5; output warehouse is one of the plant's. | `MFG_PO_001/003/006`, `MFG_ITM_001`, `MFG_MAT_011` |
| BR-PO-04 | BOM / routing choice as in PO-6; routing optional (BR-RT-07). | `MFG_PO_004/005` |
| BR-PO-05 | Snapshot and regenerate as in PO-7..10. | - |
| BR-PO-06 | Schedule as in PO-13..16, infinite capacity. | - |

## 5. State models
**Order:** Draft -> Released | Cancelled; Released -> Draft | Cancelled | InProgress | OnHold; InProgress -> OnHold | Completed; OnHold -> Released | InProgress (only back to the status held from); Completed -> InProgress (reopen) | Closed; Closed and Cancelled are final. **Operation:** Pending -> Ready -> Running <-> Paused, Running -> Ready (stop), Ready | Running -> Completed, Completed -> Ready (reopen). Only Draft is reachable in P3-1; the rest is exercised by the state-machine tests.

## 6. Use cases
**UC-1 Create an order (main).** The planner picks TP-BAN01, 100 pieces, due Friday; the system answers a Draft `MO-2611-00001` with 7 materials, 3 operations and a plan starting Wednesday. *Alternative A:* the planner names an older BOM version. *Alternative B:* the item has no routing - the order has materials but no operations and starts its lead time before the due date. *Exception:* the item is bought, inactive, serial-controlled, has no BOM effective on the date, the quantity has too many decimals or the due date is past - a business-rule 400 with its code, nothing is stored.
**UC-2 Adjust a Draft.** Quantity 100 -> 120: the lines are rebuilt; due date only: same lines, new dates. *Exception:* the order is no longer a Draft - `MFG_PO_002`.
**UC-3 Look up.** List a plant's orders by status / lateness / due range / text, open one with its lines, read its status history.

## 7. Data
Tables `ProductionOrders`, `ProductionOrderMaterials`, `ProductionOrderOperations`, `ProductionOrderOutputs`, `ProductionOrderStatusHistories`, `OutboxMessages` (SQL `src/Database/Manufacturing/006_ProductionOrders.sql`, PostgreSQL, `xmin` concurrency, soft-deleted regenerated lines). Times are UTC; an operation stores standard and calendar minutes.

## 8. Integrations
Reads: Product (item, components: active, serial flag, base unit), MasterData (decimals of the unit), the engineering data of this service. Writes: only the outbox -> Kafka `manufacturing-topic`: `ProductionOrderCreated`, `ProductionOrderStatusChanged` (catalogued in `docs/architecture/event-catalog.md`). No Warehouse call yet.

## 9. Permissions
`manufacturing:order:view` (list, get, history), `manufacturing:order:create` (create, change a Draft). The other Phase 3 codes (`release`, `cancel`, `hold`, `change-qty`, `short-close`, `reopen`, `close`, `material:*`, `operation:*`, `receipt:post`, `qc:disposition`, `stock-document:retry`) are seeded (Identity 034) for the next milestones. Admin and SuperAdmin of every tenant hold them; other roles are granted on the Roles screen.

## 10. Assumptions and open issues
- Issue method = the BOM line's value (the spec chain cannot be applied). The default BOM/routing date for a new order is plant today, not the final plan start.
- Forward scheduling puts queue, calendar minutes and move in working minutes, like the backward calculation (R162 convention).
- Order numbers can have gaps (the counter is taken before the save) and never repeat in a tenant.
- (P3-1) Orders stayed Draft until P3-2; since P3-2 the open-order guard (costing-method change, work-center deactivation) is exercised by Released orders (live check, section 22).

## 11. UAT
1. Create TP-BAN01 x 100 due in two weeks on a Friday: Draft, 7 materials with the book numbers, operations 10 / 20 / 30 starting two working days before the due date.
2. Change the quantity to 120: SON 63.00, CHAN 480, operation 10 = 1 215 minutes; change the due date by a week: dates move by a week.
3. Try quantity 0, 10.5, priority 9, a past due date, a bought item: each refused with its code; nothing appears in the list.
4. Create BTP-CHAN01 x 200 (no routing): no operations, a start before the due date.
5. Create with due date today: created, late, planned end after today.
6. List by "late" and by text; open the order and its (empty) history.
7. A user without `order:create` / `order:view` gets 403; another tenant sees none of the orders.

---

# Part 2 - Release, reservation, unrelease, cancel, hold / resume, change of quantity (P3-2, 2026-11-10)

Source: `docs/business/M/01_PRODUCTION_BA_SPEC.md` BR-PO-07..09, 11..13; tasks T3.4 and T3.9; tests TC-PO-011..028 (those that need no issue, report or receipt). Decision record: `docs/architecture/adr-manufacturing-phase3.md` ("Decisions taken while planning P3-2"). This is the first milestone in which an order calls the Warehouse.

## 12. Purpose and actors (part 2)
A Draft is a plan; **Release** commits it to the shop floor: the system checks that everything the order needs is still usable, holds the stock for it in the Warehouse and makes the first operation ready to start. The other actions let the planner take the commitment back (unrelease, cancel), pause it (hold, resume) or resize it (change of quantity) without losing consistency between the order and the Warehouse reservation.

| Actor | What they do here |
|---|---|
| Production planner / manager | Releases, unreleases, cancels, holds / resumes, changes the quantity (permissions `order:release`, `order:cancel`, `order:hold`, `order:change-qty`); reads the availability of materials (`order:view`). |
| Storekeeper | Sees the quantities the Warehouse holds for an order (Warehouse screens); issuing against them starts in P3-3. |
| Warehouse (system) | Keeps one open reservation per order (`ProductionOrder` source); answers availability. |
| Administrator | Grants the permissions; sets the plant parameters `BlockReleaseOnShortage` and `RequireSequentialOperations`. |

## 13. Process (part 2)
1. **Check availability** (`GET orders/{id}/availability`): for each material the order shows what is still needed, what the Warehouse can allocate in the material's warehouse and the shortage.
2. **Release:** only a Draft. The system checks the plant, the item, every component and every work center are active and no item is serial-controlled, reads the availability, refuses when the plant blocks release on shortage, otherwise sets Released, makes the first operation (lowest sequence) Ready and the others Pending (all Ready when the plant does not require sequential operations) and saves the order **with a flag "reservation to be brought in line"**. Immediately afterwards the reservation is made in the Warehouse: for each material the smaller of the quantity still needed and the quantity allocatable. The answer carries the order, the list of shortages and whether the reservation is still pending.
3. **If the Warehouse cannot be reached or refuses after the order was saved,** the order stays Released with the flag, the reason and the time of the next attempt; the answer says `reservationPending: true`. A background job tries again every 15 seconds (back-off 15 s ... 5 min) until it succeeds. Nothing is lost or duplicated because every Warehouse call is idempotent.
4. **Unrelease** (Released -> Draft) and **Cancel** (Draft or Released -> Cancelled) are allowed only while nothing has happened on the order; the reservation is released. Cancel needs a reason of type Cancel.
5. **Hold** (Released or InProgress -> OnHold) needs a reason of type Hold and keeps the reservation; **Resume** returns to the status held from.
6. **Change of quantity** (Released, InProgress or OnHold): the required quantities, the planned outputs, the operation minutes and the schedule are recomputed from the same BOM and routing snapshot; issued quantities stay; the reservation is brought in line with what is still needed.

## 14. Requirements and acceptance criteria (part 2)
| ID | Requirement | Acceptance (test) |
|---|---|---|
| PO-19 | The availability of a material is: still needed (required - net issued - consumed) against what the Warehouse can allocate in the material's warehouse. Allocatable stock = stock not in a lot + lots that are Available and not expired, capped by the item's available stock; stock the order itself holds counts as available to it. | TC-PO-011 on DS-01: only BTP-CHAN01 is short (need 400, have 300, short 100); expired and QcHold lots are not counted (unit test); live. |
| PO-20 | Release is allowed only for a Draft. | Second release: `MFG_PO_002` (unit, live). |
| PO-21 | Release refuses an inactive plant, item, component or work center (`MFG_PO_006`, naming the code) and a serial-controlled item or component (`MFG_MAT_011`). | TC-PO-014; inactive work center; serial component (unit). |
| PO-22 | With the plant parameter `BlockReleaseOnShortage` on, any shortage refuses the release (`MFG_PO_007`) and the order stays Draft; with it off, the order is released and the shortages are returned as a warning. | TC-PO-012 / 013 (unit, live: setting switched on and off). |
| PO-23 | Release sets the first operation Ready and the others Pending; with `RequireSequentialOperations` off all are Ready. | TC-PO-015 (unit, live). |
| PO-24 | Release reserves in the Warehouse, per material, min(still needed, allocatable); the order shows the reserved quantity per line and the reservation id. | Live: CHAN 300 (not 400), VIT 1 600, SON 52.5 held for the order; item reserved quantity +300. |
| PO-25 | A Warehouse that cannot be reached when the release is decided makes the release fail closed (`MFG_INT_001`), the order stays Draft. | Unit; live with `erp-warehouse-service` stopped. |
| PO-26 | A Warehouse that fails after the order was saved never turns the answer into an error: the state is kept, `reservationSyncPending` / error / next attempt are shown, and the job completes the reservation when the Warehouse is back. | Unit (refusal, then sync); live: cancel while the Warehouse is down, start it, the flag clears and the reservation is released without a user action. |
| PO-27 | Unrelease: Released -> Draft only while untouched; operations back to Pending; the reservation is released. | TC-PO-016 / 017 (`MFG_PO_008` after an issue); live. |
| PO-28 | Cancel: a Draft or an untouched Released order, with a reason of type Cancel (`MFG_PO_009` if missing, unknown, inactive or of another type); an InProgress order cannot be cancelled; Cancelled is final; the reservation is released. | TC-PO-026 / 027 / 028 (unit, live). |
| PO-29 | Hold needs a reason of type Hold; the reservation is kept; Resume goes back to the status held from. | TC-PO-019 / 022 (unit, live). |
| PO-30 | Change of quantity recomputes required quantities (SON 52.50 -> 63.00, CHAN 400 -> 480), planned outputs, operation minutes (op10 1 215) and schedule from the stored BOM; issued quantities stay; the quantity cannot go below the completed quantity (`MFG_PO_010`); a Draft is changed with the Draft edit; a history row records "Quantity 100 -> 120". | TC-PO-023 / 024 (unit); live: reservation SON 63, VIT 1 920, CHAN still 300. |
| PO-31 | The reservation always converges: the same routine (adopt a matching reservation, else release and reserve again) serves release, unrelease, cancel and change of quantity, and running it twice changes nothing. | Unit (second sync: no new reserve or release); live (outage test). |
| PO-32 | Release and quantity change raise `ProductionOrderReleased` / `ProductionOrderQuantityChanged` besides the status-change event. | Outbox (live, as P3-1). |

## 15. Business rules (part 2)
| ID | Rule | Code |
|---|---|---|
| BR-PO-07 | Release checks: Draft only; plant, item, components, work centers active; no serial item; availability read from the Warehouse (unreachable = refusal); `BlockReleaseOnShortage` decides whether a shortage blocks. | `MFG_PO_002`, `MFG_PO_006`, `MFG_PO_007`, `MFG_MAT_011`, `MFG_INT_001` |
| BR-PO-08 | On release the first operation is Ready, the others Pending (all Ready if sequential execution is off); the reservation per material is min(still needed, allocatable). | - |
| BR-PO-09 | Unrelease only before any transaction (an issue, return, consumption, started work, or completed quantity; pending stock documents from P3-3). | `MFG_PO_008` |
| BR-PO-11 | Hold needs a reason; Resume returns to the status held from; the reservation stays. | `MFG_PO_009`, `MFG_PO_002` |
| BR-PO-12 | Change of quantity: Released / InProgress / OnHold, positive, within the unit's decimals, not below the completed quantity, same BOM; schedule and reservation follow. | `MFG_PO_001`, `MFG_PO_010` |
| BR-PO-13 | Cancel only a Draft or an untouched Released order, with a Cancel reason; final. | `MFG_PO_009`, `MFG_PO_008`, `MFG_PO_002` |

## 16. State model (part 2)
Transitions used now: Draft -> Released (release); Released -> Draft (unrelease); Draft | Released -> Cancelled (cancel); Released | InProgress -> OnHold (hold); OnHold -> status before hold (resume). Operation: Pending -> Ready on release; Ready -> Pending on unrelease. The reservation has its own small state on the order: **in line** (`ReservationSyncPending = false`) or **to be brought in line** (flag, error text, attempts, next attempt time).

## 17. Use cases (part 2)
**UC-4 Release (main).** The planner opens MO-A (TP-BAN01 x 100, Draft), reads the availability (CHAN short by 100) and releases. The order is Released, operation 10 Ready, the Warehouse holds CHAN 300, VIT 1 600, SON 52.5 and the others; the answer lists CHAN as a shortage. *Alternative A:* the plant blocks release on shortage - the planner is told `MFG_PO_007` and the order stays Draft until stock arrives or the setting changes. *Alternative B:* the Warehouse is slow - the order is Released with "reservation pending"; a minute later the screen shows it in line. *Exception:* a component was deactivated (`MFG_PO_006`), an item became serial-controlled (`MFG_MAT_011`), the Warehouse is down when the check is read (`MFG_INT_001`).
**UC-5 Take back (unrelease / cancel).** Before any issue the planner unreleases (back to Draft, stock freed) or cancels with a reason. After an issue both are refused (`MFG_PO_008`); an order in progress is stopped by completing or short-closing it (later milestones), never by cancel (`MFG_PO_002`).
**UC-6 Pause (hold / resume).** The planner holds with a reason "waiting for a drawing"; stock stays reserved; resume returns the order to Released or InProgress.
**UC-7 Resize.** The customer asks for 120 instead of 100: change of quantity recomputes the lines (SON 63, CHAN 480), the minutes (op10 1 215), the dates, and the Warehouse reservation; an order that already issued 30 litres of paint keeps that and reserves only the remaining 33.

## 18. Data (part 2)
SQL `src/Database/Manufacturing/007_OrderReservationSync.sql` (idempotent, applied twice): on `ProductionOrders` the flag `ReservationSyncPending` (renamed from the unused `ReservationReleasePending`), `ReservationSyncAttempts`, `ReservationSyncError` (500), `ReservationNextAttemptAt`, and a partial index on the flagged rows for the job. `ProductionOrderMaterials.ReservedQty` is now filled; `ProductionOrders.ReservationId` holds the Warehouse reservation. The order DTO exposes `reservationSyncPending` and `reservationSyncError`.

## 19. Integrations (part 2)
Calls the Warehouse `internal/v1/stock/*` through `IWarehouseStockClient` (service token, caller `manufacturing`): availability per material (warehouse + lot rows), read of the order's reservation, reserve (`SourceLineId` = the material line id), release (idempotent). The `ReservationSyncJob` (hosted, every 15 s) finds flagged orders across tenants and processes each one inside its own tenant scope. Events: `ProductionOrderReleased`, `ProductionOrderQuantityChanged` (`manufacturing-topic`, catalogued). Production movements are still not booked in the general ledger (Phase 6).

## 20. Permissions (part 2)
| Action | Endpoint | Permission |
|---|---|---|
| Availability | `GET api/v1/manufacturing/orders/{id}/availability` | `manufacturing:order:view` |
| Release / Unrelease | `POST .../{id}/release`, `.../unrelease` | `manufacturing:order:release` |
| Cancel | `POST .../{id}/cancel` body `{reasonCodeId, note}` | `manufacturing:order:cancel` |
| Hold / Resume | `POST .../{id}/hold` (body `{reasonCodeId, note}`), `.../resume` | `manufacturing:order:hold` |
| Change quantity | `POST .../{id}/change-quantity` body `{quantity, note}` | `manufacturing:order:change-qty` |
Release, cancel, hold, resume and change-quantity answer `{order, shortages, reservationPending}`. A plant the caller may not see is 404.

## 21. Assumptions, open issues and reports (part 2)
- The reservation is brought in line **after** the order is saved (not before): a failed save can never leave a Warehouse reservation without an order; the cost is a short window in which a Released order is not yet fully reserved (`reservationSyncPending`). Issuing from the order (P3-3) must refuse or use free stock while the flag is set.
- A change of quantity is "release the old reservation and reserve again" (the Warehouse has no adjust); another order may take the stock in the gap, in which case this order reserves less and shows the shortage.
- "Allocatable" duplicates the Warehouse rule for lots (first-expiry-first picker); a Warehouse-side figure would remove the duplicate (risk register).
- Deactivating a **plant** is not blocked by open orders (the specification guards only the costing method and work centers); found by the live check (risk register).
- The job never gives up: a permanent refusal keeps the flag and the error until someone changes the cause; there is no alert yet.
- Reports: none new; the order screen (P3-6) will show the flag and the shortage list.

## 22. UAT (part 2)
1. MO-A (TP-BAN01 x 100, Draft): availability shows only BTP-CHAN01 short (400 / 300 / 100).
2. Release: Released, operation 10 Ready, 20 and 30 Pending; the Warehouse shows CHAN 300, VIT 1 600, SON 52.5 reserved for the order; item reserved figures rose; a second release is refused.
3. Hold without a reason, with a Cancel-type reason: refused. Hold with a Hold reason: OnHold, reservation kept. Resume: Released.
4. Change the quantity to 120: SON 63, CHAN 480, op10 1 215 min, reservation SON 63 / VIT 1 920 / CHAN 300; quantity 0 refused; history shows the change.
5. Costing-method change and deactivation of the work center of the released order: refused (`MFG_SET_001`, `MFG_WC_003`).
6. Unrelease: Draft, operations Pending, the reservation gone, item reserved figures back. Release again, cancel without a reason (refused) and with a reason: Cancelled, reservation gone; releasing it again is refused.
7. Switch `BlockReleaseOnShortage` on: the 100-piece order is refused (`MFG_PO_007`), a 10-piece order is released; switch it off. Switch `RequireSequentialOperations` off: every operation is Ready on release.
8. Stop the Warehouse service: releasing a new order is refused (`MFG_INT_001`), cancelling a released one succeeds with "reservation pending"; start the service: within a minute the flag clears and the reservation is released.
9. A user without the permission gets 403 on each action; after cancelling every test order the Warehouse reserved figures are back to what they were.
