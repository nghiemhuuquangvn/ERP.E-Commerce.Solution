# Manufacturing - quality: inspection plans and IPQC / FQC inspections (Phase 4, P4-3)

Specification: `docs/business/M/01_PRODUCTION_BA_SPEC.md` §5.15 (BR-QC-01..06), §7.4, screens S21 / S22; tasks T4.8 and T4.9; test cases TC-QC-001..015 and the creation part of TC-NCR-001.
Built 2026-11-11 on branch `feat/manufacturing-p4-3` (two commits: plans, then inspections). Non-conformance reports and rework orders are P4-4.

## 1. Purpose and scope
Before P4-3 a finished-goods lot of a `RequiresFqc` item sat on quality hold until somebody pressed the interim *Release from quality hold* button, which left no record of what was checked, and an operation never looked at quality. P4-3 adds:

* **Inspection plans** (S21): what is checked, how many units are taken, how many minor defects are tolerated - versioned like a BOM.
* **Final inspections (FQC)**: one document per received lot on hold; a Pass releases the lot, a Fail keeps it on hold and asks for a non-conformance report.
* **In-process inspections (IPQC)**: one document per Good report of an operation that has a plan; the Good quantity reaches the next operation only when it passed.
* The screens S22 (worklist and document), the Quality tab of the order and the plan pickers on the item profile and the routing operation.

Out of scope (later milestones): the NCR itself and its dispositions (P4-4), genealogy (P4-5), the tablet grid for entering values (P4-8), sampling by AQL tables.

## 2. Actors and permissions
| Actor | What they do | Permission (`manufacturing:...`) |
|---|---|---|
| Quality manager (R-QAM) | creates, edits and copies draft plans; approves or makes them obsolete; re-inspects | `qc:plan-view`, `qc:plan-manage`, `qc:plan-approve`, `qc:reinspect` |
| Quality inspector (R-QCI) | reads inspections, enters values, submits | `qc:view`, `qc:inspect` |
| Supervisor | sees the Quality tab of an order, releases lots of items without a plan (interim) | `qc:view`, `qc:disposition` |

Identity script 036 gives all six new codes to Admin and SuperAdmin of every tenant; a shop gives them to other roles on the Roles screen. Reading a plan needs only `qc:plan-view`; the pickers on the profile and routing pages read the plan list with it.

## 3. Process
1. **Set up.** The quality manager creates a plan (code, type FQC or IPQC, item, sampling, tolerated minor defects), adds the characteristics and approves it. The item profile of a `RequiresFqc` item points at an FQC plan; an operation of a routing points at an IPQC plan.
2. **FQC.** When a finished-goods receipt line is **Posted** and its lot is on hold, the system creates a Pending FQC in the same save (BR-QC-02). The inspector opens it, measures the sample, types the values (saved as a draft), submits.
   * **Pass:** the lot moves to the finished-goods warehouse and becomes Available (the same two Warehouse calls as the interim release); the inspection records the release time.
   * **Fail:** the lot stays on hold in the quarantine warehouse; the inspection says *NCR required*; the order cannot be closed (quality-hold condition).
3. **IPQC.** When a report with Good > 0 is applied at an operation that has a plan, a Pending IPQC with `LotQty` = the Good quantity is created and that quantity is *held*: the next operation cannot take it. A Pass frees it (the next operation becomes Ready), a Fail keeps it held.
4. **Re-inspection.** A Failed inspection can be repeated once (`qc:reinspect`): a new Pending document that points back at it. Its Pass does what a Pass does. A Passed inspection is final.

## 4. Business rules
| Rule | Statement | Code |
|---|---|---|
| BR-QC-01 | A plan is a **series** (plant + code) in versions Draft -> Approved -> Obsolete; only an Approved version in force on the day is used; versions of a series must not overlap (the open one ends the day before the new one starts); a measurement has `Min <= Max` (one bound may be open); an attribute has no range; a plan needs at least one characteristic to be approved; an approved version cannot be edited. | `MFG_BOM_002`, `MFG_BOM_003`, `MFG_GEN_006` (code used twice), `MFG_VALIDATION` |
| BR-QC-02 | FQC is created with a Posted receipt line on hold, IPQC with a Good report at an operation with a plan - both inside the save that applies the receipt / report. No Approved plan in force = no inspection (the lot stays held and is released by hand; the report is not held). | - |
| BR-QC-03 | Sample size: Fixed `min(n, lot)`, Percent `ceil(lot x p / 100)` capped at the lot, Full = the lot. It is computed, never typed: a client that sends another figure is refused. | `MFG_QC_001` |
| BR-QC-04 | Results are kept per characteristic and per **sample unit** (a measurement needs one value for each unit of the sample, an attribute one pass / fail). A measurement passes when every value is in range. The lot passes when every critical characteristic passes and at most `AllowedMinorDefects` others fail. A missing value gives no verdict. | `MFG_QC_003` |
| BR-QC-05 | FQC Pass releases the lot; Fail keeps it on hold. IPQC Pass makes the Good quantity input of the next operation (net output = Good - held); Fail keeps it held. | `MFG_OP_004` (the next operation asks for more than passed) |
| BR-QC-06 | A submitted inspection is final. A Failed one is corrected by a re-inspection. | `MFG_QC_002` |
| BR-FG-07 (extended) | A receipt line whose newest inspection has a result (Passed or Failed) cannot be reversed; a Pending one is cancelled with the receipt. An interim-released lot still cannot be reversed. | `MFG_FG_004` |
| BR-OP-07 (extended) | A report whose IPQC has a result cannot be reversed; a Pending one is cancelled with the report and its hold is lifted. | `MFG_REV_003` |
| (new) | A receipt line that has an inspection is released through it: the interim button is refused (a *cancelled* inspection does not count). | `MFG_QC_004` |

