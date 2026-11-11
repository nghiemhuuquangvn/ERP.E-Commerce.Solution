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
| P4-2 | Reversal of operation reports and receipts (closes R246) | planned |
| P4-3 | Inspection plans, IPQC / FQC inspections (replace the interim release) | planned |
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
