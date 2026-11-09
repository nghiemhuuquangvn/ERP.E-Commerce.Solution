# Warehouse upgrade epic - overview, traceability and acceptance (W0-W6)

Written 2026-11-10 at the end of W6. Decision record: `docs/architecture/adr-warehouse-upgrade.md`. Reason: Manufacturing Phase 3+ (issue, return, receipt, backflush, quality hold, genealogy) needs stock per (item, warehouse, lot) in decimals, production movements, reservations by source and an idempotent write API (`docs/business/M/01_PRODUCTION_BA_SPEC.md` BR-MAT-04/06/08, NFR-05).

## 1. What the epic delivered
| Milestone | Delivered | Document | SQL |
|---|---|---|---|
| W0 | ADR: StockBalance next to Inventory, all quantities decimal, ledger as single writer | adr-warehouse-upgrade.md | - |
| W1 | Decimal(18,6) quantities end to end; consumers widened first, events carry decimals | DecimalQuantities.md | Warehouse 010 (+ Product, Purchasing, Reporting scripts) |
| W2 | `StockBalance` per (item, warehouse, lot), `StockLedger` as the only writer, RowVersion 409, default warehouse, reconciliation, lot status, Product lot / serial flags | StockByWarehouseAndLot.md | Warehouse 011, Product 020 |
| W3 | Production movement types 7-12, unit cost / value / delta on every movement, unique idempotency index for production references | ProductionMovements.md | Warehouse 012 |
| W4 | Reservations by source (sales / production order) in one warehouse, partial consume, first-expiry-first `LotPicker` + suggestion, `SellStock` no longer eats reservations | ReservationsBySource.md | Warehouse 013 |
| W5 | `internal/v1/stock/*` for Manufacturing: key `<doc>#<line>`, replay vs conflict, lot / serial flags, lot status, idempotent release; `IWarehouseStockClient`; dev-only cycle runner | ProductionIntegration.md | - |
| W6 | DS-01 opening stock (`seed-ds01-products.js --stock`), expired-lot rule, BOM import unit pick, wrap-up documents, full regression | this document, DemoData-DS01.md | - |

## 2. The stock model on one page
- **`Inventory`** (key ProductId) = the per-item total Sales, POS and Replenishment read: Total / Reserved / Locked / Available, average cost. **`StockBalance`** (item, warehouse, lot) = where it is. Σ balances = the item total (`GET inventory/reconciliation` lists drift).
- **`IStockLedger`** is the only code that changes Inventory, lots, bin stock and balances (`NoStockMutationOutsideTheLedgerTests`); handlers keep their movement, events, idempotency and the single `SaveChanges`. A concurrent writer gets HTTP 409 and retries.
- **Lots**: warehouse, status (Available / QcHold / Blocked), expiry, manufacture date, source. Only Available, unexpired lots are allocated (`LotPicker`, the one first-expiry-first rule).
- **Movements**: types 0-6 (older) and 7-12 (production). Every movement stores warehouse, lot, unit cost, value and stock delta. Production references `<doc>#<line>` are unique per tenant, product and type.
- **Reservations**: source (SalesOrder / ProductionOrder); a production order holds components in one warehouse and takes them in pieces; release frees only the rest.
- **Internal API** for Manufacturing; **Accounting books none of the production movements** until Manufacturing phase 6 (WIP posting).

## 3. Traceability
| Requirement group | Milestone | Document | Unit tests | Live script |
|---|---|---|---|---|
| WQ-1..6 decimal quantities | W1 | DecimalQuantities.md | `DecimalQuantityTests` | wh-w1.js |
| WB-1..8 balances, lots, default warehouse | W2 | StockByWarehouseAndLot.md | `StockLedgerTests` | wh-w2.js |
| WM-1..8 production movements, cost, idempotency | W3 | ProductionMovements.md | `ProductionMovementTests`, Accounting `StockMovementPostingTests` | wh-w3.js |
| RS-1..10 reservations by source, partial consume, suggestion | W4 | ReservationsBySource.md | `ReservationBySourceTests` | wh-w4.js |
| PI-1..12 internal API, key, flags, lot status | W5 | ProductionIntegration.md | `ProductionIntegrationTests` (Warehouse), `WarehouseStockTests` (Manufacturing) | wh-w5.js |
| PI-R10 expired lot; DS-01 stock 1.7 | W6 | ProductionIntegration.md, DemoData-DS01.md | `ProductionIntegrationTests` (expired lot), `ImportTests` (unit pick) | wh-w6.js |
| Architecture rules (ledger only writer, internal endpoints, refusals, migrations registered) | W2-W5 | - | `ERP.Architecture.Tests` | - |

