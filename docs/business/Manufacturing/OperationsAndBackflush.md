# Manufacturing - operations on the shop floor and backflush (Phase 3, P3-4)

Added 2026-11-10. Source: `docs/business/M/01_PRODUCTION_BA_SPEC.md` §4.3 (operation states), §5.12 (BR-OP-01..06, BR-OP-09), BR-MCH-02, §5.11 BR-MAT-08, BR-PO-10/11/19, §7.3 (OperationReport, LaborTimeLog), §8.1 (events); tasks T3.7 and T4.1 (operation trigger only); tests TC-OP-001..017, TC-BF-001..006 and 008. Decision record: `docs/architecture/adr-manufacturing-phase3.md` ("Decisions taken while planning P3-4"). Follows `MaterialIssueReturn.md` (the stock-document engine this milestone reuses). Finished-goods receipts and the receipt trigger of backflush (TC-BF-007) are in `FinishedGoodsAndClosing.md` (P3-5). Not part of this milestone, reversal of a report (T4.2, TC-REV), downtime (T4.6, TC-DT), the IPQC input rule (Phase 4), the Admin screens (P3-6).

## 1. Purpose and actors
An order that is Released is worked on **operation by operation**. People start and stop work on an operation (time logs); output is reported as good, scrap and rework pieces; reporting output takes the **backflush materials** out of the Warehouse by the standard quantity (screws, kit parts) so nobody has to issue them by hand; the next operation becomes ready as soon as there is good output to work on, and an operation completes when it has taken all of its input.

| Actor | What they do here |
|---|---|
| Operator / team leader | Starts, pauses, resumes and stops a work session on an operation; reports good, scrap and rework (`operation:execute`). |
| Production supervisor | Completes an operation by hand when the count is settled, reopens one that was completed too early (`operation:complete-manual`). |
| Production planner | Reads operations, time logs and reports (`order:view`). |
| Warehouse (system) | Books the backflush movements (one per line and key) and consumes the order's reservation. |
| Retry job (system) | Finishes a backflush document that did not reach the Warehouse (the P3-3 job). |

## 2. Process
1. **Start.** An employee starts work on a Ready (or Paused, or already Running) operation: a time log opens; the operation is Running; the first start of the order moves it from Released to InProgress.
2. **Pause / resume / stop.** One log at a time. The operation follows its logs: Running while one runs, Paused when all open logs are paused, Ready again when the last one stops. Duration = elapsed time - paused time.
3. **Report.** Good / scrap / rework quantities (scrap and rework with a reason). The service checks the order, the operation, the quantities, the input the operation may take and - if the operation has backflush materials - the **stock for them**. A shortage refuses the whole report before anything is stored.
4. **Store and send.** The report `OR-yyMM-nnnnn` is stored; if backflush lines exist, a backflush document `BF-yymm-nnnnn` is stored with it (one line per material and lot) and sent to the Warehouse like any stock document. A report with nothing to backflush needs no Warehouse and counts at once.
5. **Apply.** When every backflush line is in the Warehouse, **one save** applies the document (consumed quantities) and the report (operation figures, next operation ready, completion, scrap of the order) and writes the events. The answer is **201**. While the Warehouse is away after the check, the answer is **202**: report and document wait, the retry job posts them, and the order shows the output only then.
6. **Complete.** An operation completes by itself when the one before it is done (or it is first) and it has taken all of its input; completing it closes its open time logs and may complete the next one. A supervisor can complete one by hand and reopen it.

