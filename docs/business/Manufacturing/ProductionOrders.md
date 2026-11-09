# Manufacturing - production orders: creation, snapshot, schedule (Phase 3, P3-1)

Added 2026-11-10. Source: `docs/business/M/01_PRODUCTION_BA_SPEC.md` §4.2-4.3, §5.10 (BR-PO-01..06, BR-PO-19 data), §7.3; tasks T3.1, T3.2, T3.3, T3.11; tests TC-PO-001..010, 039..042, TC-SCH-001..005. Decision record: `docs/architecture/adr-manufacturing-phase3.md`. Release, reservation, material, operations, receipts and closing follow in P3-2..P3-5 and are documented in this file as they arrive.

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
- Orders stay Draft until P3-2; the open-order guard (costing-method change, work-center deactivation) is real but can only be exercised with released orders then.

## 11. UAT
1. Create TP-BAN01 x 100 due in two weeks on a Friday: Draft, 7 materials with the book numbers, operations 10 / 20 / 30 starting two working days before the due date.
2. Change the quantity to 120: SON 63.00, CHAN 480, operation 10 = 1 215 minutes; change the due date by a week: dates move by a week.
3. Try quantity 0, 10.5, priority 9, a past due date, a bought item: each refused with its code; nothing appears in the list.
4. Create BTP-CHAN01 x 200 (no routing): no operations, a start before the due date.
5. Create with due date today: created, late, planned end after today.
6. List by "late" and by text; open the order and its (empty) history.
7. A user without `order:create` / `order:view` gets 403; another tenant sees none of the orders.
