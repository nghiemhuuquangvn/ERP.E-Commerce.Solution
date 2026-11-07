# Manufacturing - Routings, operation times, lead time, BOM / routing check and CSV import (M4)

> Service: ManufacturingService (MFG). Source specs: `docs/business/M/01_PRODUCTION_BA_SPEC.md` §5.9 (BR-RT-01..08), §7.2, error codes `PRD_RT_*` (here `MFG_RT_*`); tasks T2.5, T2.6, T2.7, T2.9 of `02_PRODUCTION_DEV_TASKS.md`; tests TC-RT-001..010, TC-BOM-021, TC-BOM-053 of `03_PRODUCTION_TEST_CASES.md`.
> Differences from the specs: `00_Mapping-to-ERP.md`. Previous milestone: `Bom.md`. Status: built and checked live 2026-11-07 (milestone M4). Screens (S09 routing editor) are milestone M5.

## 1. Purpose and scope
A routing says **how** an item is made: the ordered operations, the work center each one runs on, setup and run time, queue and move time. It answers three questions the rest of the module needs:
how long does an operation take for a quantity (standard minutes and calendar minutes), how long is the whole routing over the real calendars of the work centers (lead time, so a due date gives a start date),
and does the BOM agree with the routing (every operation a BOM line names exists). This milestone also adds **CSV import** of a BOM and of a routing, because engineers keep these lists in spreadsheets.
Not in this milestone: screens (M5), production orders and forward scheduling (Phase 3), capacity load (Phase 5).

## 2. Actors
| Actor | What they do | Permission |
|---|---|---|
| Production engineer | Creates routings and versions, edits drafts, copies a version, imports CSV | `manufacturing:routing:manage` |
| Plant manager / approver | Approves a routing version, makes it obsolete | `manufacturing:routing:approve` |
| Planner, cost accountant | Reads routings, asks for the lead time | `manufacturing:routing:view` |
| Order creation / scheduling (system, later) | Ask which routing applies on a date and for the times of a quantity | in-process services |
| ProductService, MasterData (system) | Item by SKU, unit by code (for the CSV files) | service token `manufacturing` |

## 3. Process
1. Pick the item and **create a routing** (version 1, Draft, valid from a date). An item may have no routing at all (BR-RT-07): then its orders have no operations.
2. **Add operations**: free entry, or **from a standard operation** (its values are copied; the link is only a reference). Each has a sequence number (10, 20 ...), a work center, setup, run, per unit or per batch, queue and move time.
3. Drag the operations on the screen; the system **renumbers** them 10, 20, 30 ...
4. Ask the **lead time** for a quantity and an end date: standard minutes and calendar minutes per operation, the total, and where each operation falls in the calendars.
5. **Approve** (approver permission). The open earlier version ends the day before; an overlapping period is refused. The system also tells which lines of the item's approved BOMs name an operation the new routing does not have (**warnings**, never a block).
6. To change an approved routing: **copy** it (next version, Draft), edit, approve. **Obsolete** a version when it must not be used any more.
7. **Import**: download the template, fill it, check it with a **dry run**, then import: a clean file becomes a Draft version.