## 3. Requirements and acceptance criteria
| ID | Requirement | Acceptance (test) |
|---|---|---|
| OPR-1 | An employee starts work on an operation that is Ready, Paused or Running of a Released or InProgress order. A Pending operation, or an order that is not open (Draft, OnHold, Completed), is refused `MFG_OP_001`; a completed operation `MFG_OP_009`. Several employees may work one operation. | TC-OP-001, 002, 003 (unit, live). |
| OPR-2 | The first start (or first report) moves a Released order to InProgress and records its start time (BR-PO-10); the operation records its first work time. | TC-OP-001. |
| OPR-3 | One employee cannot have two sessions that share time; an open session runs to the end of time (`MFG_OP_006`). | TC-OP-004 (unit, live). |
| OPR-4 | A session can be paused, resumed and stopped; stopping a paused session ends the pause with it. Duration = End - Start - paused minutes (never negative). The operation status follows its logs (Running / Paused / Ready). | TC-OP-005: 08:00-10:00 with 15 paused minutes = 105 / 15. |
| OPR-5 | A session can be entered afterwards: End after Start and Start not in the future (`MFG_OP_005`), no overlap with the employee's other sessions (`MFG_OP_006`). | TC-OP-006. |
| OPR-6 | The employee must exist and be active in MasterData; a chosen machine must belong to the operation's work center and must not be Retired (`MFG_OP_010`); a machine under maintenance is allowed. The labor rate of the day (crew membership, else the work center's) is stored on the log. | Unit; live. |
| OPR-7 | A report has non-negative parts and a positive total (`MFG_OP_002`); scrap needs an active reason of type Scrap, rework an active reason of type Rework (`MFG_OP_003`); quantities fit the decimals of the order's unit. | TC-OP-008..011. |
| OPR-8 | **Input limit.** The first operation may take `Quantity x (1 + over-production tolerance)` in total; a later operation the cumulative **good** output of the one before it. Reports waiting for the Warehouse count. A report beyond the limit is refused `MFG_OP_004` naming what is available. | TC-OP-012, TC-OP-013 (105 accepted, one more refused). |
| OPR-9 | A report on a Pending operation is refused `MFG_OP_001`, on an order on hold `MFG_PO_011`. A completed operation takes no more reports (`MFG_OP_009`), unless the amount also passes its input (then `MFG_OP_004`). | TC-OP-016. |
| OPR-10 | A report adds to the operation's good / scrap / rework and to the order's scrapped quantity (BR-PO-19); the next operation becomes Ready when this one has good output (BR-OP-01). | TC-OP-007. |
| OPR-11 | An operation completes automatically when the previous one is Completed (or it is the first) and good + scrap + rework reach the planned input (the order quantity for the first, the good output before for the others); completing walks down the route, closes the open time logs of the operation and stamps its end time. | TC-OP-014, 015; unit: cascade. |
| OPR-12 | A supervisor completes a Ready or Running operation by hand and can reopen a completed one (back to Ready); reopening is refused (`MFG_OP_007`) once the next operation has work. | TC-OP-017. |
| OPR-13 | Holding the order pauses every running time log and Running operation; resuming the order leaves them paused; resuming a log of a held order is refused (`MFG_PO_011`). | Unit; live. |
| BF-1 | An operation's **backflush materials** (issue method Backflush, bound to the operation) are consumed when output is reported: `ROUNDUP(cumulative output x Required / OrderQty, unit decimals) - already consumed`, never below zero. Output = good + scrap (rework is not output). Consumption already on the way counts, so two reports in flight never take the same material twice. | TC-BF-001 (62 x 16 = 992), TC-BF-002 (608, total 1 600), TC-BF-004 (0.53 / 0.52 / 0.53 = 1.58). |
| BF-2 | The kit parts of a phantom assembly are consumed on the operation the BOM binds them to. | TC-BF-003: operation 30 Good 50 = 50 boxes + 100 foam pieces. |
| BF-3 | The part the order's reservation holds leaves the reservation; the rest is taken from free stock, lots first-expiry-first (never an expired or held lot), one document line per lot. | TC-BF-008: 21.00 from lot A, none from the expired lot. |
| BF-4 | **Shortage.** When stock cannot cover the backflush, the whole report is refused `MFG_MAT_008` (item and shortage named); nothing is stored and no movement is made. | TC-BF-005. |
| BF-5 | When the plant allows negative stock on backflush, a shortage of a material that is not lot-controlled is booked below zero as a flagged line; a lot-controlled material is still refused. | TC-BF-006. |
| BF-6 | The report counts only when its backflush document is Posted. A Warehouse refusal during posting reverses the lines already booked and fails the document **and the report** (kept for audit, HTTP 400); an outage leaves both Pending (HTTP 202) until the job posts them - once. Abandoning the document fails the report. | Unit; live (refusal path by unit test). |
| BF-7 | `Idempotency-Key` on the report: the same key and request answer the same report; the same key with another request is refused (`MFG_GEN_007`). | Unit; live. |
| BF-8 | Every applied report leaves `OperationReported`, every posted backflush `MaterialBackflushed`, on `manufacturing-topic` through the outbox. | Persistence test. |

