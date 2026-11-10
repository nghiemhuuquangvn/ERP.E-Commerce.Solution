# Manufacturing - stock-document engine, material issue and return (Phase 3, P3-3)

Added 2026-11-10. Source: `docs/business/M/01_PRODUCTION_BA_SPEC.md` §5.11 (BR-MAT-01..07), BR-PO-10, §7.3 (MaterialTransaction), §8.1 (events), NFR-05; tasks T3.5 and T3.6; tests TC-MAT-001..008, 012..017, TC-GEN-010, TC-INT-002, TC-PO-017/018/020. Decision record: `docs/architecture/adr-manufacturing-phase3.md` ("Decisions taken while planning P3-3"). Follows `ProductionOrders.md` (orders, release, reservation). Substitutes (BR-MAT-09), unplanned material (BR-MAT-11), material requests (BR-MAT-10), backflush (P3-4, now in `OperationsAndBackflush.md`: a document of type Backflush on this engine) and receipts (P3-5) are not part of this milestone.

## 1. Purpose and actors
Material leaves the warehouse for the shop floor (issue) and comes back (return). The specification wanted the stock movement and the order's figures to change in one database transaction. The Warehouse is another service with its own database, so every stock movement is a call that can fail, time out or be repeated. The **stock-document engine** keeps the two consistent: a document is stored first, sent to the Warehouse line by line under keys that make repeating harmless, and only when every line is in the Warehouse does the order show the effect.

| Actor | What they do here |
|---|---|
| Storekeeper | Issues material to an order and returns what the line did not use (`material:issue`, `material:return`); sees the documents and whether they reached the Warehouse. |
| Production manager | Same, and may issue **beyond the tolerance** with a reason (`material:over-issue`); can retry or abandon a document that is stuck (`stock-document:retry`). |
| Production planner / supervisor | Reads the issued and returned quantities of an order (`order:view`). |
| Warehouse (system) | Books each movement once per key, consumes the order's reservation, values the stock. |
| Retry job (system) | Sends pending documents again until the Warehouse has them. |

## 2. Process
1. **Request.** The user chooses material lines of a Released or InProgress order (line, quantity, lot if needed), optionally a reason, and posts it (optionally with an `Idempotency-Key`).
2. **Checks first.** Everything this service can know is checked before anything is stored (state of the order, quantity, tolerance, lot rules, stock, return limits). A violation is a plain refusal with its code and **no document**.
3. **Store.** A document `MI-yyMM-nnnnn` (issue) or `MR-yyMM-nnnnn` (return) is stored `Pending` with its lines. Each line is **one Warehouse movement**; an issue that is larger than what the order has reserved is stored as two lines - the reserved part and the part from free stock.
4. **Send.** The lines go to the Warehouse in order, each under its key (`<DocNo>#<n>`; a free-stock issue ends with `F`). A line that is in the Warehouse is marked Posted at once.
5. **Done.** When every line is Posted, one save updates the order (issued / returned quantities, first issue starts the order), sets the document `Posted`, and writes the event. The answer is **201**.
6. **Not done.** If the Warehouse could not be reached the document stays `Pending` (**202**); the retry job tries again every few seconds up to five minutes apart, without ever giving up, and the user can retry or abandon it. If the Warehouse **refuses** a line, the lines already booked are reversed and the document ends `Failed` (**400** with the code).

