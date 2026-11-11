# ADR - Manufacturing Phase 4: execution and quality

Status: accepted by the owner 2026-11-11 (master plan); P4-1 built 2026-11-11. Later milestones append "as built" sections.
Context documents: `adr-manufacturing-phase3.md` (pending-document pattern), `adr-warehouse-upgrade.md` (W7), `docs/business/M/02_PRODUCTION_DEV_TASKS.md` (T4.x), `docs/business/M/03_PRODUCTION_TEST_CASES.md`.

## 1. Context
Phase 3 delivered the happy path of a production order (create, release, issue, operation reports with backflush, finished-goods receipt, completion, close) and proved it with TC-E2E-001 / 005 on both tenants. Phase 4 adds what a real plant needs next: undoing mistakes (reversal of operation reports and receipts), quality (inspection plans, IPQC / FQC, non-conformance reports, rework orders), traceability (lot genealogy), richer execution (material requests, substitutes, unplanned material, downtime, child orders) and the shop-floor terminal. Backflush and co- / by-products were already delivered in Phase 3 and are not repeated.

**Owner decisions (2026-11-11):** subcontracting (T4.14) is out of Phase 4 (it waits for Purchasing); the terminal (S20) is a responsive web page in the Admin (`/manufacturing/terminal`), not an Expo screen; an item with `RequiresFqc` but no Approved FQC plan is still received on hold with a configuration warning and no inspection document, and the interim `qc:disposition` release stays available for exactly that case.

## 2. Cross-cutting decisions (all milestones)
1. **Everything that moves stock stays on the pending-document engine.** A reversal is a document with `ReversalOfId` whose lines are the opposite Warehouse movements under their own keys `<DocNo>#<n>`; a refusal rolls the whole reversal back; the effect on the order is applied when the document is Posted. Nothing is edited in place.
2. **Quality documents are Manufacturing's own data, not events consumed from itself.** The FQC inspection is created in the same save in which the receipt is applied (inside `ApplyPostedDocument`), so it exists exactly when the receipt is Posted and a failed receipt leaves none; IPQC the same way with an operation report.
3. **FQC Pass and the interim release share one Warehouse path** (transfer + `QcRelease`, keys `Q1` / `Q2`). An FQC result decides the whole inspected lot; partial outcomes go through an NCR disposition. The interim action is refused for a lot that has an FQC inspection.
4. **Lot split in the Warehouse (W7):** a partial move creates a sub-lot `<lot>-S<n>` with `ParentLotId`; disposition lines become movements on sub-lots; genealogy and inspections keep the parent link.
5. **Per-lot movement breakdown (W7):** a production movement answers `lots[]` and the Warehouse stores it; Manufacturing stores the breakdown on its document lines (P4-5).
6. **Rework issues the held lot explicitly (W7):** `ProductionIssue` with `AllowHeldLot` plus a named QcHold lot and a reason; internal API only.
7. **Permissions** (Identity script 035, default Admin / SuperAdmin roles): material request / substitute / add-unplanned, operation reverse, receipt reverse, downtime record, QC plan-manage / inspect / reinspect / approve-deviation, trace view; `qc:disposition` stays. Mapping to the specification's `Production.*` names in `00_Mapping-to-ERP.md`.
8. **SQL** continues append-only and idempotent (`011...`); `PostgreSqlSchemaDriftTests` runs for every milestone that adds a table.
9. **Events** (all `ITenantEvent`, outbox to `manufacturing-topic`, catalogued): `OperationReportReversed`, `FinishedGoodsReceiptReversed`, `InspectionCompleted`, `NcrCreated`, `NcrClosed`.
10. **Every milestone** extends `scripts/manufacturing-live/`, keeps `run-all.js` green on both tenants and extends the Admin contract check.

## 3. Milestones
| # | Scope | Status |
|---|---|---|
| P4-1 | Warehouse W7: lot split, per-lot breakdown, held-lot issue | **built 2026-11-11** |
| P4-2 | Reversal of operation reports and receipts (closes R246) | **built 2026-11-11** |
| P4-3 | Inspection plans, IPQC / FQC inspections (replace the interim release) | **built 2026-11-11** |
| P4-4 | NCR, disposition, rework orders | planned |
| P4-5 | Genealogy and tracing | planned |
| P4-6 | Material requests, substitutes, unplanned material | planned |
| P4-7 | Downtime and child orders | planned |
| P4-8 | Shop-floor terminal | planned |
| P4-9 | E2E-002, acceptance, open list | planned |