## 4. Business rules
| ID | Rule | Code |
|---|---|---|
| BR-OP-01 | The next operation becomes Ready when the one before it has good output (all operations are Ready at release if the plant does not require sequence). | - |
| BR-OP-02 | Start: Ready / Paused / Running operation of an open order; several workers. | `MFG_OP_001` |
| BR-OP-03 | Report quantities and reasons (OPR-7). | `MFG_OP_002`, `MFG_OP_003` |
| BR-OP-04 | Input limit (OPR-8). Since P4-3 the previous operation hands over its **net output** (Good minus the quantity an in-process inspection still holds, `Quality.md`); reports of an operation that has a plan are not counted while they wait for the Warehouse. | `MFG_OP_004` |
| BR-OP-05 | Time log: End > Start, Start <= now, no overlap per employee. | `MFG_OP_005`, `MFG_OP_006` |
| BR-OP-06 | Completion (OPR-11); a completed operation takes no more reports. | `MFG_OP_009` |
| BR-OP-09 | The good output of the last operation is the ceiling for finished-goods receipts - enforced with the receipts in P3-5. | - |
| BR-MCH-02 | A Retired machine cannot be chosen. | `MFG_OP_010` |
| BR-PO-10 | First transaction starts the order. | - |
| BR-PO-11 | OnHold blocks work; hold pauses running work. | `MFG_PO_011`, `MFG_OP_001` (start) |
| BR-MAT-08 | Backflush quantity, lots, shortage (BF-1..BF-5). | `MFG_MAT_008` |
| New | A report waits for its backflush document; the document number prefix is `BF`. | - |

## 5. State models
**Operation** (`OperationStateMachine`): Pending -> Ready (previous output or release); Ready -> Running (start) | Completed; Running -> Paused | Ready (last log stopped) | Completed; Paused -> Running; Completed -> Ready (reopen). A stopped paused log walks Paused -> Running -> Ready.
**Time log:** Running <-> Paused -> Stopped.
**Report:** `Pending` (waiting for its backflush document) -> `Posted` (applied to the order) | `Failed` (backflush refused or abandoned; never applied).
**Backflush document:** as in `MaterialIssueReturn.md` (Pending -> Posted | Failed; Forward / Rollback); line statuses Pending / Posted / Refused / Compensated.

## 6. Use cases
**UC-1 Work and report (main).** NV001 starts operation 10 of MO-A; NV002 joins; the operator reports Good 60, Scrap 2 (reason SCRAP-01): the system consumes 62 x 16 = 992 screws (`BF-2611-00001`, one reserved line), the report `OR-2611-00001` counts, operation 20 becomes Ready, the order has 2 scrapped. A second report Good 38 completes operation 10 (98 + 2 = 100): 608 more screws, total 1 600, NV001's open session is closed. *Alternative A - nothing to backflush:* operation 20 reports 98 good (paint is issued by hand): counts at once. *Alternative B - phantom kit:* operation 30 Good 50 consumes 50 boxes and 100 foam pieces. *Alternative C - supervisor:* completes operation 10 at 90 good by hand, later reopens it.
**UC-2 Not enough material.** Screws in stock 400, operation 10 reports Good 100 (needs 1 600): refused `MFG_MAT_008`; no report, no document, no movement. If the plant allows negative stock the report posts and screws go to -1 200, the line flagged.
**UC-3 The Warehouse is away.** The check reads the Warehouse, so a report **with** backflush is refused `MFG_INT_001` while it is down and nothing is stored; a report on an operation **without** backflush, and all time logs, work as usual. If the Warehouse fails between the check and the booking, report and document stay Pending (202) and the job posts them once it is back.
**UC-4 A line is refused.** Of three backflush lines the second is refused (the stock was taken a moment ago): the first is reversed, the document and the report are Failed with `MFG_MAT_008`; operation figures are unchanged.
**UC-5 Hold.** The order is put on hold while two people work: their logs and the operation are Paused; on resume they stay paused until each person resumes.

## 7. Data
Tables `OperationReports` (number per plant and month, order, operation, quantities, reasons, machine, status, backflush document, failure code / message, idempotency key + hash, reversal columns for T4.2) and `LaborTimeLogs` (order, operation, employee number / name, machine, start, end, pause start, paused minutes, duration, labor rate, manual flag); `MaterialTransactions.SourceOperationReportId` and `MaterialTransactionLines.IsNegativeStock` - SQL `src/Database/Manufacturing/009_OperationsAndBackflush.sql` (PostgreSQL, idempotent, `xmin`). Report numbers are counted per plant and month (BR-GEN-04) and are unique per plant; `BF` numbers per tenant and month (the number is the Warehouse key, like MI / MR).

