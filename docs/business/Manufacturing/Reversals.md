# Manufacturing - reversal of operation reports and finished-goods receipts (Phase 4, P4-2)

Added 2026-11-11. Source: `docs/business/M/01_PRODUCTION_BA_SPEC.md` BR-OP-07, BR-FG-07, BR-PO-19; `02_PRODUCTION_DEV_TASKS.md` T4.2; `03_PRODUCTION_TEST_CASES.md` TC-REV-001..007. Plan: `docs/architecture/adr-manufacturing-phase4.md`. Closes R246.

## 1. Purpose and actors
A report or a receipt that was entered wrongly (wrong quantity, wrong item, double entry) is undone by a **counter-document**, never edited in place and never deleted (BR-GEN-01). The reversal takes the figures off the operation and the order, gives back the material that the original backflush consumed, and takes received finished goods out of stock again. Everything that touches stock runs on the pending-document engine of Phase 3: a Warehouse outage leaves the reversal **Pending** (HTTP 202) and the retry job finishes it exactly once; a Warehouse refusal rolls the reversal back and the original stays untouched.

| Actor | Role |
|---|---|
| Shop-floor supervisor | reverses an operation report (`manufacturing:operation:reverse`) |
| Warehouse / production planner | reverses a finished-goods receipt (`manufacturing:receipt:reverse`) |
| Quality (later, P4-3) | the FQC result will replace the interim quality release as the reason a receipt can no longer be reversed |
| Warehouse service | books the opposite movements (`ProductionReturn`, `ProductionScrap`, `ProductionIssue`) under the keys of the reversal document |
| Retry job | finishes a Pending reversal when the Warehouse was unreachable |

## 2. Requirements and acceptance criteria
| ID | Requirement | Acceptance |
|---|---|---|
| REV-1 | A Posted operation report can be reversed with a counter-report (TC-REV-001). | Report Good 60 / Scrap 2 on operation 10 (backflush 992 screws): reversal answers 201; operation 10 Good 0 / Scrap 0; `ScrappedQty` 0; the screws' `ConsumedQty` 0 and the Warehouse stock back at its opening level; the next operation goes back from Ready to Pending; the original report and its backflush document read "reversed"; the reversal report lists its original. |
| REV-2 | A report whose good output was used downstream cannot be reversed (TC-REV-002, TC-REV-003, BR-OP-07). | Operation 10 Good 60, operation 20 took 50: 400 `MFG_OP_007` naming what is left and what was used, nothing stored, no Warehouse call. For the last operation the receipts (also receipts on their way) count as "used". Scrap and rework of a report can always be reversed. After the downstream use is reversed, the report can go. |
| REV-3 | A Posted receipt can be reversed while its lots are still whole in the Warehouse (TC-REV-004, BR-FG-07). | Receipt of 40 on quality hold: reversal answers 201; the lot is empty in the Warehouse; finished-goods stock back at its opening level; `CompletedQty` 0; what the receipt's backflush part consumed is back; the receipt reads "reversed". |
| REV-4 | A receipt cannot be reversed after its lot was released from quality hold (TC-REV-005) or when the lot was partly used or reserved (TC-REV-006). | 400 `MFG_FG_004` with the reason ("lot ... was released from quality hold" / "lot ... only holds 8 of 12 received"); nothing stored. |
| REV-5 | Reversing a receipt of a Completed order reopens the order when `CompletedQty` falls below `Quantity x (1 - under-completion tolerance)` (TC-REV-007). | Completed at 100, reverse 40: `CompletedQty` 60, status InProgress, `ActualEnd` empty, history row "Reopened: receipt reversed by RV-...". Reversing 1 of 100 with a 2 % tolerance leaves the order Completed. |
| REV-6 | A reversal needs a written reason and is idempotent. | Reason of at least 3 characters (`MFG_VALIDATION`), kept on the reversal; the same `Idempotency-Key` with the same request answers the same reversal (also while Pending); with another reason `MFG_GEN_007`. |
| REV-7 | A document is reversed once, and a reversal is never reversed. | Second attempt, or an attempt on a reversal, a Pending or a Failed original: 400 `MFG_REV_001`. A **Failed** reversal does not block a new try. |
| REV-8 | While a reversal of an order is waiting for the Warehouse, new reports, receipts and reversals of that order wait (`MFG_REV_002`), and Complete / Close / Cancel / Unrelease are blocked by the pending document. | Warehouse stopped: report reversal 202; a new report and a new receipt on that order answer 400 `MFG_REV_002`. |
| REV-9 | A report on a Completed order is reversed only after the order is reopened (owner decision 2026-11-11). | 400 `MFG_OP_001` on a Completed order; the receipt reversal reopens by itself. |
| REV-10 | The Admin offers the reversals on the order screen with confirm-first dialogs. | Operations tab: **Reverse report** on a Posted live report; Finished goods / Stock documents tab: **Reverse receipt** on a Posted receipt without a released lot; both ask for a reason; a 202 is "saved and waiting"; reversed rows and reversal rows are marked. |

