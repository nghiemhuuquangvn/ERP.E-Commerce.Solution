# Manufacturing (Production) - business documentation index

ManufacturingService (port 5018, code MFG, PostgreSQL `ManufacturingDB`). Specs received 2026-11-06: `docs/business/M/` (BA spec, dev tasks, test cases). Build log: `docs/PROJECT-STATUS.md`; architecture decision: `docs/architecture/adr-manufacturing-service.md`; Admin guide: `docs/frontend/manufacturing-admin-guide.md`.

| Document | Area | Milestone |
|---|---|---|
| `00_Mapping-to-ERP.md` | how every spec item maps to this ERP (names, permissions, ids, tests) | M0-M6 |
| `PlantSettingsItemProfile.md` | plants, production settings, item profiles, plant access | M1, screens M5 |
| `CalendarsWorkCenters.md` | shifts, calendars, work-time calculator, work centers, machines, standard operations, reason codes, crews | M2, screens M5 |
| `Bom.md` | bills of materials: versions, approval, cycle check, explosion, where-used, compare, CSV import | M3-M4, screens M5 |
| `Routing.md` | routings, operation times, lead time, BOM/routing warnings, CSV import | M4, screens M5 |
| `DemoData-DS01.md` | the demo / test data set and how to load it | M6 |

## Phase 0-2 acceptance (test-case groups)
| Group | Status |
|---|---|
| TC-GEN (rules, audit, delete, concurrency 409) | done; concurrency 409 mapped in M6 (a parallel approval used to answer 500); document numbers (TC-GEN-006) come with Phase 3 orders |
| TC-SEC (permissions, plant visibility, tenant) | done for Phase 0-2 endpoints; second-tenant live check still open (R186) |
| TC-SET, PLT, ITM | done |
| TC-CAL, WC, MCH, OPS, RSN, LBR | done (crew rates resolve; used by costing in Phase 6) |
| TC-BOM, TC-RT | done, including CSV import (TC-BOM-053) |
| TC-INT-001/003 | done (UoM conversion, fail-closed clients) |
| TC-MO, MI, OP, FG, QC, MRP, COST, KPI, E2E | not started: Phase 3-7, after the Warehouse upgrade |

## What Phase 3 needs first
The Warehouse must accept decimal quantities, keep stock per item / warehouse / lot with a status (available, QC hold), have production movement types, reservations by source with consume, a FEFO suggestion and an idempotent internal write API that returns the unit cost. That is its own epic with its own plan.
