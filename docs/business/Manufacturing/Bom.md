# Manufacturing - Bills of materials (M3)

> Service: ManufacturingService (MFG). Source specs: `docs/business/M/01_PRODUCTION_BA_SPEC.md` §5.8 (BR-BOM-01..15), §7.2, error codes `PRD_BOM_*` (here `MFG_BOM_*`); tasks T2.1-T2.4 of `02_PRODUCTION_DEV_TASKS.md`; tests TC-BOM-001..021, 030..040, 050, 051, TC-INT-001 of `03_PRODUCTION_TEST_CASES.md`.
> Differences from the specs: `00_Mapping-to-ERP.md`. Previous milestone: `CalendarsWorkCenters.md`. Status: built and checked live 2026-11-06 (milestone M3, 46 of 46 live checks). Screens are milestone M5; routing and the BOM / routing check (BR-RT-08) are M4; CSV import (T2.9) is M4.

## 1. Purpose and scope
A bill of materials (BOM) says what one `BaseQuantity` of an item is made of: components, with quantity, unit, scrap, the operation that consumes them and how they are issued, plus the by-products and co-products the work yields.
Production orders (Phase 3) will copy it, planning (Phase 5) will explode it, costing (Phase 6) will roll it up. This milestone delivers the **BOM as master data with versions and approval**, and the **arithmetic** that turns a BOM and an order quantity into material requirements:
single-level explosion with phantoms, the indented multi-level tree, where-used, version comparison, cycle detection, low-level codes.
Not in this milestone: routing (M4), screens (M5), production orders, MRP and costing (later).

## 2. Actors
| Actor | What they do | Permission |
|---|---|---|
| Production engineer | Creates BOMs and versions, edits drafts, copies a version to change it | `manufacturing:bom:manage` |
| Plant manager / approver | Approves a version, makes a version obsolete | `manufacturing:bom:approve` |
| Planner, buyer, cost accountant | Reads BOMs, explosions, where-used, comparisons | `manufacturing:bom:view` |
| Order creation, MRP, costing (system, later) | Ask "which BOM applies on this date" and for the materials of a quantity | in-process services |
| ProductService (system) | Says whether a component exists, is active, its base unit and its own unit conversions | service token `manufacturing` |
| MasterData (system) | Says the decimals of a unit and how units of one family convert | service token `manufacturing` |

## 3. Process
1. Pick the item (a made item, or one with no profile yet) and **create a BOM**: base quantity, yield %, validity from / to. It is version 1, **Draft**.
2. **Add lines**: component (active product), quantity, unit, scrap %, fixed quantity or not, phantom or not, issue method (manual / backflush), operation sequence, alternative group and priority; by-products and co-products with their cost share.
3. **Approve** (needs the approve permission). The system checks the structure, the overlap with other versions (closing the open one the day before), cycles over all approved BOMs, the depth, and rewrites the low-level codes - all in one save.
4. To **change** an approved BOM: **copy** it. The copy is the next version, a Draft starting on a date you choose; edit it and approve it; the older version ends the day before.
5. When a version is no longer to be used: **make it obsolete**. Its end date can still be moved.
6. Ask: **explosion** of a quantity (the materials and outputs of an order), **indented tree**, **where-used** of an item, **comparison** of two versions, **low-level codes**.