## 3. Requirements and acceptance criteria
| ID | Requirement | Acceptance (test) |
|---|---|---|
| MAT-1 | Material is issued or returned only for a Released or InProgress order. Draft / Completed: `MFG_MAT_001`; OnHold: `MFG_PO_011`; Closed `MFG_PO_014`; Cancelled `MFG_PO_002`. | TC-MAT-002, 003, TC-PO-020 (unit, live). |
| MAT-2 | The quantity is greater than zero and within the decimals of the material's unit; lines for the same material and lot in one request are added together. | KEO01 5.005 refused (2 decimals); merged lines (unit). |
| MAT-3 | Net issued (issued - returned) plus issues still on their way may not pass `Required x (1 + OverIssueTolerancePct)`; the boundary is inclusive. Beyond it the caller needs `material:over-issue` **and** a reason of type OverIssue, else `MFG_MAT_002` (naming the maximum). The plant's tolerance at the time of the request applies. | KEO01 required 10, tolerance 3 %: 10.30 accepted, 10.31 `MFG_MAT_002` (TC-MAT-004/005); 12.00 with permission + reason OVI-01 accepted, reason stored (TC-MAT-006); tolerance changed to 0: 10.30 refused (TC-SET-003). |
| MAT-4 | An issue may not exceed what the Warehouse can give: free stock of the material's warehouse (lot-less stock and Available, unexpired lots) **plus what the order itself has reserved**, else `MFG_MAT_003` (naming the available quantity). | CHAN 350 against 300 (all reserved for the order): refused, no document (TC-MAT-007); CHAN 300 accepted. |
| MAT-5 | A lot-controlled material must name its lot for the part taken from free stock (`MFG_MAT_004`); the part taken from the reservation may leave the lot to the Warehouse (the reservation already holds lots). A named lot must be Available, not on quality hold and not expired (`MFG_MAT_005`). | SON beyond the reservation without a lot: `MFG_MAT_004` (TC-MAT-008); expired L-SON-X, held and unknown lots: `MFG_MAT_005` (TC-MAT-009/011). |
| MAT-6 | Serial-controlled materials are refused (`MFG_MAT_011`). | TC-MAT-012 (variant). |
| MAT-7 | An issue consumes the order's reservation first; only the rest is taken from free stock. A return never creates a reservation. | Live: reservation line of KEO01 consumed 10 / remaining 0 (BR-MAT-07); return leaves reservations as they are (TC-MAT-017). |
| MAT-8 | The first issue moves a Released order to InProgress and records `ActualStart` (BR-PO-10). A return does not. | TC-PO-018. |
| MAT-9 | A return is limited to the net issued quantity of the material (`MFG_MAT_006`), counting returns already waiting; for lot-controlled materials it names the lot (`MFG_MAT_004`) and is limited by what was issued from that lot (issues whose lot is not known, drawn from several lots by the reservation, widen the limit); the lot is kept. | KEO01 net 10: return 10.01 refused (TC-MAT-016); SON return without lot `MFG_MAT_004`; into a lot over its share `MFG_MAT_006` (TC-MAT-015). |
| MAT-10 | A return is booked at the weighted average unit cost of the issues it gives back. | Issues at 4 and 6 (6 + 4 pieces): return costed 4.80; live: equals the issue cost. |
| MAT-11 | The effect on the order (issued / returned quantities, status) is applied **only** when every line of the document is in the Warehouse; until then the order shows nothing and counts the document as pending. | Unit: outage on line 2 -> order unchanged; job posts -> figures change once. |
| MAT-12 | Booking is idempotent: each line has a key; sending it again (after a timeout, a lost answer, a restart) never moves stock twice. | Unit: first line replayed, not booked twice; live: one Warehouse movement per key after the outage. |
| MAT-13 | A Warehouse **refusal** reverses what the document already booked (counter-movements under `<key>R`) and ends it `Failed` with the mapped code; nothing reaches the order. | TC-INT-002 (unit): refusal on line 2 -> line 1 reversed, stock back, order unchanged. |
| MAT-14 | A Warehouse **outage** never fails a document: it stays Pending with the attempt count and the last error visible, and the retry job finishes it. | Live: Warehouse stopped, return answers 202; started again, the job posts it once. |
| MAT-15 | An order with a pending document cannot be unreleased or cancelled (`MFG_PO_008`). | Unit; live: cancel refused during the outage. |
| MAT-16 | A user can **retry now** or **abandon** a pending document (`stock-document:retry`); abandoning reverses what is booked and ends it `Failed` (`ABANDONED`). | Unit; live (abandon with nothing booked). |
| MAT-17 | `Idempotency-Key` (optional, up to 100 characters): the same key and request answer the same document; the same key with another request is refused (`MFG_GEN_007`); parallel posts with one key create one document. | TC-GEN-010 (unit; live with 6 parallel posts). |
| MAT-18 | Before an issue the order's reservation must be in line with the Warehouse; if it is being updated and cannot be finished now the issue is refused with `MFG_MAT_012` (retry later). | Unit. |
| MAT-19 | Lots are suggested first-expiry-first (never expired or held lots), together with the lots the order's own reservation holds. | Live: suggestion for SON has no L-SON-X and shows the reserved lots L-SON-A / L-SON-B. |
| MAT-20 | Every posted document leaves `MaterialIssued` / `MaterialReturned` on `manufacturing-topic` through the outbox with its lines (item, lot, quantity, unit cost, amount). | Persistence test: event restored from the outbox row. |

