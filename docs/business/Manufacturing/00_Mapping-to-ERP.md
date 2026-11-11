# Manufacturing - how the specs map to this ERP

The three specs in `docs/business/M/` were written for a modular monolith. This file lists every place where the implementation differs from the text,
so the specs stay readable as they are. Decision record: `docs/architecture/adr-manufacturing-service.md`.

| Spec says | Implemented as |
|---|---|
| Module code `PRD`, schema `production`, namespace `ERP.E_Commerce.*.Production` | Service `ManufacturingService`, code **MFG**, PostgreSQL database `ManufacturingDB` (tables in `public`), namespaces `ERP.MFG.*` |
| Error codes `PRD_GEN_001` ... | `MFG_GEN_001` ... (same numbers, same Vietnamese messages; `MfgErrors`) |
| Permissions `Production.Bom.Approve` ... | `manufacturing:bom:approve` ... (`PermissionConstants.Manufacturing_*`); roles R-xxx map to tenant roles that hold these permissions |
| `bigint` identity ids | `Guid` ids + `TenantId` on every row |
| Plant code unique "toàn hệ thống" (BR-PLT-01) | unique per tenant |
| One DB transaction with Inventory (BR-MAT-06, NFR-05) | Pending document + idempotent call to Warehouse + retry/reversal (ADR decision 5) |
| `IDocumentNumberService`, `IPlantContext`, `ICurrentUser` as shared infrastructure | Own `DocumentNumberService` (atomic upsert), `PlantUserAccess` (Phase 0 M1), `UserContext` of the platform |
| Interfaces §8 (`IItemQueryService`, `IUomConversionService`, `IInventory*`, ...) | Internal REST clients in `ERP.MFG.Infrastructure` against `internal/v1/...` endpoints of Product, MasterData, Warehouse, Sales, Purchasing; events towards Accounting |
| xUnit + NSubstitute + Testcontainers | xUnit + Moq + EF InMemory (repository standard); PostgreSQL behaviour is checked in the live run |
| Test names `TC_PO_031_...` | Kept: `TC_<GROUP>_<nnn>_<Description>` |
| Error answer with `ErrorCode` (BR-GEN-07) | `errorCode` field next to `detail` in the shared problem answer for every business-rule refusal |
| Item facts (`IItemQueryService`: lot / serial controlled, make / buy) | Make / buy lives in Manufacturing (`ItemManufacturingProfile`); active + base unit come from Product `internal/v1/product-lookup`; lot / serial flags do not exist in Product yet (Phase 3 prerequisite) |
| `IUomConversionService.GetDecimalPlacesAsync` | MasterData `UnitsOfMeasure.DecimalPlaces` + `internal/v1/units-of-measure/lookup`; `ConvertAsync` = `internal/v1/units-of-measure/convert` |
| `IEmployeeQuery` (HR) | MasterData `internal/v1/employees/lookup` (employees are MasterData's, HRM only keeps a snapshot) |
| `WorkingTimeService` cached by month (T1.1.3) | cached per calendar for 60 s per tenant (a calendar is small); month data come from the same calculator |
| Reason code seed (T1.5.1) as migration seed | written per tenant on first read / `seed-defaults` (a SQL seed cannot know the tenants) |
| `IUomConversionService` (T2.1.4) | pure `UomRules` over MasterData units (`internal/v1/units-of-measure/lookup`) and the product's own conversions (`internal/v1/product-uom-conversions/lookup`); one batched read per tree |
| `IBomCycleDetector`, `ILowLevelCodeCalculator`, `IBomExplosionService` (T2.2-T2.3) | pure `BomGraph` / `BomExplosionCalculator` in the Domain plus `BomExplosionService` loading the approved versions in one read |
| Approval in one DB transaction | one `SaveChanges`: status, closing of the earlier version and the low-level codes of the plant |
| `IOperationTimeCalculator` (T2.6.1), `GetRoutingLeadTime` (T2.6.2) | pure `OperationTimeCalculator` and `RoutingLeadTimeCalculator` in the Domain, loaded with work centers and calendars by `OperationTimeService`; lead time is scheduled backwards from the end of a day |
| BR-RT-08 shared function (T2.7.1) | pure `BomRoutingCheck.Warnings` (BOM and routing approval) and `AssignOperation` (orders, Phase 3) |
| Excel import (T2.9.1) | **CSV** (UTF-8) instead of Excel, all-or-nothing, reusing `CsvImportReader` of `ERP.Shared.Streaming` for parsing; not streamed (a document is small) |
| One versioning rule for BOM and routing | generic `DocumentVersioning` over `IVersionedDocument` |
| Screens S01-S09 (T1.7, T2.8) | routes `/manufacturing/...`: S01 plants, S02 settings, S03 item profiles, S04 calendars, S05 work centers, S06 catalogs, S08 BOM (tree), S09 routing (drag and drop); S07 (reason codes / crews) is a tab of S06; drag-and-drop is native HTML5 with arrow buttons |
| Seeder `ProductionTestDataSeeder` (T3.14.1) | `SeedDs01Handler` (Phase 0-2 part only) behind `POST seed/ds01`, switch `Manufacturing:EnableDemoSeed` (off by default); products by `scripts/seed-ds01-products.js`; see `DemoData-DS01.md` |
| Concurrency 409 (BR-GEN-03, `PRD_GEN_001`) | `MfgUnitOfWork` turns `DbUpdateConcurrencyException` (xmin) and a unique-key race into `ConcurrencyException` = HTTP 409 with the MFG_GEN_001 text |

## Phase 3 - production orders (P3-1, 2026-11-10)
| Spec says | Implemented as |
|---|---|
| Permissions `Production.Order.View` / `Create` / `Release` / `Cancel` / `Hold` / `ChangeQty` / `ShortClose` / `Reopen` / `Close` | `manufacturing:order:view` / `create` / `release` / `cancel` / `hold` / `change-qty` / `short-close` / `reopen` / `close` |
| `Production.Material.Issue` / `Return` / `OverIssue`, `Production.Operation.Execute` / `CompleteManual`, `Production.Receipt.Post`, `Production.Qc.Disposition` | `manufacturing:material:issue` / `return` / `over-issue`, `manufacturing:operation:execute` / `complete-manual`, `manufacturing:receipt:post`, `manufacturing:qc:disposition` (interim use: release of received goods from quality hold); new `manufacturing:stock-document:retry` (Identity 034) |
| Document number sequence per type + plant + month (BR-GEN-04) | For orders per **tenant** + month (plant = empty): the number is the idempotency key in the Warehouse (`<DocNo>#<line>`), so it must be unique in the tenant |
| Material issue method: BOM line -> item profile -> plant default (T3.2.2) | The BOM line's value (a line always carries Manual or Backflush, there is no "inherit") |
| `PlannedStart` = start of the first operation; BOM / routing effective at `PlannedStart` (BR-PO-04) | Reference date = plant today for a new order, the old plan start (if still ahead) when a Draft is regenerated; a version can always be chosen by id |
| Serial-controlled items (BR-MAT-04, BR-FG-03) | Refused with new code `MFG_MAT_011` until the Warehouse supports serials in production (R206) |
| `IOpenOrderGuard` stub (M1/M2) | `OrderBackedOpenOrderGuard`: Released, InProgress, OnHold, Completed count as open |
| Domain events "handled in the same transaction" | Outbox in the same transaction, Kafka topic `manufacturing-topic` |
| Reservation made in the same transaction as the release (NFR-05) | Order saved with a sync flag, then the Warehouse reservation is brought in line (reconciler + `ReservationSyncJob`); the answer says `reservationPending` when it is not yet done (P3-2) |
| Reserved quantity per material = min(required, available) (BR-PO-08) | min(still needed, allocatable) where allocatable counts lot-less stock and Available, unexpired lots of the material's warehouse (capped by the item's available quantity) |
| Change of quantity keeps the reservation | Release + reserve again (the Warehouse has no "adjust") |
| Issue / return atomic with the inventory transaction in one DB transaction (BR-MAT-06, NFR-05, T3.5.3) | Pending-document engine: stored first, one Warehouse movement per line under `<DocNo>#<n>`, effect on the order only when all lines are Posted, reversal on refusal, retry job; 201 Posted / 202 Pending (P3-3) |
| Document numbers MI / MR per plant and month (BR-GEN-04) | Per **tenant** and month: the number is the Warehouse key |
| `ProductionOrderMaterial.RegisterIssue` checks BR-MAT-02 (T3.5.1) | The check is in the handler (`MaterialLimits`) before the document is stored; pending quantities count; the order only registers posted quantities |
| Serial numbers on issue lines (MaterialTransactionSerial, TC-MAT-007 serial variant) | Serial-controlled items are refused (`MFG_MAT_011`); no serial table |
| Return lot limit "<= quantity issued from that lot" (BR-MAT-05) | As specified when the lot of the issue is known; issues drawn by the reservation from several lots have no recorded lot, so they widen every lot's limit |
| Posting date chosen by the user, open cost period (BR-GEN-08) | Always plant today; no period lock until Phase 6 |
| New codes | `MFG_MAT_012` (reservation being updated), `MFG_GEN_007` (Idempotency-Key reused for another request) |
| Backflush runs synchronously in the same transaction as the operation report / receipt (T4.1.3) | Backflush is a stock document (type Backflush, prefix `BF`); the report is stored Pending and applied when the document is Posted; a shortage is checked before anything is stored (`MFG_MAT_008`, TC-BF-005); a Warehouse refusal fails document and report; an outage leaves both Pending (202) for the retry job (P3-4) |
| Backflush on the output "Good + Scrap" of the report (BR-MAT-08) | Same; **rework is not output**. Quantity on the cumulative output rounded up to the unit decimals minus consumption recorded **including backflush still pending** |
| Negative backflush line (TC-BF-006, `IsNegativeStock`) | A flagged line sent with `AllowNegative`; refused for lot-controlled items (no lot to name) |
| Operation report: first-operation input limit "Quantity x (1 + OverTol%)" and "no report on a Completed operation" (`PRD_OP_009`) | Input limit is checked first, so a report that also passes the input is `MFG_OP_004` (TC-OP-013) and one within it on a completed operation `MFG_OP_009` (TC-OP-016) |
| IPQC "only the Passed good quantity of the previous operation" (BR-OP-04, BR-QC-05) | Deferred to Phase 4 (no inspection plans yet); the previous operation's good output is used |
| Start "Ready/Paused and the order not OnHold" (BR-OP-02) | Ready, Paused **or Running** (several workers share an operation); an order on hold gives `MFG_OP_001`, a report on a held order `MFG_PO_011` |
| Operation status "Running" while someone works | The status follows the open time logs (Running / Paused / back to Ready when the last one stops); a report needs no time log and does not change the status |
| Time log employee is the signed-in user | The caller names the employee (active in MasterData); Manufacturing has no user-to-employee link |
| Document numbers `OR` per plant and month | As specified (unique index per plant); `BF` per tenant and month like MI / MR (Warehouse key) |
| Receipt + backflush atomic with the inventory transaction (T3.8.2, BR-FG-05) | One stock document (type Receipt, `FR`) with receipt lines and the backflush material lines, posted by the pending-document engine; a refusal reverses the booked lines (scrap of the created lot) and fails the receipt (P3-5) |
| `ILotService.CreateAsync` for the finished lot (T3.8.2) | The Warehouse creates the lot from the lot number in the `ProductionReceipt` movement; a duplicate number is not refused there, so Manufacturing checks the Warehouse lot list (new internal read) and its own pending lines |
| Receipt into a warehouse the Warehouse knows | The first receipt of an item the Warehouse never held creates its inventory record (product name and SKU travel with the movement) |
| Lot number format `{ItemCode}-{yyMMdd}-{seq2}` with "seq in the day" | Smallest free sequence of the item and day, over the Warehouse lots and receipts on their way; `{yyyyMMdd}` and `{seqN}` also accepted |
| FQC document created at receipt (T3.8.4, BR-FG-04) | Phase 4. Until then the lot is on quality hold in the quarantine warehouse and the interim release (`qc:disposition`) moves it to the finished-goods warehouse and makes it Available |
| Document numbers FR per plant and month | Per **tenant** and month: the number is the Warehouse key |
| Unit cost of a receipt from the order cost (BR-CST) | Not sent; the Warehouse values it at the item cost until Phase 6 |
| Return only on Released / InProgress orders (BR-MAT-01) | A return is also accepted on a Completed order so the issued material can be settled before the close (E2E-001 step 11); an issue is not |
| Complete (b) permission not named in the spec | `operation:complete-manual` (supervisor, manager) or `order:short-close`; a short-close needs `order:short-close` and a ShortClose reason |
| Close condition (4) "NetIssued not consumed or returned, or confirmed" | Tracked with `SettledIssueQty` per material line, separate from backflush consumption |
| Conditions (1) open material requests and (5) costing | Always met until material requests (Phase 4) and costing (Phase 6) exist |
| New code | `MFG_FG_005` (finished goods only on a Released / InProgress order) |
| S15 order list, filters, late flag, bulk Release (T3.12.1) | `/manufacturing/orders`: server paging and filters, late flag as text + icon, Release of up to 20 selected Drafts as a loop of single releases with a result per order (the API has no bulk call) (P3-6) |
| S16 order detail, availability panel, buttons by status and permission (T3.12.2, TC-UI-001) | `/manufacturing/orders/:id` with tabs Overview, Materials (availability panel), Operations, Finished goods, Stock documents, History; the Quality and Cost tabs wait for Phases 4 / 6; buttons from the pure `orderActions`; OnHold shows Resume only (P3-6) |
| S18 issue / return with FEFO suggestion and lot scan (T3.12.3) | Dialog opened from the order: lot typed or scanned (Enter picks the lot, never submits), *Suggest lots* from `suggest-lots`; no camera / barcode-library scanning (P3-6) |
| S19 finished-goods receipt with lot generation and label printing (T3.12.4) | Dialog with quantity and optional lot per product; the lot is generated by the server; **label printing dropped** (owner decision 3, 2026-11-10) (P3-6) |
| S17 material request, S20 shop-floor terminal, S14 Gantt, tablet / mobile variants | Not built (later phases); the Operations tab of S16 offers the same actions to a supervisor at a desk |
| New: plant-wide stock documents page | `/manufacturing/stock-documents` (Pending + Failed by default) so a stuck document is found without opening each order (R226) |
| E2E automation with Playwright / API and `ProductionTestDataSeeder` (T3.14.1) | API-level scripts in `scripts/manufacturing-live/` (`mfg-p37-e2e.js` for TC-E2E-001 / 005, `run-all.js` for the whole regression) on the DS-01 data of `seed-ds01-products.js --stock --seed`; no browser automation (R251); traceability by `tc-trace.js` (P3-7) |
| TC-E2E-001 step 3 "create a child order" | A second manual order BTP-CHAN01 x 100 whose output warehouse is the raw-material warehouse; the automatic child order is T4.12 (P3-7) |
| TC-E2E-001 step 9 "FQC Pass" | The interim quality release (`qc:disposition`); FQC documents are Phase 4 (P3-7) |
| TC-E2E-001 step 11 "return paint after Complete" vs BR-MAT-01 | A return is accepted on a Completed order, an issue is not - **accepted by the owner (R245 closed, 2026-11-10)** |
| Manual issue of a Backflush material (BR-MAT-xx silent) | Refused `MFG_MAT_013` (new, R234, owner decision 2026-11-10) |
| Plant deactivation (BR-PLT silent about orders) | Refused `MFG_PLT_001` while any order is not Closed / Cancelled (new, R219, owner decision 2026-11-10) |
| TC-GEN-008 future-dated posting | No request carries a posting date (it is the plant's today), proven by `ManufacturingApiRulesTests`; a back-dated document waits for the period lock of Phase 6 |
| T3.1 / T3.2 / T3.3 / T3.11 | P3-1 (done); T3.4 / T3.9 P3-2 (done); T3.5 / T3.6 P3-3 (done, 2026-11-10); T3.7 + T4.1 (operation trigger) P3-4 (done, 2026-11-10); T3.8 / T3.10 + the receipt trigger of backflush P3-5 (done, 2026-11-10); T3.12 P3-6 (done, 2026-11-10); T3.14 P3-7 (done, 2026-11-10) |

## Phase 4 (execution and quality) - mapping and deviations
Plan: `docs/architecture/adr-manufacturing-phase4.md`. Subcontracting (T4.14) is out of Phase 4 (owner, 2026-11-11); the terminal (S20, T4.13) is a responsive Admin page, not an Expo screen; backflush (T4.1) and co- / by-products (T4.5) were delivered in Phase 3.

| Specification | Here |
|---|---|
| T4.7 genealogy needs the lots each issue used (BR-TR-01); T4.10 / T4.11 NCR dispositions split a lot (Rework 10 + Scrap 5 + UseAsIs 85); rework consumes the held lot (BR-RW-02) | **P4-1, Warehouse W7 (done, 2026-11-11):** the Warehouse splits a lot on a partial move (sub-lot `<lot>-S<n>`), answers and stores `lots[]` per production movement, and issues a QcHold lot on request (`allowHeldLot`, internal API only, reason required). The specification assumed these existed in Inventory; the Warehouse upgrade epic (W0-W6) had made a lot sit in one warehouse, answer one lot and refuse held lots. Details: `docs/business/Warehouse/ProductionIntegration.md` (PI-R11..PI-R14) |
| **T4.2 reversal - P4-2 (done, 2026-11-11):** BR-OP-07 / BR-FG-07 as counter-documents on the pending-document engine (`Reversals.md`); permissions `manufacturing:operation:reverse` / `manufacturing:receipt:reverse` (spec `Production.Operation.Reverse`, `Production.Receipt.Reverse`; Identity 035); codes `MFG_REV_001` (already reversed / not reversible) and `MFG_REV_002` (a reversal waits) are not in the specification, `PRD_OP_007` / `PRD_FG_004` are `MFG_OP_007` / `MFG_FG_004`. Deviations: a reversal is a document of its own type (`RV`) with a free-text reason (the spec gives none); a receipt whose lot was released by the interim quality release is not reversible (owner, until FQC); a report on a Completed order needs the order reopened first (owner); the lots of each line are now kept (`MaterialTransactionLineLots`, moved forward from P4-5). |
| **T4.8 / T4.9 inspections - P4-3 (done, 2026-11-11):** plans and inspections (`Quality.md`); permissions `manufacturing:qc:plan-view`, `plan-manage`, `plan-approve`, `view`, `inspect`, `reinspect` (spec `Production.Qc.PlanManage / Inspect / Reinspect`; Identity 036); codes `MFG_QC_001..003` as in the spec (`PRD_QC_*`), `MFG_QC_004` (line has an inspection) and `MFG_REV_003` (report inspection decided) are not in the specification. Deviations: a plan is a series (plant + code) in versions and the pointers name the series; results are kept **per sample unit** (spec table has one row per characteristic, TC-QC-007 needs five values); status `Cancelled` added; `IpqcHeldQty` (net output) beside `IpqcPassedQty`; the FQC / IPQC are created in the save that applies the receipt / report instead of by an event handler; a Fail records `NcrRequired` (the NCR itself is P4-4); owner answers: only a Failed inspection is re-inspected, a Failed lot waits for P4-4. |
| T4.10 / T4.11 NCR and rework; T4.7 genealogy; T4.3 / T4.4 material requests, substitutes, unplanned material; T4.6 / T4.12 downtime and child orders; T4.13 terminal | P4-2 .. P4-8 (planned); T4.14 not in Phase 4 |