## 5. Data
`InspectionPlans` (series code, type, item, version, status, sampling, `SampleValue`, `AllowedMinorDefects`, validity), `InspectionCharacteristics`, `Inspections` (document `QI-yymm-nnnnn` per plant, plan version used, order, operation + report or receipt document + line, lot, `LotQty`, `SampleQty`, status Pending / Passed / Failed / Cancelled, `ReinspectionOfId`, `ReinspectedById`, `NcrRequired`, `ReleasedAt`), `InspectionResults` (inspection, characteristic, `SampleNo`, measured value or attribute result, `IsPassed`); `ProductionOrderOperations.IpqcHeldQty` (SQL 012 / 013). The pointers `ItemManufacturingProfiles.FqcInspectionPlanId`, `RoutingOperations.IpqcInspectionPlanId` and `ProductionOrderOperations.IpqcInspectionPlanId` name the **series** by the id of any of its versions; the version in force on the day is resolved (`IInspectionPlanResolver`). A new order copies the pointer of its routing operation.

State of an inspection: `Pending --submit--> Passed | Failed`; `Pending --reversal of its source--> Cancelled`; `Failed --re-inspect--> (new Pending document)`.

## 6. Integration
* **Warehouse:** an FQC Pass calls `SubcontractTransfer` (only when the lot is not already in the order's finished-goods warehouse) and `QcRelease` with the keys `<receipt line key>Q1` and `Q2` - the same keys as the interim release, so both are idempotent against each other. The calls are made **before** the result is saved: a Warehouse outage answers `MFG_INT_001`, nothing is decided, the draft stays and the inspector submits again; the lot is released exactly once.
* **Events:** `InspectionCompleted` (Passed / Failed, with `NcrRequired`) on `manufacturing-topic`; no consumer yet.
* **Reports, reversals, close checklist:** see rules above; the close checklist's *quality hold* condition is unchanged (a Pass sets the release time, a Fail leaves the lot held).

## 7. Screens
S21 `/manufacturing/inspection-plans` (list with type / status filters; editor with header, characteristics, versions; approve / obsolete / copy / delete after a confirm). S22 `/manufacturing/inspections` (worklist, default Pending) and `/manufacturing/inspections/:id` (document: header, one value box per sample unit with an *in range / out of range* text, pass / fail choice for attributes, *Save draft*, *Submit* after a confirm that says what a pass does, *Re-inspect* for a failed one). Order detail: a **Quality** tab and, on the *Finished goods* tab, the inspection link of each receipt line instead of the interim release button. Item profile: plan picker (with a warning when none is linked). Routing operation: IPQC plan picker.

## 8. Use cases
**UC-QC-1 Final inspection, pass (main).** Receipt of 10 -> FQC Pending (sample 5) -> inspector types five lengths and the attribute results -> Submit -> Passed, lot Available in the finished-goods warehouse. *Alternative:* values saved as a draft first. *Exception:* wrong sample size (`MFG_QC_001`), a value missing (`MFG_QC_003`), Warehouse down (`MFG_INT_001`, submit again).
**UC-QC-2 Final inspection, fail.** A value out of range -> Failed, *NCR required*, lot stays on hold; the interim release and the receipt reversal are refused; the inspector or the manager re-inspects.
**UC-QC-3 In-process.** Good 60 at operation 10 -> IPQC Pending, operation 20 has no input; the first 50 pass -> operation 20 can take 50 (51 is `MFG_OP_004`).
**UC-QC-4 Plan-less item.** No plan resolves: the lot is received on hold with a hint *No inspection plan: release by hand* and the supervisor uses the interim release.

## 9. Assumptions and open issues
Decisions of the owner (2026-11-11): only a Failed inspection is re-inspected; a lot with a Failed FQC stays on hold until P4-4 (no reversal, no interim release). A plan approved **after** a receipt does not inspect that receipt (no back-fill); IPQC holds only reports created after the plan was approved. Risks are recorded as R279-R285 in `docs/DEFERRED-ISSUES.md`.

## 10. UAT scenarios
1. Create plan FQC-BAN01 (Fixed 5, one minor defect tolerated; C1 length 748-752 critical; C2 colour critical attribute; C3, C4 minor), approve it, link it on the TP-BAN01 profile.
2. Produce and receive 10 pieces: one Pending FQC, sample 5, lot on hold. Try the interim release: refused.
3. Enter C1 = 749, 750, 751, 752, 750.5 and pass C2-C4: Submit -> Passed, the lot is Available in the finished-goods warehouse (TC-QC-007).
4. Repeat with C1 = 753: Failed, NCR required, lot still on hold, receipt cannot be reversed (TC-QC-008).
5. Re-inspect the failed one and pass it: the lot is released (TC-QC-013).
6. Fail only C3: Passed; fail C3 and C4: Failed (TC-QC-009 / 010).
7. Give operation 10 an IPQC plan: Good 60 creates an IPQC; pass 50 of it and check that operation 20 refuses 51 (TC-QC-014 / 015).
8. Stop the Warehouse, submit a passing FQC: refused with the Warehouse message and nothing changes; start it and submit again: released once.

Live proof: `scripts/manufacturing-live/mfg-p43.js` on glcheck and SYSTEM (plans, FQC pass / fail / re-inspect / minor defects, reversal rules, Warehouse outage, IPQC TC-QC-014 / 015, report reversal rules) and the contract check `mfg-p36-contract-check.js` (shapes of plans and inspections).
