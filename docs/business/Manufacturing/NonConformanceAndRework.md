# Manufacturing - non-conformance reports, dispositions and rework orders (Phase 4, P4-4)

Specification: `docs/business/M/01_PRODUCTION_BA_SPEC.md` §5.15 / §5.16 (BR-NCR-01..04, BR-RW-01..03, BR-QC-05 Fail branch), §7.4, screen S23; tasks T4.10 and T4.11; test cases TC-NCR-001..007, TC-RW-001..004 and the creation part of TC-E2E-002.
Built 2026-11-17 on branch `feat/manufacturing-p4-4`. Genealogy of the `-R` lot back to the original lot is P4-5.

## 1. Purpose and scope
Before P4-4 a lot whose final inspection **failed** stayed in the quarantine warehouse for good (R279): it could not be reversed, released by hand (`MFG_QC_004`) or closed with the order. A failed in-process inspection kept its Good quantity out of the next operation. P4-4 adds the **non-conformance report (NCR)**: the record of the units that did not pass, and what is done with them - **Rework**, **Scrap** or **Use as is** - and the **rework order** that makes the reworked goods come back through quality.

In scope: the NCR (created by a failed inspection or by hand from an operation report), its disposition lines and their execution, rework orders, the rework receipt `<lot>-R<n>`, the close checklist, the S23 screen.
Out of scope: genealogy (P4-5), the cost report of rework (Phase 6, the order is only flagged), WIP rework (owner decision, see rule BR-NCR-05), NCR print-outs.

## 2. Actors and permissions
| Actor | What they do | Permission (`manufacturing:...`) |
|---|---|---|
| Quality inspector (R-QCI) | submits an inspection (a failed one creates the NCR), raises an NCR by hand from an operation report | `qc:inspect` |
| Quality manager (R-QAM) | decides the dispositions, executes the lines, approves *use as is* | `qc:disposition`, `qc:approve-deviation` |
| Supervisor / planner | reads the reports, releases and runs the rework order like any other order | `qc:view`, order and material permissions |

`qc:approve-deviation` is new (Identity script 037, Admin and SuperAdmin of every tenant). A shop gives it to its quality managers on the Roles screen. The spec name is `Production.Qc.ApproveDeviation`.

## 3. Process
1. **A final inspection fails** (FQC). In the same save the system creates the NCR (`NCR-yymm-nnnnn`, status **Open**) for the **whole inspected lot**: the lot stays on hold in the quarantine warehouse.
2. **Decide.** The quality manager splits the quantity into lines (Rework / Scrap / Use as is). The lines must add up to the non-conforming quantity (BR-NCR-02). A Scrap line names a scrap reason; a Use as is line needs the approval right and records who approved it. The NCR becomes **InDisposition**.
3. **Execute**, line by line, in any order. Each execution is a Warehouse effect made **before** the line is marked (a Warehouse outage = `MFG_INT_001`, nothing marked, execute again; the keys are fixed so the stock moves exactly once):
   * **Scrap:** the quantity leaves the held lot (`ProductionScrap`, reason `NCR <no>: scrap - <reason>`) (TC-NCR-005).
   * **Use as is:** the quantity moves from the quarantine warehouse to the order's finished-goods warehouse (a part of the lot becomes a sub-lot `<lot>-S<n>`) and is released for sale (TC-NCR-006).
   * **Rework:** a **rework order** is created (Draft). The line is *started* and counts as executed when that order is **Completed**.
4. **Rework order.** The user releases it, issues the held lot to it (the dialog has the lot fixed), reports operations if a routing was chosen, and receives the goods: the lot is the original lot plus `-R<n>`, it goes to the quarantine warehouse on hold and a new FQC is created when the item's plan applies (TC-RW-003). Receiving all of the quantity completes the order, which executes the NCR line.
5. **The NCR closes** with its last line (BR-NCR-04): the lot of its receipt line is *settled*, so the order's quality-hold condition is met and the original order can be closed.
6. **In-process failure (IPQC).** A failed in-process inspection creates an NCR for the held Good quantity of the operation. Only **Scrap** (the held units become Scrap on the operation and the order; no stock moves) and **Use as is** (the hold is lifted and the next operation may take them) are possible.
7. **By hand.** From a posted operation report a user may raise an NCR (`qc:inspect`): it is a **record only** - the lines can be decided and ticked off, nothing moves in the Warehouse or on the operation.

