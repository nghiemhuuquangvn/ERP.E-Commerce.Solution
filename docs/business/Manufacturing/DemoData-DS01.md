# Manufacturing - demo and test data set DS-01 (M6)

Added 2026-11-07. Source: `docs/business/M/03_PRODUCTION_TEST_CASES.md` section 1 (data set DS-01, task T3.14.1). Code: `ERP.MFG.Application/Features/Seeding/Ds01Seeder.cs`, endpoint `POST /mfg/api/v1/manufacturing/seed/ds01`, script `scripts/seed-ds01-products.js`.

## 1. Purpose and actors
| Actor | Need |
|---|---|
| Developer / tester | The same numbers as the test-case book on a running stack (BOM x100 = 7 lines, paint 52.50, lead time 2003 minutes, start 2026-11-11) without typing 60 records by hand. |
| Business analyst / trainer | A realistic factory (a wooden-table plant) to demonstrate the screens and run the UAT scenarios of the four Manufacturing documents. |
| Administrator (production tenant) | Must **not** be able to create demo data by accident. |

## 2. What it creates (Phase 0-2 part of DS-01)
| Spec section | Created | Count |
|---|---|---|
| 1.1 plant and settings | plant `PL01` "Nha may Binh Duong" (3 warehouses given by the caller), tolerances 5 / 2 / 3 %, Actual costing, horizon 90 days, 20 levels | 1 |
| 1.2 shifts and calendar | CA1 06-14, CA2 14-22, CA3 22-06 (30 min break each); CAL-5D Mon-Fri CA1+CA2, Sat CA1, 2027-01-01 holiday; set as the plant default | 3 + 1 |
| 1.3 work centers and machines | WC-CUT, WC-ASM, WC-PNT (efficiency 80 %), WC-PKG with the rates of the book; M-CUT-01, M-CUT-02, M-PNT-01, M-OLD-01 (Retired) | 4 + 4 |
| (default) | the 11 default reason codes | 11 |
| 1.4 item profiles | the 12 items (make / buy, lead times, lot policy, safety stock, backflush) | 12 |
| 1.5 BOMs | TP-BAN01 (7 lines, KEO01/KEO02 alternative group G1, phantom KIT-PKG01), KIT-PKG01, BTP-MAT01 (with by-product sawdust), BTP-CHAN01 - all **Approved**, effective 2026-01-01 | 4 |
| 1.6 routings | TP-BAN01 (10 Assembly, 20 Painting, 30 Packing), BTP-MAT01 (10 Cutting) - Approved | 2 |

The BOMs and routings are written as CSV and created by the **import handlers**, then approved - so the seeder proves the import, the units, the cycle check and the approval on every run.

**Not created yet (answered in `notSeeded`):** opening stock and lots (1.7) - needs the Warehouse upgrade; the quality plan QP-BAN01 (1.8) - Phase 4; production order MO-A (1.9) - Phase 3.

## 3. How to run
1. Products (they belong to ProductService): `node scripts/seed-ds01-products.js --host <tenant host> --email <admin> --password <pw> --prefix ZZ-` creates the 12 products (and the unit `M3`, 3 decimals, when the tenant has none). The prefix keeps demo SKUs apart from real ones.
2. Switch the endpoint on: `MFG_DEMO_SEED=true` in `.env` (compose passes `Manufacturing__EnableDemoSeed`), redeploy `manufacturingservice`. **Default is off**; with the switch off the route answers 404. Never set it in production or k8s.
3. Add `--seed --raw <warehouse id> --fg <id> --qc <id>` to step 1 (or call the endpoint with `{plantCode, skuPrefix, warehouses{raw, finished, quarantine, scrap}, units?}`). Needs `manufacturing:settings:manage`.
4. The answer lists, per kind, how many were `created` and `skipped`. A second run creates nothing (idempotent: it finds by code and skips what exists; item profiles are an upsert).

## 4. Business rules
| ID | Rule |
|---|---|
| DS-R1 | Everything goes through the normal commands, so every business rule (MFG_*) applies; the seeder never writes rows directly. |
| DS-R2 | A missing or ambiguous product SKU stops the run before anything is written and names the SKUs. |
| DS-R3 | The plant needs the warehouse ids only when it does not exist yet. |
| DS-R4 | Unit codes default to PCS, M3, L, KG, SET; `units` overrides them for a tenant with other codes. |
| DS-R5 | The route exists only when `Manufacturing:EnableDemoSeed` is true; it also needs the settings-manage permission. |

## 5. Acceptance (automated and live)
- Unit tests `Ds01SeederTests`: counts, idempotence, 7 lines and paint 52.50 from the seeded BOM, missing product named and nothing created.
- Live (glcheck, `mfg-m6.js`): first run creates 1 / 3 / 1 / 4 / 4 / 12 / 4 / 2, second run 0 new; explosion x100 = MAT 100, CHAN 400, VIT 1600, SON 52.5, KEO1 10, THUNG 100, XOP 200 (no phantom, no KEO02); BTP-MAT01 x50 = wood 1.1 m3 + sawdust 25 kg; low-level codes of TC-BOM-019; lead time 2003, start 2026-11-11; month view 900 / 450 / 0; capacity Monday WC-CUT 1620, WC-PNT 720.

## 6. Open issues
The set is for development and training, not for production; stock, lots, QC plan and orders follow with their phases; the units are the tenant's MasterData units (the seeder does not create them - the script does for M3).