## 4. Requirements and acceptance criteria
| ID | Requirement | Acceptance criteria |
|---|---|---|
| M4-01 | Operation number (BR-RT-01) | Greater than 0 and unique in the routing; a number used twice, 0 or negative -> `MFG_RT_001` naming the number. A removed operation does not block its number |
| M4-02 | Times (BR-RT-02) | Setup >= 0, run > 0, queue and move >= 0; per batch needs a batch size > 0; otherwise `MFG_RT_002` |
| M4-03 | Work center (BR-RT-02) | Active and of the same plant, else `MFG_RT_003` (also unknown id); checked again at approval |
| M4-04 | Subcontract (BR-RT-06) | A subcontracted operation must use a Subcontract-type work center, else `MFG_RT_004` |
| M4-05 | Standard operation (BR-OPS-01) | Fields left empty are copied from it (name, work center, setup, run, basis, batch, instructions); changing the standard operation later does not change the routing |
| M4-06 | Versions (BR-RT-03) | Same as BOM: Draft -> Approved -> Obsolete; approved versions never change (`MFG_BOM_002`); approving version N from 2026-12-01 ends the open version on 2026-11-30; overlap -> `MFG_BOM_003` and nothing changes; copy = highest number ever + 1 |
| M4-07 | Standard time (BR-RT-04) | `Std = Setup + Q x Run` (per unit) or `Setup + ceil(Q / Batch) x Run` (per batch). Painting: 30 + 100 x 6 = **630**; a batch run of 30 per 40 for 100 pieces = setup + 3 x 30 |
| M4-08 | Calendar time (BR-RT-04) | `ceil(Std / (efficiency% / 100))` of the work center: 630 at 80 % = 787.5 -> **788** |
| M4-09 | Lead time (BR-RT-05) | Backwards from the end of an end date, last operation first, each on its own work center's calendar: move, then calendar minutes, then queue are taken off working time. DS-01, 100 tables ending Friday 2026-11-13: 1015 + 788 + 200 = **2003 minutes**, start **Wednesday 2026-11-11**; operation 30 on Friday from working minute 700, operation 20 from Thursday, operation 10 from Wednesday; Sunday is skipped; operations follow each other without a gap |
| M4-10 | Different calendars | When the next operation's work center has another calendar the schedule is handed over by clock time (the latest working moment at or before it) |
| M4-11 | No routing (BR-RT-07) | `effective` answers 404 for an item without a routing; BOM approval then gives no warning |
| M4-12 | BOM / routing check (BR-RT-08) | Approving a BOM whose line names operation 40 while the routing has 10-30 **succeeds** and returns one warning naming the operation, the BOM line and the item; a line without an operation, or an item without a routing, gives none. Approving a routing warns about lines of approved BOMs it does not cover |
| M4-13 | Order helper (T2.7) | `AssignOperation`: an order's material line goes to its operation; when the routing lacks it, to the first operation with a warning (used by orders in Phase 3) |
| M4-14 | Renumber | The new order must list every operation exactly once; result 10, 20, 30 ... in that order |
| M4-15 | CSV import (T2.9) | Described in §8; all-or-nothing, every problem with its line number, dry run, template |
| M4-16 | Used work center (BR-GEN-02) | A work center on a routing operation cannot be deleted (`MFG_GEN_005`), only deactivated |

## 5. Business rules and decisions
Rules BR-RT-01..08 with codes `MFG_RT_001..004`, and `MFG_BOM_002/003` for versions. Decisions where the spec is silent:
| Decision | Why |
|---|---|
| Queue and move time are **working minutes of the work center's calendar** | The spec adds them to the lead time but does not say whether the clock runs through nights and weekends |
| The efficiency is read from the work center when the time is asked, not stored on the operation | The spec says "efficiency of the work center" |
| Lead time is scheduled **backwards only** | Forward scheduling (a late order) belongs to production orders, Phase 3 |
| Queue / move default to the work center's own values when an operation does not give them | Matches how the work center master is described (BA §5.5) |
| A routing for a Buy item is not refused | The spec only restricts BOMs |
| The unique key on the operation number covers live rows only | A removed operation keeps its row; it must not hold its number |
| Renumbering is done in two saves (rows first leave their numbers) | The number is unique per routing, rows would clash while they swap |