## 3. Business rules
| ID | Rule |
|---|---|
| R-REV-1 | A reversal is a stock document of type **Reversal** (prefix `RV`, numbered per tenant and month because the number is the Warehouse key) whose `ReversalOfId` is the document it undoes. Each line is one Warehouse movement and says what it does (`ReversalAction`): **Return** (stock comes back into the lot it left, at the cost it left with), **Scrap** (the lot a receipt created leaves; allowed on a lot on quality hold), **Issue** (lot-less received stock leaves). Lines are the only place the movement is decided (`ReversalPlanner`). |
| R-REV-2 | A report reversal is a second `OperationReport` row (`ReversalOfId` = the original, quantities stored positive, number `OR` of the plant). If the original had **no backflush** it needs no Warehouse and is applied in the same save (201). Otherwise the Reversal document returns every consumed line and the reversal report is applied when that document is Posted (201 / 202). |
| R-REV-3 | Lot attribution: the Warehouse (W7) answers the lots a movement really used; since P4-2 they are kept on the document line (`MaterialTransactionLineLots`). A reversal gives each lot back its own quantity at its own cost; the part no lot covers goes back lot-less; a line posted before the lots were kept goes back to `PostedLotId` / `LotId`. A lot-controlled item with neither is refused by the Warehouse (`WH_LOT_REQUIRED`) and the reversal ends Failed. |
| R-REV-4 | Effect of a report reversal on the order: operation Good / Scrap / Rework decrease (never below 0); `ScrappedQty` decreases; backflush `ConsumedQty` decreases by the returned lines; the **reversed operation goes back from Completed to Ready** (`ActualEnd` cleared) when it no longer has all its input taken (BR-OP-06); the **next operation goes back from Ready to Pending** when this one has no good output left, nothing was done there and the plant is sequential (BR-OP-01). Operations after that keep their status. |
| R-REV-5 | Effect of a receipt reversal: `CompletedQty` (main product) or the output's `ReceivedQty` decrease; `ConsumedQty` of the backflush part decreases; the receipt is flagged reversed; a Completed order below the threshold is reopened (REV-5). A reversed receipt no longer counts as a lot on quality hold in the close checklist; its lot number stays taken. |
| R-REV-6 | Validate first: every check runs before anything is stored. A violation is a plain 400 and no document exists. The lot check of a receipt reads the Warehouse (`GetAvailabilityAsync`); with the Warehouse down the request is refused `MFG_INT_001`, nothing stored. |
| R-REV-7 | Interim quality release counts as "the inspection has a result" (owner decision 2026-11-11, `MFG_FG_004`). P4-3 replaces this check by "the FQC inspection has a result". |
| R-REV-8 | The reversal document and the reversal report fail together: a refused Warehouse movement rolls back the lines already posted (opposite movements under keys `...R`), the document ends Failed, the reversal report Failed with the code, the original is unchanged. |
| R-REV-9 | Events: `OperationReportReversed` (raised when the order applies the report reversal), `FinishedGoodsReceiptReversed` and `MaterialReturned` (raised when the Reversal document is Posted); outbox -> `manufacturing-topic`; no consumer yet (costing, Phase 6). |

## 4. Use cases
**UC-1 Reverse a wrong report (main).** The supervisor opens the order > Operations > reports, presses *Reverse report* on OR-2611-00001, writes "counted twice", confirms. The system checks the downstream use, creates the counter-report and the RV document, the Warehouse takes the 992 screws back, the operation shows 0 good. *Alternative A - Warehouse down:* the answer is 202 ("saved and waiting"), the order shows the effect when the Warehouse has booked it. *Alternative B - nothing consumed:* the reversal is applied at once, no RV document. *Exception:* the next operation used the output (`MFG_OP_007`), the report is already reversed (`MFG_REV_001`), the order is Completed (`MFG_OP_001`), on hold (`MFG_PO_011`).
**UC-2 Reverse a wrong receipt.** The planner opens the order > Finished goods, presses *Reverse receipt* on FR-2611-00001, writes the reason, confirms. The lot leaves the quarantine warehouse, the order shows 0 completed (and is reopened if it was Completed). *Exception:* the lot was released or used (`MFG_FG_004`), the Warehouse cannot be reached (`MFG_INT_001`).
**UC-3 Warehouse outage during a reversal.** The reversal is Pending; new reports / receipts on the order are refused (`MFG_REV_002`); the retry job posts the reversal when the Warehouse is back, exactly once; *Retry* / *Abandon* work on the Pending document as for any stock document.
**UC-4 Correct a report.** Reverse the wrong report, then report the right quantity (a new report).