## 4. Business rules
| ID | Rule | Code |
|---|---|---|
| BR-MAT-01 | State of the order as in MAT-1. | `MFG_MAT_001`, `MFG_PO_011`, `MFG_PO_014`, `MFG_PO_002` |
| BR-MAT-02 | Tolerance, permission and reason as in MAT-3. | `MFG_MAT_002` |
| BR-MAT-03 | Availability including the order's own reservation (MAT-4). | `MFG_MAT_003` |
| BR-MAT-04 | Lot rules (MAT-5); first-expiry-first suggestion. | `MFG_MAT_004`, `MFG_MAT_005` |
| BR-MAT-05 | Return limits and lot kept (MAT-9). | `MFG_MAT_006` |
| BR-MAT-06 | Document, stock movements and reservation consumption stay consistent - realised as a pending document with idempotent Warehouse calls, reversal and retry (not one database transaction). | - |
| BR-MAT-07 | An issue consumes the reservation; a return does not re-reserve. | - |
| BR-PO-10 | The first issue starts the order. | - |
| New | The reservation must be in line before an issue; an `Idempotency-Key` cannot be reused for another request. | `MFG_MAT_012`, `MFG_GEN_007` |

## 5. State models
**Document:** `Pending` -> `Posted` (every line in the Warehouse and the order updated) | `Failed` (refused or abandoned, and nothing is left in the Warehouse). **Mode:** `Forward` (sending lines) or `Rollback` (reversing lines already booked; the document stays `Pending` until the reversal is complete). **Line:** `Pending` -> `Posted` | `Refused` (not sent after a refusal, or refused) ; `Posted` -> `Compensated` (reversed). A reserved line that the reservation can no longer cover is converted once to a free-stock line (key gets the suffix `F`) - only while it was never booked.

## 6. Use cases
**UC-1 Issue (main).** The storekeeper issues 10 kg of KEO01 to MO-A: the system checks the order, the tolerance and the stock, stores `MI-2611-00001` with one reserved line, the Warehouse books it and consumes the reservation, the order shows 10 kg issued and becomes InProgress. *Alternative A - more than reserved:* 54 l of paint where 52.5 are reserved: two lines (52.5 from the reservation, 1.5 from free stock). *Alternative B - beyond the tolerance:* the production manager issues 12 kg with reason OVI-01. *Alternative C - several materials:* one document, one line per material (and lot). *Exception:* Draft / held / completed order, over tolerance, not enough stock, lot expired or missing, serial item - refused with its code, nothing stored.
**UC-2 Return.** The storekeeper returns 2 kg: a document `MR-...` at the issue cost; the order's net issued falls to 8. *Exception:* more than is out (`MFG_MAT_006`), lot not named.
**UC-3 The Warehouse is away.** A return is accepted while the Warehouse is down: the answer says the document is pending (202); the order does not show it yet and cannot be cancelled; when the Warehouse is back the retry job posts it - once. The storekeeper may retry at once or abandon the document. An **issue** is checked against the Warehouse first, so while it is down an issue is refused (`MFG_INT_001`) and nothing is stored.
**UC-4 A line is refused.** Line 2 of 3 is refused by the Warehouse (a lot expired a moment ago): line 1 is reversed, line 3 is never sent, the document is Failed with the code; the order is unchanged.

## 7. Data
Tables `MaterialTransactions` (document: number, type, status, mode, order, posting date, reason, idempotency key + request hash, attempts, next attempt, last error and code) and `MaterialTransactionLines` (order material, item, warehouse, lot, quantity, from reservation, movement key, status, Warehouse movement id, unit cost, amount, lot booked, reversal movement id, error) - SQL `src/Database/Manufacturing/008_StockDocuments.sql` (PostgreSQL, idempotent, `xmin` concurrency). Document numbers `MI` / `MR` are counted **per tenant and month** (deviation from BR-GEN-04, which counts per plant): the number is the key the Warehouse deduplicates on.

## 8. Integrations
Warehouse `internal/v1/stock/*` through `IWarehouseStockClient`: availability and reservation reads (before an issue), lot suggestion, movements `ProductionIssue` (with `ReservationId` and `SourceLineId` for the reserved part; a named lot) and `ProductionReturn` (lot, unit cost). Lot / serial flags come from Product (cached for a minute). Warehouse codes become `MFG_MAT_003` (`WH_INSUFFICIENT_*`), `MFG_MAT_005` (`WH_LOT_EXPIRED`, `WH_LOT_NOT_USABLE`, `WH_LOT_NOT_AVAILABLE`), `MFG_MAT_004` (`WH_LOT_REQUIRED`), `MFG_MAT_011` (`WH_SERIAL_NOT_SUPPORTED`); other codes pass through. Events `MaterialIssued`, `MaterialReturned` (catalogued). Production movements are not posted to the general ledger before Phase 6.