## 6. Use cases
**UC-1 Build a routing (main).** Engineer creates the routing for the table, adds Assembly (WC-ASM, 15 + 10/pc), Painting from the standard operation (30 + 6/pc, two coats), Packing (0 + 2/pc), approver approves. Result: version 1 Approved.
- *Alternative:* the engineer drags Packing to the top; the screen sends the new order; numbers become 10, 20, 30.
- *Exception:* operation 10 twice -> `MFG_RT_001`; run time 0 -> `MFG_RT_002`; an inactive work center -> `MFG_RT_003`; "subcontracted" on an assembly bench -> `MFG_RT_004`.
**UC-2 Lead time.** Planner asks 100 tables ending 2026-11-13: 2003 minutes, start 2026-11-11, with each operation's start and end. *Exception:* quantity 0 -> refused.
**UC-3 New version.** Copy version 1 from 2026-12-01, change a time, approve; version 1 ends 2026-11-30. *Exception:* a start date inside a closed period -> `MFG_BOM_003`, the copy stays a Draft.
**UC-4 BOM check.** Engineer approves a BOM with a line at operation 40; the approval works and shows "Operation 40 ... is not in routing version 1".
**UC-5 Import.** Engineer exports the sheet as CSV UTF-8, runs a dry run, sees "line 5: no item with SKU ...", fixes it, imports: Draft version created.

## 7. Data
`Routings` (item, plant, version unique per item and plant, status 1 Draft / 2 Approved / 3 Obsolete, effective from / to, approved by / at, item code and name snapshot) and `RoutingOperations` (routing, operation number unique among live rows, standard operation reference, name, work center with code snapshot, setup, run, basis 1 PerUnit / 2 PerBatch, batch size, queue, move, subcontract flag, quality plan reference (empty until Phase 4), instructions).
SQL `src/Database/Manufacturing/005_Routings.sql` (idempotent, run twice live). Tenant isolation and `xmin` as everywhere.

## 8. CSV import (T2.9)
Both imports take the file as the request body (`Content-Type: text/csv`), UTF-8 (BOM accepted), comma, semicolon or tab, headers without case / spaces / underscores, at most **2000 rows**. Query: `plantId`, `itemId`, `effectiveFrom` (default today), `dryRun`; the BOM import also `baseQuantity` and `yieldPct`.
The answer is **200** `{dryRun, rowCount, createdId, version, errors:[{line, key, message}]}`; a wrong header, an empty file or too many rows is a normal 400. **Nothing is written unless the file has no error and `dryRun` is false**; then one Draft with the next version is created.
Line numbers are the lines of the file (the header is line 1).
| BOM columns | Notes |
|---|---|
| `componentSku`*, `quantity`*, `uom`* | SKU of the component (must match exactly one product), unit by code (PCS, KG ...) |
| `scrapPct`, `operationSeq`, `issueMethod` (Manual / Backflush), `lineType` (Component / ByProduct / CoProduct) | empty = 0, none, Manual, Component |
| `isPhantom`, `isFixedQuantity` | true / false / yes / no / 1 / 0 |
| `alternativeGroup`, `alternativePriority`, `costAllocationPct`, `notes` | |
Every row goes through the same checks as typing it in: item exists and is active, unit converts, quantities, duplicates (`MFG_BOM_010`), alternative rules ...
| Routing columns | Notes |
|---|---|
| `operationSeq`*, `workCenter`* (code), `runMinutes`* | `runMinutes` may be left empty when a standard operation fills it |
| `name`, `standardOperation` (code), `setupMinutes`, `basis` (PerUnit / PerBatch), `batchSize`, `queueMinutes`, `moveMinutes`, `isSubcontract`, `instructions` | empty name = the standard operation's name or "Operation n" |
A decimal comma is accepted when the number has no dot ("0,5"). Templates: `GET boms/import/template`, `GET routings/import/template`. The import is **not streamed** (a document is small), so it needs no special Gateway or nginx route.

## 9. Integrations
| Call | Purpose |
|---|---|
| ProductService `internal/v1/product-lookup` (M1) | the item exists and is active |
| ProductService `internal/v1/product-lookup/by-sku` (new) | components of a CSV file by SKU; a SKU shared by two products is reported as ambiguous (R149) |
| MasterData `internal/v1/units-of-measure/by-code` (new) | units of a CSV file by code |
Fail closed (`MFG_INT_001`), batched 200 per call, cached 60 s per tenant. Events: none yet.