## 4. Business rules
| Rule | Statement | Code |
|---|---|---|
| BR-NCR-01 | `NCR-yymm-nnnnn`; source = a failed inspection or an operation report; item, lot (FQC), order; the non-conforming quantity is the whole inspected lot (an FQC result decides the whole lot) or, by hand, at most the units the report counted. The number is per **tenant** because it is also the key of the Warehouse movements. | `MFG_VALIDATION` |
| BR-NCR-02 | Dispositions are lines Rework / Scrap / Use as is; the sum equals the non-conforming quantity; a quantity above 0; a Scrap line has its reason. The lines are replaced as a set while none has started. | `MFG_NCR_001`, `MFG_NCR_004` |
| BR-NCR-03 | Use as is needs `qc:approve-deviation` (checked when the lines are saved); Scrap books a scrap movement from the held lot with the Scrap reason; Rework creates the rework order. | `MFG_NCR_002` |
| BR-NCR-04 | The NCR closes when every line is executed. A Rework line is executed when its rework order is Completed. | - |
| BR-NCR-05 | Output held by a failed in-process inspection has no lot in stock: it can be scrapped or accepted as is, **not reworked**. | `MFG_NCR_003` |
| BR-NCR-06 | A re-inspection of a failed inspection **replaces** its NCR (the NCR is Cancelled) while no line has started; once a line started or executed the NCR must be finished instead. | `MFG_NCR_004` |
| BR-NCR-07 | While an NCR of the order is Open or InDisposition the order cannot be closed (quality-hold condition). | `MFG_PO_013` |
| BR-RW-01 | Rework order: type Rework, same item, quantity of the line, source Ncr (`SourceRefId` = the NCR), parent = the original order, **no BOM**, optional routing and due date; created Draft. | - |
| BR-RW-02 | The only material is the held lot (fixed lot, issued from the quarantine warehouse with the Warehouse's held-lot issue and the rework order in its reason). Other lots are refused. The reworked goods are received as `<root lot>-R<n>` (the smallest free n; a rework of `X-R1` is `X-R2`), always into the quarantine warehouse on hold; the final inspection is created when the item's plan applies. | `MFG_MAT_005`, `MFG_FG_002` |
| BR-RW-03 | A rework order is flagged (`isRework`) in its DTO and its created event so that costing can report it separately (Phase 6). | - |
| BR-QC-05 (Fail) | A failed FQC keeps the lot on hold and creates the NCR; a failed IPQC keeps the quantity held and creates the NCR. | - |

## 5. Data and states
`NonConformances` (number per tenant, source, inspection / report, order, operation sequence, item, lot, receipt document and line, non-conforming quantity, status Open / InDisposition / Closed / **Cancelled**) and `NcrDispositions` (line, type, quantity, scrap reason, approver, rework order, movement key, released lot, executed flag). `ProductionOrders.BomId` is now optional; `ProductionOrderMaterials.SourceLotId / SourceLotNumber` fix the lot of a rework order; `MaterialTransactionLines.AllowHeldLot / MovementReason` carry the held-lot issue (SQL 014).

```
NCR:   Open --save dispositions--> InDisposition --last line executed--> Closed
       Open | InDisposition --re-inspection (no line started)--> Cancelled
Line:  pending --execute--> executed          (Rework: pending --order made--> started --order Completed--> executed)
```

## 6. Integration
* **Warehouse (no change):** the keys are `<NCR no>#<line>` for scrap, `...Q1` / `...Q2` for the transfer and release of a use-as-is line; the held-lot issue of a rework order is a `ProductionIssue` with `allowHeldLot` and a reason naming the rework order and its NCR (R267); the user endpoint of the Warehouse cannot set that flag.
* **Events:** `NcrCreated`, `NcrClosed` on `manufacturing-topic` (no consumer yet).
* **Close checklist:** `QUALITY_HOLD` is met when no lot is held **and** no NCR of the order is open.

## 7. Screens
S23 `/manufacturing/ncrs` (worklist: open ones by default, status and source filters) and `/manufacturing/ncrs/:id` (the document: header with the links to the order and the inspection; the disposition editor with a live sum and the quantity still to assign shown as text; *Save*; per line a status badge, the approver, the released lot, the rework order link and *Execute* after a confirm that says what it does). The failed inspection links to its NCR; the order's **Quality** tab lists the NCRs; the **Operations** tab has *Raise NCR* on a posted report; a rework order shows a *Rework* badge and a link to its NCR, and its issue dialog has the lot fixed.

## 8. Use cases
**UC-NCR-1 Failed lot (main).** FQC fails (a value of 753 mm) -> NCR Open for 100 -> Rework 10 + Scrap 5 + Use as is 85 saved (approver) -> Scrap executed (5 gone, 95 held) -> Use as is executed (85 as a sub-lot Available in finished goods, 10 still held) -> Rework executed (rework order Draft) -> release, issue the held 10, receive 10 as `<lot>-R1` on hold with a new FQC -> the rework order completes, the NCR closes, the original order closes. *Alternative:* the lot is wholly scrapped or wholly accepted (one line); the re-inspection of the failed inspection passes before any line started (the NCR is cancelled). *Exception:* lines that do not add up (`MFG_NCR_001`), use as is without the right (`MFG_NCR_002`), Warehouse down (`MFG_INT_001`, execute again), a lot that no longer holds the quantity (`MFG_NCR_005`).
**UC-NCR-2 In-process.** IPQC of 6 units fails -> NCR for 6 -> Scrap 2 + Use as is 4 -> operation 10 shows Good 4, Scrap 2; operation 20 may take 4.
**UC-NCR-3 By hand.** A supervisor sees scratched panels in a report: NCR for 3 from the report, Scrap 1 + Use as is 2, ticked off; nothing else changes.

## 9. Assumptions and open issues
Recorded as R287-R295 in `docs/DEFERRED-ISSUES.md`. Notably: execution calls the Warehouse inside the request (like the FQC pass); a rework line counts as executed when the rework order completes (a short-closed rework order leaves unrecorded units); an item without an approved FQC plan reworks into a held lot that only the interim release can free; the quantity of an NCR is always the whole lot (no partial NCR); the Admin is verified without a real browser (R251).

## 10. UAT scenarios
1. Fail an FQC of 100 (value 753): the NCR is Open for 100 and the lot is on hold (TC-NCR-001).
2. Save Rework 10 + Scrap 5 + Use as is 80: refused with the total 100 (TC-NCR-002); with 85 accepted (TC-NCR-003); as a user without the approval right: refused (TC-NCR-004).
3. Execute Scrap: 5 leave the lot (TC-NCR-005); stop the Warehouse and execute Use as is: refused, start it and repeat: one transfer and one release (TC-NCR-006).
4. Execute Rework, release the rework order, issue the lot, receive 10: lot `<lot>-R1` on hold, new inspection; pass it (TC-RW-001..003); the NCR closes (TC-NCR-007) and the original order closes.
5. Re-inspect a failed inspection whose NCR is untouched: the NCR is cancelled and a pass releases the lot; after a line started the re-inspection is refused (TC-QC-013).
6. Fail an IPQC: Rework is refused, Scrap + Use as is move the figures of the operation.
7. Raise an NCR from a report: record only.

Live proof: `scripts/manufacturing-live/mfg-p44.js` on glcheck and SYSTEM and the extended contract check `mfg-p36-contract-check.js`.