## 9. Permissions and endpoints
| Action | Endpoint | Permission |
|---|---|---|
| Issue | `POST api/v1/manufacturing/orders/{id}/material-issues` (+ header `Idempotency-Key`) | `manufacturing:material:issue` (beyond tolerance also `manufacturing:material:over-issue`) |
| Return | `POST .../orders/{id}/material-returns` | `manufacturing:material:return` |
| Suggest lots | `GET .../orders/{id}/material-issues/suggest-lots?lineNo=&quantity=` | `manufacturing:material:issue` |
| Documents of an order | `GET .../orders/{id}/stock-documents` | `manufacturing:order:view` |
| List / read | `GET .../stock-documents?plantId=&status=&type=&orderId=&search=`, `GET .../stock-documents/{id}` | `manufacturing:order:view` |
| Retry / abandon | `POST .../stock-documents/{id}/retry`, `.../abandon` | `manufacturing:stock-document:retry` |
Request body of issue and return: `{ "lines": [{ "lineNo": <material line of the order>, "quantity": n, "lotId": guid? }], "reasonCodeId": guid?, "note": text? }`. Answers: 201 (Posted), 202 (Pending), 400 (refusal with `errorCode`), 404 (a plant the caller may not see), 409 (conflict). The order DTO carries `pendingStockDocuments`.

## 10. Reports
None new. The stock-document list is the control report: Pending (not yet in the Warehouse) and Failed (refused / abandoned) documents per plant.

## 11. Assumptions and open issues
- The posting date is always today (plant time); back-dating waits for the period lock of Phase 6.
- The quantity of an issue is in the material's unit; there is no conversion at issue.
- Cross-request race: two simultaneous requests are each checked against the figures they see (pending quantities count, but a request that has not stored yet does not); the Warehouse still protects the stock.
- An issue is refused while the Warehouse is unreachable (it reads availability first); a Pending **issue** therefore appears only when the Warehouse fails *between* the check and the booking, or in the middle of a multi-line document.
- A document stuck Pending blocks unrelease and cancel of its order; the exits are retry and abandon.
- A reversal needs the Warehouse as well; a failing reversal keeps the document Pending in Rollback mode until it succeeds.
- Issues drawn by the reservation from several lots do not record which lot; returns of lot-controlled material to a lot that was never issued from are possible up to the unattributed quantity.
- Failed documents are kept forever.

## 12. UAT
1. MO-A released. Issue KEO01 10 kg: document MI, Posted, order InProgress with a start time, Warehouse stock down 10, the reservation line of KEO01 consumed.
2. Post the same request again with the same `Idempotency-Key`: same document, one Warehouse movement. Same key with another quantity: refused.
3. Issue 0.30 kg more: accepted (10.30). 0.01 more: `MFG_MAT_002`. 1 kg with a reason that is not an over-issue reason: refused. With the over-issue permission and reason OVI-01: accepted.
4. Issue CHAN 350 (stock 300): `MFG_MAT_003`, no document appears in the list.
5. Paint: suggest lots (no expired lot; reserved lots shown); issue 52.5 l without naming a lot (taken from the reservation); issue 1 l more without a lot: `MFG_MAT_004`; with lot B: accepted; with the expired lot: `MFG_MAT_005`.
6. Return KEO01 2 kg: document MR at the issue cost, returned 2, net issued 8. Return 100: `MFG_MAT_006`. Return paint without a lot: `MFG_MAT_004`; into lot B: accepted.
7. Hold the order: issue refused (`MFG_PO_011`); resume.
8. Stop the Warehouse service: an issue is refused (`MFG_INT_001`); a return answers 202 and shows as pending; the order cannot be cancelled; retry now still pending; start the service: within about a minute the document is Posted, once.
9. Stop the service again, post a return (pending) and abandon it: Failed (ABANDONED), order unchanged, not pending any more.
10. A user without the permissions gets 403 on every endpoint above.

**Screen (P3-6):** the issue / return dialog S18 (typed or scanned lots, suggest lots, over-issue reason on demand, idempotency key per request) is described in `ProductionOrderScreens.md`.