## 4. Epic acceptance
| Goal of the ADR | Status |
|---|---|
| Decimal quantities across Warehouse and its consumers | Done (W1); POS and sales order lines stay whole numbers by design |
| Stock per item, warehouse and lot with one writer | Done (W2) |
| Production movement types with cost and real idempotency | Done (W3); legacy types keep a pre-check (750 duplicate groups) |
| Reservations by source, partial consume, first-expiry-first | Done (W4) |
| Idempotent internal write API with unit-cost answers | Done (W5) |
| Lot / serial enforcement | Lot: done (flag passed by the caller). Serial: refused on the production path (deferred: separate serial epic, R206) |
| DS-01 stock for the Manufacturing test book | Done (W6) with assumed costs (A-1) |
| Repair of the old Reserved drift | Deferred - owner decision (R201) |
| Accounting of production movements | Deferred to Manufacturing phase 6 |

## 5. Behaviour changes to remember
Manual sale and vendor return need *available* stock; expired lots are not allocated to sales and not issued to production (`WH_LOT_EXPIRED`); a stock write that races another gets 409; the same production reference with another payload is 409 `WH_IDEMPOTENCY_CONFLICT`; `ReleaseReservation` is idempotent.

## 6. Rollout notes
Consumers (Product, Purchasing, Sales, Reporting) widen to decimals and deploy **before** the Warehouse. SQL order: Warehouse 010, 011, 012, 013 (all idempotent, registered in `RunAllScripts.ps1`; rehearsed on a restored copy before the live run); Product 020 for the flags. Every environment needs the scripts; 010 alters large tables (lock time). `Manufacturing:EnableDemoSeed` stays off outside development.

## 7. Final regression (2026-11-10)
Unit and architecture tests (sequential, one project at a time):

| Project | Passed | Result |
|---|---|---|
| ERP.ACC.UnitTests | 602 | passed |
| ERP.Architecture.Tests | 84 | passed |
| ERP.CRM.UnitTests | 17 | passed |
| ERP.Gateway.UnitTests | 44 | passed |
| ERP.HRM.UnitTests | 50 | passed |
| ERP.Identity.UnitTests | 289 | passed |
| ERP.MFG.UnitTests | 280 | passed |
| ERP.MST.UnitTests | 33 | passed |
| ERP.NTF.UnitTests | 85 | passed |
| ERP.PAY.UnitTests | 99 | passed |
| ERP.PO.UnitTests | 107 | passed |
| ERP.POS.UnitTests | 27 | passed |
| ERP.PRD.UnitTests | 68 | passed |
| ERP.PRM.UnitTests | 17 | passed |
| ERP.RPT.UnitTests | 22 | passed |
| ERP.SHP.UnitTests | 176 | passed |
| ERP.SO.UnitTests | 142 | passed |
| ERP.Shared.UnitTests | 151 | passed |
| ERP.WH.UnitTests | 132 | passed |

Total 2425 .NET tests. `LiveSchemaDriftTests` (`ERP_DRIFT_SQL`): see the PROJECT-STATUS entry. Admin: `npm test` 371 passed (31 files; jsdom prints "canvas not implemented" from axe, no failure), `check:tokens` ok, `tsc -b` clean. Storefront 46, mobile customer 72, staff 64, admin 248 passed.

Live scripts (SYSTEM = admin.erp.local, glcheck = glcheck.erp.local; the order saga script wh-w1 runs on glcheck, where the catalogue and customer exist):

| Run | Result |
|---|---|
| glcheck wh-w2 | ALL OK |
| glcheck wh-w3 | ALL OK |
| glcheck wh-w4 | ALL OK |
| glcheck wh-w5 | ALL OK |
| glcheck wh-w6 | ALL OK |
| SYSTEM wh-w2 | ALL OK |
| SYSTEM wh-w3 | ALL OK |
| SYSTEM wh-w4 | ALL OK |
| SYSTEM wh-w5 | ALL OK |
| SYSTEM wh-w6 | ALL OK |
| glcheck wh-w1 post (order saga, POS, PO receipt, adjustment, transfer, stocktake) | ALL OK |
| glcheck mfg-m4 | ALL OK |
| glcheck mfg-m5 | ALL OK |
| glcheck mfg-m6 | ALL OK |

## 8. Open decisions for the owner
Cleaning the throw-away `ZZ-*` data on SYSTEM and glcheck (R186-R208 lists them); the ~1 000 seed `ReservedQuantity` values (R201); confirming the assumed opening costs (A-1); whether serial tracking in production becomes an epic (R206); k8s `postgres.yaml`.
