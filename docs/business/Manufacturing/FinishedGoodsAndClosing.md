# Manufacturing - finished-goods receipt, completion and closing of an order (Phase 3, P3-5)

Added 2026-11-10. Source: `docs/business/M/01_PRODUCTION_BA_SPEC.md` §5.10 (BR-PO-14..17, BR-PO-19), §5.13 (BR-FG-01..06), BR-OP-09, BR-MAT-08 (receipt trigger), §7.3 (ProductionReceipt), §8.1 (events); tasks T3.8 and T3.10; tests TC-FG-001..013, TC-BF-007, TC-PO-029..040, 042, 044. Decision record: `docs/architecture/adr-manufacturing-phase3.md` ("Decisions taken while planning P3-5"). Follows `OperationsAndBackflush.md` (output reported per operation) and `MaterialIssueReturn.md` (the stock-document engine this milestone reuses). Not part of this milestone: reversal of a receipt (T4.2, `MFG_FG_004` stays unused), final-inspection documents and NCR (Phase 4: here only an interim release), child orders (BR-PO-18), the period lock (Phase 6), the Admin screens (P3-6).

## 1. Purpose and actors
Good output becomes stock: the finished product (and any co- or by-product) is **received** into the warehouse with a lot, the order counts it, the materials that no operation owns are backflushed by the quantity received, and when everything is received the order **completes**. A completed order is **closed** after its issued material is settled; a closed order is read only.

| Actor | What they do here |
|---|---|
| Storekeeper | Receives finished goods (`receipt:post`), returns unused material after completion (`material:return`). |
| Quality inspector / manager | Releases a lot from quality hold (interim: `qc:disposition`; Phase 4 replaces this by the final inspection). |
| Production supervisor / manager | Completes an order by hand (`operation:complete-manual`), short-closes it (`order:short-close`), reopens it (`order:reopen`). |
| Production manager | Closes the order after the checklist (`order:close`). |
| Warehouse (system) | Creates the lot, books the receipt and the backflush, moves and releases the lot. |
| Retry job (system) | Finishes a receipt that did not reach the Warehouse (the P3-3 job). |

## 2. Process
1. **Receive.** The storekeeper chooses the quantity of the main product (and, if the BOM has them, co- / by-products). The service checks the order, the limits, the lots and - for materials no operation owns - the stock for their backflush. A violation is a plain refusal; nothing is stored.
2. **Store and send.** One document `FR-yymm-nnnnn` is stored: first the backflush consumption lines, then the receipt lines (one Warehouse movement each, sent under the key `<FR>#<n>`). The lot of a finished good is created by the Warehouse from the lot number; an item that needs final inspection goes to the plant's **quarantine warehouse** with the lot **on hold**.
3. **Apply.** When every line is in the Warehouse one save updates the order: the main product adds to `CompletedQty`, a co- / by-product to its output line, the backflush to `ConsumedQty`; the first receipt starts the order; if all of the quantity is received and the plant completes by itself, the order **completes** in the same save (open operations are completed, open time logs closed, the rest of the reservation goes back to the Warehouse). 201; 202 while the Warehouse is away.
4. **Quality.** A lot on hold is released by the interim action: it moves to the finished-goods warehouse and becomes Available.
5. **Complete by hand.** Without auto-complete, or short of the quantity, a supervisor completes the order: at or above `Quantity x (1 - under-completion tolerance)` without a reason; below it only as a **short-close** with permission and a ShortClose reason.
6. **Close.** The checklist shows what is still open; the manager closes the order, optionally carrying the issued material that nobody returned to consumption.