## 4. P4-1 as built (Warehouse W7)
**Problem.** (a) A lot sits in one warehouse, so only whole lots moved (`WH_PARTIAL_LOT_MOVE`): a disposition "Rework 10 + Scrap 5 + UseAsIs 85" of a 100-piece lot could not be executed. (b) A multi-lot issue answered no lot and stored no split: genealogy had nothing to read. (c) `ProductionIssue` refused a held lot, but a rework order must consume exactly that lot.

**Decisions.**
- *Sub-lot instead of a lot in several warehouses.* A part of a lot that leaves becomes a new lot (`ParentLotId`, number `<root>-S<n>`); the parent's remaining and received quantity both fall, so the tree adds up to the original receipt. The sub-lot copies cost, expiry, manufacture date, source, status and received date. Only the unreserved part can move (`WH_LOT_RESERVED`). A whole-lot move is unchanged. Rejected: a lot spread over warehouses (would change every reader of the lot's warehouse, `LotPicker` and the balances).
- *Breakdown rows beside the movement.* `StockMovementLots` (position, lot id, number as it was, quantity, cost, warehouse, parent lot). `StockMovement.LotId` keeps "the lot named in the request" (a split transfer's movement points at the source lot; the new sub-lot is the row), so the W5 replay comparison and every caller are untouched. Rejected: a JSON column (not queryable by lot).
- *`AllowHeldLot` is a privilege.* Internal API only; needs the named lot and a reason (kept on the movement); QcHold only (Blocked and expired stay refused). The public `POST movements` binds the same command, so its controller resets the flag; `HeldLotIssueRulesTests` fixes this. Rejected: a new movement type for rework.
- *Unchanged:* reservations, first-expiry-first, Sales / POS, the event contracts.

**Contract (Manufacturing client).** `StockMovementRequest.AllowHeldLot`, `StockMovementResult.Lots` (`StockMovementLotInfo`), `StockLotInfo.ParentLotId` - appended, defaulted, nothing in Manufacturing uses them yet. The dev-only `seed/lot-cycle` plays the scenario through the real internal API.

**Found while building.** The first live run showed that the stored rows came back in a different order on a replay (no ORDER BY): rows now carry a `Position`. A cost printed `60000.00` on the first answer and `60000.000000` on a replay (same value, different scale; numbers are compared as numbers).

**Deployment order.** SQL `Warehouse/014`, Warehouse, ManufacturingService (the JSON additions are backward compatible in both directions).

**Verification.** Warehouse unit tests 134 -> 159, Manufacturing 648 -> 654, Architecture 87 -> 89; `LiveSchemaDriftTests` clean with 014; `wh-w7.js` ALL OK on glcheck and SYSTEM; regression of `wh-w3`, `wh-w5`, `wh-w6`, `mfg-p35`, `mfg-p37-e2e`.

## 5. P4-2 as built (reversal of operation reports and receipts)
**Problem.** A wrong report or receipt could only be fixed by hand in the Warehouse (R246, `MFG_FG_004` unused).

**Decisions.**
- *A reversal is a stock document of its own type.* `StockDocumentType.Reversal` (prefix `RV`, per tenant and month = the Warehouse key) with `ReversalOfId`; each line carries a `ReversalAction` (Return / Scrap / Issue) so the engine builds the opposite movement without new branches by document type; the inverse of a reversal line is the rollback. Rejected: negative quantities on the original lines (the engine and the Warehouse work with positive quantities and a direction) and editing the original in place (BR-GEN-01).
- *The report reversal is a second report row* (`ReversalOfId`), Pending while its RV document waits for the Warehouse, applied in the same save as the document is Posted; with nothing consumed it needs no Warehouse at all.
- *Validate first.* Downstream use (`MFG_OP_007`), lot whole in the Warehouse (`MFG_FG_004`), released lot, already reversed (`MFG_REV_001`), a reversal waiting (`MFG_REV_002`) are all checked before anything is stored; only a refusal by the Warehouse itself produces a Failed document.
- *The lots of a line are kept.* `MaterialTransactionLineLots` stores the Warehouse W7 `lots[]`; the reversal gives each lot its own quantity and cost back. This moved forward from P4-5 because a correct reversal of a multi-lot reserved line needs it; genealogy (P4-5) reads the same table.
- *One reversal in flight per order.* While an RV document is Pending, new reports, receipts and reversals of the order are refused (`MFG_REV_002`): their backflush quantities are computed from the consumption the reversal is about to change. The window is a Warehouse outage. (Subtracting pending reversals from the limits was considered and rejected as more complex for the same effect.)
- *Interim release = inspected.* A receipt line released by `qc:disposition` blocks the reversal until P4-3 swaps the check for the FQC result (owner answer 2026-11-11); a report on a Completed order needs the order reopened (owner answer).
- *Effects on operations* (reversed operation back to Ready, next operation back to Pending, later operations unchanged) are pure methods of the aggregate (`ApplyOperationReportReversal`, `ReopenAfterReceiptReversal`).

**Found while building.** A reversal line for a receipt must not copy `IsQcHold`: a Posted Receipt-kind line on hold counts as a lot waiting for inspection in the close checklist, so the reversal itself would have blocked the close (caught by a unit test). The lot-less return of a lot-controlled item is refused by the Warehouse on purpose (`RequiresLot`) rather than silently losing the lot.

**Deployment order.** `pg-migrate` (SQL 011), `db-migrate` (Identity 035), ManufacturingService, Admin. No Warehouse change.

**Verification.** Manufacturing unit tests 654 -> 690, Architecture 89 (PostgreSQL drift clean: 11 scripts applied twice, 32 tables / 618 columns), Admin 424 -> 438; `mfg-p42.js` ALL OK on glcheck and SYSTEM (TC-REV-001..007 live, exactly-once after a Warehouse outage, 403 matrix, reconciliation clean); contract check extended.

## 6. P4-3 as built (inspection plans and IPQC / FQC inspections)
**Problem.** The interim release left no record of a quality decision (R244) and an operation never looked at quality.

**Decisions.**
- *A plan is a series, versioned like a BOM.* (plant + `Code`) with `Type` and item fixed on the series; `DocumentVersioning` runs over the versions of one code only, so two plans of one item (IPQC at two operations) never clash. The pointers on a profile / routing operation / order operation name the series by the id of any of its versions and `IInspectionPlanResolver` returns the Approved version in force on the day; no resolution = no inspection (owner decision 3 of Phase 4).
- *Inspections are created inside the save that applies their source* (`StockPostingService.CompleteAsync` for a Posted receipt, the report handler / `CompleteAsync` for a Good report): an inspection exists exactly when its source counts; a partial unique index per receipt line / report makes creation idempotent when the retry job applies a document again.
- *Results per sample unit* (`InspectionResults.SampleNo`): TC-QC-007 lists five values for one characteristic; `QualityRules` (pure) computes the sample size and the verdict.
- *FQC Pass releases before it saves.* Evaluate (pure) -> the two idempotent Warehouse calls (the same keys as the interim release, extracted into `IQuarantineRelease`) -> save status, release time and the line's `QcReleasedAt` in one save. A Warehouse outage leaves the inspection Pending with its draft; submitting again finishes it. No pending-document state is needed.
- *IPQC holds quantity, not status.* `IpqcHeldQty` per operation; **net output** = Good - held replaces Good as the input of the next operation in every place (limit, readiness, completion, reversal checks). Reports without an inspection are never held, so a plan approved late cannot freeze old output. While output of an operation is still held the next operation is not auto-completed.
- *Reversal rules*: a decided inspection blocks the reversal of its receipt line / report (`MFG_FG_004` / `MFG_REV_003`), a Pending one is cancelled with it.
- *Re-inspection only of a Failed inspection* (owner answer): a Passed lot has left quarantine and may be sold. A Failed lot waits for P4-4.

**Found while building.** The inspection number was first unique per tenant while its counter is per plant (like the operation reports): the second plant's first inspection hit the unique index inside the receipt's save, which the engine reports as a concurrent change and leaves the receipt Pending. Caught by the live run on a second plant; the index is now per plant. (Lesson: a counter per plant needs an index per plant.)

**Deployment order.** `pg-migrate` (SQL 012, 013), `db-migrate` (Identity 036), ManufacturingService, Admin. No Warehouse change.

**Verification.** Manufacturing unit tests 690 -> 760, Architecture 91 (PostgreSQL drift clean: 12 / 13 applied twice), Admin 438 -> 459; `mfg-p43.js` ALL OK on glcheck and SYSTEM; contract check extended.