## 8. Integrations
MasterData `internal/v1/employees/lookup` (active employee, number, name); Warehouse `internal/v1/stock/*` for the backflush (availability, reservation, lot suggestion, `ProductionIssue` with the reservation or a lot, `AllowNegative` for a flagged line, `ProductionReturn` for a reversal). Product lot / serial flags. Events `OperationReported`, `MaterialBackflushed` (catalogued). Production movements are not posted to the general ledger before Phase 6.

## 9. Permissions and endpoints
| Action | Endpoint | Permission |
|---|---|---|
| Operations with time logs | `GET api/v1/manufacturing/orders/{id}/operations` | `manufacturing:order:view` |
| Start | `POST .../orders/{id}/operations/{seq}/start` `{ employeeId, machineId? }` | `manufacturing:operation:execute` |
| Manual time | `POST .../operations/{seq}/time-logs` `{ employeeId, machineId?, startAt, endAt }` | `manufacturing:operation:execute` |
| Pause / resume / stop | `POST .../operations/{seq}/time-logs/{logId}/pause\|resume\|stop` | `manufacturing:operation:execute` |
| Report | `POST .../operations/{seq}/report` `{ goodQty, scrapQty, reworkQty, scrapReasonId?, reworkReasonId?, machineId?, note? }` + header `Idempotency-Key` | `manufacturing:operation:execute` |
| Complete / reopen | `POST .../operations/{seq}/complete\|reopen` | `manufacturing:operation:complete-manual` |
| Reports of an order | `GET .../orders/{id}/operation-reports` | `manufacturing:order:view` |
Answers of a report: 201 (counts), 202 (waiting for the Warehouse), 400 (refusal with `errorCode`, a failed report included), 404, 409.

## 10. Reports
None new. The stock-document list shows Pending / Failed backflush documents; the report list of an order shows Pending / Failed reports.

## 11. Assumptions and open issues
- **Rework is not output** for backflush (it goes round the same operation again); the spec does not say.
- The backflush target moves with the order quantity: after a change of quantity the difference is taken by the next report.
- A manual issue of a Backflush material is allowed and is not netted against backflush (the formula uses consumption only).
- A lot-controlled material cannot be taken below zero (no lot to name).
- The input limit counts reports still waiting for the Warehouse, so a second report in flight cannot pass what the first leaves; two simultaneous requests are each checked against what they see (the Warehouse still protects stock).
- A report on a Ready operation leaves it Ready (a report needs no time log); only a start makes an operation Running.
- The employee is named by the caller; Manufacturing has no link from a user to an employee.
- Time zone: "Start <= now" compares instants (UTC).
- A report or document stuck Pending blocks unrelease and cancel of its order (retry / abandon as in P3-3).
- Reversal of a report is done (P4-2, see `Reversals.md`); in-process inspections are done (P4-3, see `Quality.md`): an operation with a plan holds each Good report until its IPQC passes, the next operation becomes Ready on the passed quantity, and while output of an operation is still held the next one is not completed. Downtime comes later.

## 12. UAT
1. MO-A released. Start operation 20: `MFG_OP_001`. Start operation 10 as NV001: Running, the order is InProgress. NV002 joins. NV001 again: `MFG_OP_006`.
2. NV002 pauses, resumes, stops: duration shown; a manual session with End before Start or in the future: `MFG_OP_005`; one hour in the past: accepted.
3. Report zeros: `MFG_OP_002`; scrap without a reason: `MFG_OP_003`; Good 60 Scrap 2: screws consumed 992, stock down 992, operation 20 Ready, order scrapped 2.
4. Operation 20 Good 61: `MFG_OP_004` (60 available). Operation 10 Good 38 with a key, again with the same key: same report, screws 1 600 in total; operation 10 Completed, NV001's session closed. One more piece: `MFG_OP_009`.
5. Operation 20 Good 98 (no stock movement, paint is issued by hand); operation 30 Good 50: 50 boxes and 100 foam pieces consumed.
6. Start operation 30, hold the order: session and operation Paused; report: `MFG_PO_011`; resume the order: still paused; resume the session; complete operation 30 by hand; reopen it.
7. Second order: screws in stock 400, report Good 100: `MFG_MAT_008`, nothing stored. Switch on negative stock on backflush, report again: posted, screws -1 200, flagged. Switch off.
8. Stop the Warehouse service: a report on operation 30 (backflush): `MFG_INT_001`, nothing stored; a report on operation 20: accepted; time logs work. Start the service.
9. A user without the permissions gets 403 on every endpoint above.

**Screen (P3-6):** the Operations tab of S16 (start, pause, resume, stop, add time, report output, complete / reopen by hand, operation reports) is described in `ProductionOrderScreens.md`.