## 3. Requirements and acceptance criteria
| ID | Requirement | Acceptance (test) |
|---|---|---|
| FG-1 | Finished goods are received only on a Released or InProgress order (OnHold `MFG_PO_011`, Draft / Completed `MFG_FG_005`, Closed `MFG_PO_014`). The first receipt starts the order (BR-PO-10). | Unit; live. |
| FG-2 | The main product received in total (posted + on its way + this) may not pass `min(Good of the last operation, Quantity x (1 + over-production tolerance))`; an order without operations is limited by the tolerance only. Beyond it: `MFG_FG_001` naming what is left. | TC-FG-002 (50 good: 51 refused), TC-FG-003 (ceiling independent of bad data), TC-FG-004 (105 accepted, 106 not), TC-FG-009 (no routing: 210). |
| FG-3 | A co- / by-product may not pass `Planned x (1 + tolerance)`. | TC-FG-010, 011 (25 planned: 26.26 refused, ceiling 26.25). |
| FG-4 | A lot is needed when the item is lot-controlled or needs final inspection. Its number is typed (unique per item against the Warehouse's lots, used-up ones included, and against receipts on their way: `MFG_FG_002`) or generated from the plant's format (`{ItemCode}-{yyMMdd}-{seq2}`), the smallest free sequence of that day. | TC-FG-001 (`TP-BAN01-261104-01`), TC-FG-005 (-01, -02), TC-FG-006. |
| FG-5 | Manufacture date = posting date; expiry = + shelf life days when the item has them. | TC-FG-007: 2026-11-04 + 180 days = 2027-05-03. |
| FG-6 | An item that needs final inspection is received into the quarantine warehouse with the lot on quality hold; any other item goes to the order's output warehouse, Available, with no lot unless it is lot-controlled. | TC-FG-001, TC-FG-008. |
| FG-7 | Serial-controlled finished goods are refused (`MFG_MAT_011`). | TC-FG-012 variant. |
| FG-8 | Materials with issue method Backflush that no operation owns are consumed by the quantity received: `ROUNDUP(cumulative received x Required / OrderQty) - consumed`, with pending consumption counted; a shortage refuses the whole receipt (`MFG_MAT_008`, nothing stored). | TC-BF-007 (10 received = 160 screws), shortage test. |
| FG-9 | One receipt is one document: a Warehouse refusal reverses the lines already booked (consumption by a return, a receipt line by scrapping the lot it created) and fails the receipt; an outage leaves it Pending (202) for the job. | Unit; live (outage). |
| FG-10 | The main product counts in `CompletedQty` when received, also while its lot waits for quality; `ScrappedQty` stays the sum of scrap reported. | TC-FG-001, TC-PO-044. |
| FG-11 | Receiving all of the quantity completes the order when the plant allows it (`AutoCompleteOnFullReceipt`); otherwise it stays InProgress. | TC-PO-029, TC-FG-013. |
| FG-12 | `Idempotency-Key` on a receipt: same key and request = same document; same key, another request = `MFG_GEN_007`. | Unit; live. |
| CO-1 | **Complete by hand:** InProgress only; no operation Running and no stock document / report on its way (`MFG_PO_013`); at or above the under-completion threshold no reason; below it a short-close needs `order:short-close` and a ShortClose reason, else `MFG_PO_012`. Completing completes the open operations, closes the open time logs, stamps the end time and releases the reservation. | TC-PO-030, 031, 032, 033. |
| CO-2 | **Reopen:** Completed -> InProgress, end time cleared, reservation rebuilt for what is still needed; operations keep their state. | TC-PO-034. |
| CO-3 | A **return** of issued material is accepted on a Completed order (not an issue). | Unit; live. |
| CL-1 | **Close checklist:** (1) no open material request (always met until requests exist), (2) no open time log, (3) no lot still on quality hold, (4) issued material settled - for each line net issued minus what was settled is 0, or the user confirms "carry the rest to consumption", (5) costing (Phase 6, always met), (6) no stock document or report on its way. | TC-PO-036, 037. |
| CL-2 | **Close:** Completed -> Closed when every condition is met; with confirmation, consumed quantity increases by the unsettled rest and the settled quantity is recorded. A failing close is `MFG_PO_013` listing the unmet conditions. A closed order is read only (`MFG_PO_014`); closing an order that is not Completed is `MFG_PO_002`. | TC-PO-038, 039, 040. |
| CL-3 | The history of the order shows every move with reason and person. | TC-PO-042. |
| QC-1 | **Interim quality release:** for each held, unreleased line of a posted receipt: the lot moves from the quarantine warehouse to the order's output warehouse (key `<line key>Q1`), then is released (key `<line key>Q2`), then the line records the release time. Repeating is harmless; a release that stopped halfway is finished by calling again. | Unit; live. |
| EV-1 | Events `FinishedGoodsReceived` (receipt lines with lot, hold, expiry), `MaterialBackflushed` (consumption part), `ProductionOrderCompleted` (automatic / short-close), `ProductionOrderClosed` leave through the outbox. | Persistence tests. |

## 4. Business rules
| ID | Rule | Code |
|---|---|---|
| BR-FG-01 | Limit of the main product (FG-2). | `MFG_FG_001` |
| BR-FG-02 | Lot number unique per item; generated from the format; dates (FG-4, FG-5). | `MFG_FG_002` |
| BR-FG-03 | Serial finished goods refused for now (FG-7). | `MFG_MAT_011` |
| BR-FG-04 | Final inspection items go to the quarantine warehouse on hold (FG-6). | - |
| BR-FG-05 | Receipt backflush, `CompletedQty`, auto-complete (FG-8, FG-10, FG-11). | `MFG_MAT_008` |
| BR-FG-06 | Co- / by-product ceiling (FG-3). | `MFG_FG_001` |
| BR-OP-09 | The good output of the last operation caps the receipts. | `MFG_FG_001` |
| BR-PO-14 | Complete (auto, manual, short-close) (FG-11, CO-1). | `MFG_PO_012`, `MFG_PO_013` |
| BR-PO-15 | Reopen (CO-2); the period lock is Phase 6. | - |
| BR-PO-16 | Close (CL-1, CL-2). | `MFG_PO_013`, `MFG_PO_014` |
| BR-PO-17 | Over-production ceiling (FG-2). | `MFG_FG_001` |
| BR-PO-19 | `CompletedQty` / `ScrappedQty`. | - |
| New | A receipt on an order that is not Released / InProgress; return on a Completed order. | `MFG_FG_005` |

## 5. State models
**Order:** ... InProgress -> Completed (auto, manual, short-close) -> InProgress (reopen) | Closed (final). **Receipt document:** as the other stock documents (Pending -> Posted | Failed; Forward / Rollback); lines of kind Material (backflush consumption) and Receipt; a receipt line on quality hold is `IsQcHold` until `QcReleasedAt` is set.

## 6. Use cases
**UC-1 Receive and complete (main).** MO-A (40 pieces) has reported 40 good on all three operations. The storekeeper receives 40: the Warehouse creates lot `TP-BAN01-261104-01` in the quarantine warehouse on hold; `CompletedQty` 40; the order completes by itself, operations are completed, the unused reservation goes back. *Alternative A - partial:* 15, then 10 on the same day: lots -02, -03. *Alternative B - by-product:* BTP-MAT01 x 50: 48 pieces and 24 kg of by-product in one receipt. *Alternative C - unbound backflush:* 10 received consume 160 screws. *Exception:* over the ceiling, lot number exists, serial item, held / draft order, not enough screws - refused with its code, nothing stored.
**UC-2 Quality and close.** The inspector releases the lot (moves to the finished-goods warehouse, Available). The manager returns 1 kg of glue, runs the checklist (lot released, issued paint and glue not settled) and closes with "carry the rest to consumption": consumed quantities are updated, the order is Closed and read only.
**UC-3 Short-close.** 25 of 50 received and the customer accepts that: completing without a reason is refused (`MFG_PO_012`); with the ShortClose reason and permission the order completes; if more is found it can be reopened and completed again.
**UC-4 The Warehouse is away.** A receipt that needs no Warehouse read (no lot, no backflush) is accepted as Pending (202); the order cannot be completed meanwhile; after restart the job posts it once. A receipt that needs the Warehouse first (lot list, backflush stock) is refused `MFG_INT_001`.

## 7. Data
`MaterialTransactionLines` gains `Kind`, `OrderOutputId`, `OutputLineNo`, `LotNumber`, `ExpiryDate`, `ManufactureDate`, `IsQcHold`, `QcReleasedAt` and nullable `OrderMaterialId` / `MaterialLineNo`; `ProductionOrderMaterials.SettledIssueQty` - SQL `src/Database/Manufacturing/010_FinishedGoodsReceipts.sql` (idempotent). Warehouse: no schema change. Receipt numbers `FR` are counted per tenant and month (the number is the Warehouse key).

## 8. Integrations
Warehouse `internal/v1/stock`: new read `GET {productId}/lots` (all lots, used-up ones included); movements `ProductionReceipt` (lot number, expiry, manufacture date, `initialLotStatus` QcHold, product name and SKU so that the **first receipt of an item the Warehouse never held creates its record**), `ProductionIssue` / `ProductionReturn` for the backflush part, `ProductionScrap` / `ProductionIssue` for the reversal of a receipt line, `SubcontractTransfer` + `QcRelease` for the interim release. Unit cost is not sent: the Warehouse values the receipt at the item cost (actual costing is Phase 6). Production movements are not in the ledger before Phase 6. Warehouse must be deployed **before** Manufacturing (new endpoint and fields).

## 9. Permissions and endpoints
| Action | Endpoint | Permission |
|---|---|---|
| Receive | `POST api/v1/manufacturing/orders/{id}/receipts` `{ lines: [{ outputLineNo?, quantity, lotNumber? }], note? }` + `Idempotency-Key` | `manufacturing:receipt:post` |
| Complete / short-close | `POST .../orders/{id}/complete` `{ reasonCodeId?, note? }` | `manufacturing:operation:complete-manual` or `manufacturing:order:short-close` (short-close itself needs the latter) |
| Reopen | `POST .../orders/{id}/reopen` | `manufacturing:order:reopen` |
| Checklist | `GET .../orders/{id}/close-checklist?confirmConsumeRemaining=` | `manufacturing:order:view` |
| Close | `POST .../orders/{id}/close` `{ confirmConsumeRemaining }` | `manufacturing:order:close` |
| Quality release (interim) | `POST .../stock-documents/{id}/qc-release` | `manufacturing:qc:disposition` |
| List receipts | `GET .../stock-documents?type=Receipt&orderId=` | `manufacturing:order:view` |

## 10. Reports
None new. The receipt list (Pending / Failed) and the close checklist are the control reports.

## 11. Assumptions and open issues
- Unit cost of a receipt is the item cost; actual costing, scrap value and by-product allocation arrive with Phase 6.
- The Warehouse does not refuse a duplicate lot number; Manufacturing checks against the Warehouse's list, so two simultaneous receipts with the same typed number can still collide.
- `CompletedQty` counts received quantity even while its lot is on hold.
- A return is accepted on a Completed order - a deviation from BR-MAT-01 as written, needed for the settlement before close (TC-E2E-001 step 11).
- Reversal of a receipt, final inspection documents, rework orders and child orders come later.
- The ceiling uses the applied good output of the last operation (reports waiting for the Warehouse are not counted).
- A by-product item without a Manufacturing profile is treated as needing no final inspection.
- A receipt on a Pending state blocks completion and close until the document is posted or abandoned.

## 12. UAT
1. MO-A of 40 pieces: report 40 good on operations 10, 20, 30. Receive 41: `MFG_FG_001`. Receive 40: document FR, lot `<item>-<yymmdd>-01` on hold in the quarantine warehouse, order Completed by itself, operations Completed, reservation released.
2. Same key again: same document. Receive with a lot number that exists (also a used-up lot): `MFG_FG_002`.
3. Close checklist: lot on hold and unsettled material are unmet; close: `MFG_PO_013` naming them. Quality release: the lot is Available in the finished-goods warehouse; repeat: nothing changes.
4. Return 1 kg of glue (Completed order). Close without confirmation: refused; with confirmation: Closed, consumed quantities updated; any action: `MFG_PO_014`.
5. Order of 50: report 25 good; receive 26: `MFG_FG_001`; 15 then 10: lots -02, -03; complete without a reason: `MFG_PO_012`; with the ShortClose reason: Completed; reopen: InProgress; complete again.
6. BTP-MAT01 x 50: by-product 26.26: refused; main 48 + by-product 24: two receipt lines, by-product received 24.
7. Stop the Warehouse: a receipt of an item without lot and backflush answers 202 and the order cannot be completed; a receipt of a lot item is refused `MFG_INT_001`; start the service: the receipt is posted once.
8. A user without the permissions gets 403 on every endpoint above.

**Screens (P3-6):** the receipt dialog S19, the interim quality release, the complete / short-close / reopen buttons and the close checklist dialog are described in `ProductionOrderScreens.md`.