## 10. Permissions
`manufacturing:routing:view`, `manufacturing:routing:manage`, `manufacturing:routing:approve` (existing since M0). Importing needs `bom:manage` / `routing:manage`; the templates need the view permission.

## 11. API (`/mfg/api/v1/manufacturing/routings`)
`GET ?plantId&itemId&status`, `GET {id}`, `GET effective?plantId&itemId&date`, `GET {id}/lead-time?quantity&endDate`, `POST`, `PUT {id}`, `DELETE {id}` (draft), `POST {id}/operations`, `POST {id}/operations/from-standard/{standardOperationId}`, `PUT|DELETE {id}/operations/{seq}`,
`POST {id}/renumber`, `POST {id}/approve|obsolete|copy`, `POST import`, `GET import/template`; and `POST boms/import`, `GET boms/import/template`. Refusals carry `errorCode`.

## 12. Assumptions and open issues
Open issues: `docs/DEFERRED-ISSUES.md` R173-R178 (queue / move convention, efficiency read at calculation time, today's calendar for old routings, import limits and Excel, ambiguous SKU, warnings only).
Assumption: the calendar of a work center is the one that governs its operations even when the order's plant calendar differs.

## 13. UAT scenarios
1. Create the table routing: Assembly (15 + 10/pc), Painting from the standard operation (30 + 6/pc), Packing (0 + 2/pc). Lead time 100 ending 13/11: 2003 minutes, start 11/11; Painting shows 630 / 788.
2. Add operation 10 twice -> refused. Run 0 -> refused. Batch operation without a size -> refused. "Subcontracted" on WC-ASM -> refused; on the subcontract center -> accepted.
3. Drag Packing first -> Packing 10, Assembly 20, Painting 30.
4. Approve; try to add an operation -> refused. Copy from 2026-12-01 and approve -> version 1 ends 2026-11-30; effective on 12-01 is the new one. Copy from 2026-06-01 and approve -> refused naming the overlap.
5. Approve a BOM with a line at operation 40 -> approved with the warning. An item without a routing -> no warning.
6. Try to delete WC-ASM -> refused (used); deactivate instead.
7. Fill the BOM template with 10 lines, one unknown SKU: dry run lists exactly that line, a real import writes nothing; fix it, import -> Draft next version; open it, approve.
8. Routing file with an unknown work center, a duplicate number and a batch operation without size: three errors with their lines; fix and import.

## 14. Screens (M5)
Route `/manufacturing/routings` (list, **New routing**, **Import routing from CSV**) and `/manufacturing/routings/:id` (editor). Guide: `docs/frontend/manufacturing-admin-guide.md`.
- **Operations tab (S09):** the operation grid. In a Draft each row can be **dragged** to a new place (a grip icon, or the Up / Down buttons for keyboard and touch); on drop the screen sends the new order and the server renumbers 10, 20, 30 ... An approved routing cannot be dragged or edited. *Add operation* is free entry or **from a standard operation** (values copied), with work center, setup, run, per unit or per batch, queue, move and the subcontract flag; a refused value (`MFG_RT_*`) shows in the dialog.
- **Lead time tab:** quantity and the date the order must finish -> total calendar minutes, start date and time, and per operation the standard minutes, calendar minutes, start and end (DS-01: 100 tables ending 2026-11-13 -> 2003 minutes, start 2026-11-11, painting 630 standard / 788 calendar).
- **Versions tab:** the versions of the item and the **comparison** of this version with another by operation number (added, removed, changed work center / times).
- **Approve** shows the BOM lines the routing does not cover as a warning box (BR-RT-08).
- **UAT:** (1) build the table routing with three operations; drag Packing to the top: it becomes 10 and the others 20, 30; the same with the arrows; (2) Lead time for 100 ending 13/11: 2003 minutes, 11/11; (3) approve: the grid loses its drag handles; (4) copy, change a time, open *Versions*: one changed operation; (5) approve a routing that lacks an operation named by an approved BOM line: warning box.