## 5. Data and state model
SQL `Manufacturing/011_Reversals.sql`: `MaterialTransactions.IsReversed`; `MaterialTransactionLines.ReversalAction`, `ReversesLineId`; new table `MaterialTransactionLineLots` (line, position, lot id, lot number, quantity, unit cost, warehouse, parent lot); indexes on `ReversalOfId`. `OperationReports.ReversalOfId` / `IsReversed` (since SQL 009). Document type `Reversal = 5`. Report states unchanged (Pending / Posted / Failed); a reversal report is Pending only while its document waits for the Warehouse.

## 6. Integrations
Warehouse `internal/v1/stock/movements` (W5 / W7): `ProductionReturn` (lot, cost), `ProductionScrap` (lot), `ProductionIssue` (lot-less), keys `RV-yymm-nnnnn#n`, rollback keys `...R`; `GET {productId}/availability` for the lot check. The movement answers `lots[]` (W7), which the engine stores. No new Warehouse endpoint.

## 7. Permissions
`manufacturing:operation:reverse`, `manufacturing:receipt:reverse` (Identity script 035; Admin and SuperAdmin hold them, a shop gives them to other roles). Reading stays `manufacturing:order:view`. Mapping to the specification's `Production.Operation.Reverse` / `Production.Receipt.Reverse` in `00_Mapping-to-ERP.md`.

## 8. Reports
Order screen: reports list (original, reversed badge, reversal row with link), stock documents (type Reversal, reversed badge, link to the original, lots per line). No new report.

## 9. Assumptions and open issues
- Documents posted before P4-2 have no stored lot rows; a multi-lot reserved line of a **lot-controlled** item cannot be returned automatically (the reversal ends Failed with `WH_LOT_REQUIRED`) - R268.
- The reversal of a receipt scraps the lot at the item's **current** cost, not the receipt's cost: a valuation difference for the costing phase.
- Operations after a reversed operation keep their status (a Completed next operation stays Completed while the reversed one is Ready again).
- The reason is a free note, not a reason code.
- Manual issue and return documents are not reversible (a return is the undo of an issue).
- A Pending reversal blocks the order's new reports / receipts for the length of a Warehouse outage.
- The Admin buttons are verified in jsdom and by the contract script, not in a real browser (R251).

## 10. UAT
1. Release a 100-piece order; report operation 10 Good 60 Scrap 2; note the screws' Warehouse stock. Reverse the report: screws back, operation 10 at 0, operation 20 Pending, both reports marked.
2. Report operation 10 Good 60 and operation 20 Good 50; try to reverse the operation 10 report: refused with the figures. Reverse the operation 20 report, then operation 10's: both work.
3. Produce 20 on every operation, receive 8 (lot on hold): reversing the operation 30 report is refused; reverse the receipt: lot empty, completed 0; now the operation 30 report can go.
4. Receive 8, release the lot from quality hold: reversal refused. Receive 12, scrap 4 in the Warehouse: reversal refused naming 8.
5. Receive 8 + 12 (the order completes), reverse the 8: order InProgress, completed 12; receive 8 again: Completed.
6. Stop the Warehouse; reverse a report with backflush: 202; a new report on that order is refused; start the Warehouse: within a minute the reversal is Posted and the stock back once.
7. A user without the permissions gets 403 on both endpoints.

## Addendum P4-3: inspections (see `Quality.md`)
* A receipt line whose newest inspection has a **result** (Passed or Failed) cannot be reversed (`MFG_FG_004`, the message names the inspection); the earlier rule "a lot released by the interim button counts as inspected" stays for items without a plan. A **Pending** inspection is cancelled in the same save in which the reversal document is Posted.
* A report whose in-process inspection has a result cannot be reversed (`MFG_REV_003`); a Pending one is cancelled with the report and the Good quantity it held is no longer held. The downstream check of BR-OP-07 counts the **net** output of the operation (Good minus held), apart from the hold of the report being reversed.
* A lot with a Failed FQC can be neither reversed nor released by hand until P4-4 gives the non-conformance report its dispositions (owner decision, R279).