## 4. Requirements and acceptance criteria
| ID | Requirement | Acceptance criteria |
|---|---|---|
| M3-01 | Only a made item has a BOM (BR-BOM-01) | A BOM for an item whose profile says Buy is refused `MFG_BOM_001`; an item with no profile counts as made; a component must be active (`MFG_BOM_001`) |
| M3-02 | Draft -> Approved -> Obsolete, approved versions never change (BR-BOM-02) | Adding, changing, removing a line or deleting an approved / obsolete BOM is refused `MFG_BOM_002`; a Draft can be edited and deleted; approval records who and when; approving needs `bom:approve` (HTTP 403 otherwise) |
| M3-03 | Validity periods of approved versions never overlap (BR-BOM-03) | Version 1 open-ended from 2026-01-01, version 2 approved from 2026-12-01 -> version 1 ends 2026-11-30. Version 1 closed 2026-12-31, a version from 2026-06-01 -> refused `MFG_BOM_003` naming version 1 and nothing changes. A version that starts before the open one is an overlap |
| M3-04 | The version that applies on a date (BR-BOM-03) | On 2026-11-30 version 1; on 2026-12-01 version 2; before any version -> none (`MFG_PO_004` when an order asks) |
| M3-05 | No cycles (BR-BOM-04) | An item as its own component is refused at the line. A cycle over levels is refused at approval, with its path `BTP-MAT01 > TP-BAN01 > BTP-MAT01`; the refused approval changes nothing |
| M3-06 | Quantity rules (BR-BOM-05) | Quantity > 0 with at most 6 decimals; base quantity > 0; scrap 0-99.99; yield above 0 and at most 100; otherwise `MFG_BOM_005` |
| M3-07 | Units (BR-BOM-07) | A line may be in any unit that converts to the component's base unit: the same unit, the product's own conversion (1 box = 100 pieces) or a unit of the same family in MasterData (grams for kilograms). Paint counted in pieces -> `MFG_BOM_006` naming both units and the item |
| M3-08 | Co-products (BR-BOM-09) | The cost shares of the co-products add up to less than 100 % (`MFG_BOM_007`); the main product takes the rest. By-products take no share |
| M3-09 | Alternatives (BR-BOM-11) | Lines of one group substitute each other; each group has exactly one priority-1 line (`MFG_BOM_009`, checked at approval); only priority 1 counts in requirements; a group needs priorities and a priority needs a group |
| M3-10 | One component once per operation (BR-BOM-12) | The same component twice at operation 10 -> `MFG_BOM_010`; at operation 10 and 30 -> allowed |
| M3-11 | Requirements (BR-BOM-06) | `Req = Qty / Base x Line x (1 + scrap%) / (yield%)`, a fixed-quantity line `Line x (1 + scrap%)`; converted to the component's base unit and **rounded up** to that unit's decimals. 100 tables: 7 lines, paint 52.50; 7 tables: paint 3.675 (3.68 where the unit has 2 decimals), glue 0.70 |
| M3-12 | Phantoms (BR-BOM-08) | A phantom line is replaced by its own components multiplied through, at the phantom line's operation; no line for the phantom itself; a phantom with no effective BOM -> `MFG_BOM_011` |
| M3-13 | Outputs | By-products and co-products are listed with a planned quantity without scrap: 50 tops -> 25 kg sawdust |
| M3-14 | Depth (BR-BOM-10) | A tree deeper than `MaxBomLevels` of the plant -> `MFG_BOM_008`, at approval and in explosions |
| M3-15 | Low-level codes (BR-BOM-13) | After approval / obsolescence every item is at the deepest level it occurs in any approved BOM (top item 0 ... wood 2); written to the item profiles of the plant; the query `low-level-codes` answers for items without a profile too |
| M3-16 | Copy (BR-BOM-15) | The copy is a Draft, version = highest ever used + 1 (a deleted draft's number is not reused), all lines copied |
| M3-17 | Where-used and compare (T2.4) | Where-used of wood: the two sub-assemblies at level 1, the table at level 2 (each parent once, at its lowest level). Compare two versions: added / removed / changed lines (quantity 16 -> 18 shows as one change) |
| M3-18 | Plant visibility (BR-GEN-09) | A user who may not see the plant gets 404 for its BOMs |

## 5. Business rules
All rules of BA spec 01 §5.8 apply under the codes `MFG_BOM_001..011` (Vietnamese messages in `MfgErrors`). Decisions where the spec is silent or ambiguous:
| Decision | Why |
|---|---|
| The **line's** phantom flag decides; there is no check against the item | The spec has both an item-level and a line-level phantom; the line is what the explosion needs |
| A phantom's quantity is **not rounded** between levels; only real components are rounded | A phantom is never stocked or made |
| By-product / co-product planned quantity: `Qty / Base x Line x factor`, no scrap, no yield | "without scrap" in BR-PO-05; yield describes the input side |
| Depth counts BOM levels: the top BOM is level 1, a phantom's BOM is a level | Matches the DS-01 low-level codes (THUNG, XOP = 2) |
| Cycle check uses **all** approved versions plus the one being approved | The spec says "approved set + the one being approved" |
| Line numbers go 10, 20, 30 ... and are never reused inside a BOM | Stable references for orders |
| The indented tree shows alternatives that are not priority 1 marked `isSubstitute`, unexpanded | The screen S08 marks the group |
| Approving also keeps the older approved versions in the cycle graph even when they ended | Conservative; see R170 |

## 6. Use cases
**UC-1 Create and approve a BOM (main).** Engineer creates the BOM for TP-BAN01, adds 7 lines, approver approves. Result: version 1 Approved, low-level codes written.
- *Alternative:* the engineer explodes the Draft (by its id) to check the numbers before asking for approval.
- *Exception:* an alternative group without a priority-1 line -> approval refused `MFG_BOM_009`; nothing changes. A line in pieces for paint -> refused `MFG_BOM_006` at the line.
**UC-2 Change an approved BOM.** Engineer copies version 1 starting 2026-12-01, changes screws 16 -> 18, adds a line; approver approves. Version 1 ends 2026-11-30. *Exception:* start date inside the closed period of version 1 -> refused `MFG_BOM_003`, the copy stays a Draft and can be deleted.
**UC-3 Prevent a cycle.** Engineer adds the table to version 2 of the table top. Approval -> refused with the path `BTP-MAT01 > TP-BAN01 > BTP-MAT01`.
**UC-4 Material requirements.** Planner asks the explosion for 100 tables on 2026-11-04 and gets the seven lines and the outputs. *Exception:* no BOM effective that day -> `MFG_PO_004`.
**UC-5 Retire.** Approver makes version 2 obsolete ending 2026-12-31; later moves the end to 2026-12-15. Low-level codes are rewritten.
**UC-6 Analyse.** Where-used of an item before changing it; compare version 1 and 2 before approving 2.

## 7. Data
`Boms` (item, plant, version unique per item and plant, base quantity, yield, status 1 Draft / 2 Approved / 3 Obsolete, effective from / to, approved by / at, item code and name snapshot) and `BomLines` (line number unique per BOM, type 1 Component / 2 ByProduct / 3 CoProduct, component id with code and name snapshot, quantity, unit, scrap %, fixed, phantom, issue method, operation sequence, alternative group and priority, cost share, notes). SQL `src/Database/Manufacturing/004_Boms.sql` (idempotent; run twice live). Tenant isolation as everywhere; optimistic locking through `xmin`.
State: Draft -> Approved -> Obsolete (no way back; a change is a new version).

## 8. Integrations
| Call | Purpose |
|---|---|
| ProductService `internal/v1/product-lookup` (M1) | component exists, is Active, base unit; the parent exists |
| ProductService `internal/v1/product-uom-conversions/lookup` (new) | the product's own conversions (`ProductUomConversion`: 1 From = factor To) in either direction |
| MasterData `internal/v1/units-of-measure/lookup` (M1) | decimals of the base unit; base unit and factor to convert units of one family |
All calls fail closed (`MFG_INT_001`, never an empty answer), are batched (200 ids per call; one call per service for a whole tree) and cached 60 s per tenant. Events: none yet (BOM approved events come with orders).

## 9. Permissions
`manufacturing:bom:view` (all reads), `manufacturing:bom:manage` (create, edit lines, delete a draft, copy), `manufacturing:bom:approve` (approve, obsolete). The three codes exist since M0 (`PermissionConstants`).

## 10. API (`/mfg/api/v1/manufacturing/boms`)
`GET ?plantId&itemId&status`, `GET {id}`, `GET effective?plantId&itemId&date`, `POST`, `PUT {id}` (header), `DELETE {id}` (draft), `POST {id}/lines`, `PUT|DELETE {id}/lines/{lineNo}`, `POST {id}/approve`, `POST {id}/obsolete`, `POST {id}/copy`,
`GET explode?plantId&itemId&quantity&bomId&date`, `GET indented?...`, `GET where-used?plantId&itemId`, `GET compare?left&right`, `GET low-level-codes?plantId`. Refusals carry `errorCode` (`MFG_BOM_*`).

## 11. Reports
No report yet; the explosion and where-used answers are the data for the planner's screens (S08) and for MRP / costing later.

## 12. Assumptions and open issues
Open issues: `docs/DEFERRED-ISSUES.md` R167-R172: no concurrency test of two parallel approvals, per-product conversion needs a product-service round trip per tree, low-level code stored only on existing profiles, stale ended versions in the cycle graph, snapshot names go stale, BR-RT-08 not yet checked.
Assumption: units of the same family convert exactly (`factor(from) / factor(to)`); the unit decimals are those configured in MasterData (R155).

## 13. UAT scenarios
1. Create BOM for the table (7 lines of DS-01). Explode 100 -> 7 lines: top parts 100 / 400 / 1600, paint 52.50, glue A 10.00 (not glue B), carton 100 and foam 200 (from the phantom kit at operation 30).
2. Explode 7 -> paint 3.675 (3.68 with 2 decimals), glue 0.70.
3. Approve; try to add a line -> refused. Copy for 2026-12-01, change screws to 18, add a line, approve -> version 1 ends 2026-11-30; the effective BOM on 11-30 is version 1, on 12-01 version 2. Compare shows one change and one addition.
4. Copy version 1 from 2026-06-01 and approve while version 1 is closed -> refused naming version 1.
5. Add the table as a component of its own sub-assembly and approve -> refused with the cycle path.
6. A line of paint in pieces -> refused. A line of 1 box of screws -> accepted (1 box = 100 pieces); grams of glue accepted.
7. Open low-level codes: table 0, parts 1, wood / carton / foam 2. Make the table's BOM obsolete -> parts drop to 0.
8. Where-used of wood: two sub-assemblies, then the table. Indented tree for 1 table: wood 0.022 under both.
9. A user without `bom:approve` presses Approve -> 403. A user who may not see the plant -> 404.
