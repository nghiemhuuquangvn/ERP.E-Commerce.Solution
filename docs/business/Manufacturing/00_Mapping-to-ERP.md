# Manufacturing - how the specs map to this ERP

The three specs in `docs/business/M/` were written for a modular monolith. This file lists every place where the implementation differs from the text,
so the specs stay readable as they are. Decision record: `docs/architecture/adr-manufacturing-service.md`.

| Spec says | Implemented as |
|---|---|
| Module code `PRD`, schema `production`, namespace `ERP.E_Commerce.*.Production` | Service `ManufacturingService`, code **MFG**, PostgreSQL database `ManufacturingDB` (tables in `public`), namespaces `ERP.MFG.*` |
| Error codes `PRD_GEN_001` ... | `MFG_GEN_001` ... (same numbers, same Vietnamese messages; `MfgErrors`) |
| Permissions `Production.Bom.Approve` ... | `manufacturing:bom:approve` ... (`PermissionConstants.Manufacturing_*`); roles R-xxx map to tenant roles that hold these permissions |
| `bigint` identity ids | `Guid` ids + `TenantId` on every row |
| Plant code unique "toàn hệ thống" (BR-PLT-01) | unique per tenant |
| One DB transaction with Inventory (BR-MAT-06, NFR-05) | Pending document + idempotent call to Warehouse + retry/reversal (ADR decision 5) |
| `IDocumentNumberService`, `IPlantContext`, `ICurrentUser` as shared infrastructure | Own `DocumentNumberService` (atomic upsert), `PlantUserAccess` (Phase 0 M1), `UserContext` of the platform |
| Interfaces §8 (`IItemQueryService`, `IUomConversionService`, `IInventory*`, ...) | Internal REST clients in `ERP.MFG.Infrastructure` against `internal/v1/...` endpoints of Product, MasterData, Warehouse, Sales, Purchasing; events towards Accounting |
| xUnit + NSubstitute + Testcontainers | xUnit + Moq + EF InMemory (repository standard); PostgreSQL behaviour is checked in the live run |
| Test names `TC_PO_031_...` | Kept: `TC_<GROUP>_<nnn>_<Description>` |
| Error answer with `ErrorCode` (BR-GEN-07) | `errorCode` field next to `detail` in the shared problem answer for every business-rule refusal |
| Item facts (`IItemQueryService`: lot / serial controlled, make / buy) | Make / buy lives in Manufacturing (`ItemManufacturingProfile`); active + base unit come from Product `internal/v1/product-lookup`; lot / serial flags do not exist in Product yet (Phase 3 prerequisite) |
| `IUomConversionService.GetDecimalPlacesAsync` | MasterData `UnitsOfMeasure.DecimalPlaces` + `internal/v1/units-of-measure/lookup`; `ConvertAsync` = `internal/v1/units-of-measure/convert` |
| `IEmployeeQuery` (HR) | MasterData `internal/v1/employees/lookup` (employees are MasterData's, HRM only keeps a snapshot) |
| `WorkingTimeService` cached by month (T1.1.3) | cached per calendar for 60 s per tenant (a calendar is small); month data come from the same calculator |
| Reason code seed (T1.5.1) as migration seed | written per tenant on first read / `seed-defaults` (a SQL seed cannot know the tenants) |
| `IUomConversionService` (T2.1.4) | pure `UomRules` over MasterData units (`internal/v1/units-of-measure/lookup`) and the product's own conversions (`internal/v1/product-uom-conversions/lookup`); one batched read per tree |
| `IBomCycleDetector`, `ILowLevelCodeCalculator`, `IBomExplosionService` (T2.2-T2.3) | pure `BomGraph` / `BomExplosionCalculator` in the Domain plus `BomExplosionService` loading the approved versions in one read |
| Approval in one DB transaction | one `SaveChanges`: status, closing of the earlier version and the low-level codes of the plant |
| `IOperationTimeCalculator` (T2.6.1), `GetRoutingLeadTime` (T2.6.2) | pure `OperationTimeCalculator` and `RoutingLeadTimeCalculator` in the Domain, loaded with work centers and calendars by `OperationTimeService`; lead time is scheduled backwards from the end of a day |
| BR-RT-08 shared function (T2.7.1) | pure `BomRoutingCheck.Warnings` (BOM and routing approval) and `AssignOperation` (orders, Phase 3) |
| Excel import (T2.9.1) | **CSV** (UTF-8) instead of Excel, all-or-nothing, reusing `CsvImportReader` of `ERP.Shared.Streaming` for parsing; not streamed (a document is small) |
| One versioning rule for BOM and routing | generic `DocumentVersioning` over `IVersionedDocument` |
| Screens S01-S09 (T1.7, T2.8) | routes `/manufacturing/...`: S01 plants, S02 settings, S03 item profiles, S04 calendars, S05 work centers, S06 catalogs, S08 BOM (tree), S09 routing (drag and drop); S07 (reason codes / crews) is a tab of S06; drag-and-drop is native HTML5 with arrow buttons |
| Seeder `ProductionTestDataSeeder` (T3.14.1) | `SeedDs01Handler` (Phase 0-2 part only) behind `POST seed/ds01`, switch `Manufacturing:EnableDemoSeed` (off by default); products by `scripts/seed-ds01-products.js`; see `DemoData-DS01.md` |
| Concurrency 409 (BR-GEN-03, `PRD_GEN_001`) | `MfgUnitOfWork` turns `DbUpdateConcurrencyException` (xmin) and a unique-key race into `ConcurrencyException` = HTTP 409 with the MFG_GEN_001 text |

## Phase 3 - production orders (P3-1, 2026-11-10)
| Spec says | Implemented as |
|---|---|
| Permissions `Production.Order.View` / `Create` / `Release` / `Cancel` / `Hold` / `ChangeQty` / `ShortClose` / `Reopen` / `Close` | `manufacturing:order:view` / `create` / `release` / `cancel` / `hold` / `change-qty` / `short-close` / `reopen` / `close` |
| `Production.Material.Issue` / `Return` / `OverIssue`, `Production.Operation.Execute` / `CompleteManual`, `Production.Receipt.Post`, `Production.Qc.Disposition` | `manufacturing:material:issue` / `return` / `over-issue`, `manufacturing:operation:execute` / `complete-manual`, `manufacturing:receipt:post`, `manufacturing:qc:disposition` (interim use: release of received goods from quality hold); new `manufacturing:stock-document:retry` (Identity 034) |
| Document number sequence per type + plant + month (BR-GEN-04) | For orders per **tenant** + month (plant = empty): the number is the idempotency key in the Warehouse (`<DocNo>#<line>`), so it must be unique in the tenant |
| Material issue method: BOM line -> item profile -> plant default (T3.2.2) | The BOM line's value (a line always carries Manual or Backflush, there is no "inherit") |
| `PlannedStart` = start of the first operation; BOM / routing effective at `PlannedStart` (BR-PO-04) | Reference date = plant today for a new order, the old plan start (if still ahead) when a Draft is regenerated; a version can always be chosen by id |
| Serial-controlled items (BR-MAT-04, BR-FG-03) | Refused with new code `MFG_MAT_011` until the Warehouse supports serials in production (R206) |
| `IOpenOrderGuard` stub (M1/M2) | `OrderBackedOpenOrderGuard`: Released, InProgress, OnHold, Completed count as open |
| Domain events "handled in the same transaction" | Outbox in the same transaction, Kafka topic `manufacturing-topic` |
| Reservation made in the same transaction as the release (NFR-05) | Order saved with a sync flag, then the Warehouse reservation is brought in line (reconciler + `ReservationSyncJob`); the answer says `reservationPending` when it is not yet done (P3-2) |
| Reserved quantity per material = min(required, available) (BR-PO-08) | min(still needed, allocatable) where allocatable counts lot-less stock and Available, unexpired lots of the material's warehouse (capped by the item's available quantity) |
| Change of quantity keeps the reservation | Release + reserve again (the Warehouse has no "adjust") |
| Issue / return atomic with the inventory transaction in one DB transaction (BR-MAT-06, NFR-05, T3.5.3) | Pending-document engine: stored first, one Warehouse movement per line under `<DocNo>#<n>`, effect on the order only when all lines are Posted, reversal on refusal, retry job; 201 Posted / 202 Pending (P3-3) |
| Document numbers MI / MR per plant and month (BR-GEN-04) | Per **tenant** and month: the number is the Warehouse key |
| `ProductionOrderMaterial.RegisterIssue` checks BR-MAT-02 (T3.5.1) | The check is in the handler (`MaterialLimits`) before the document is stored; pending quantities count; the order only registers posted quantities |
| Serial numbers on issue lines (MaterialTransactionSerial, TC-MAT-007 serial variant) | Serial-controlled items are refused (`MFG_MAT_011`); no serial table |
| Return lot limit "<= quantity issued from that lot" (BR-MAT-05) | As specified when the lot of the issue is known; issues drawn by the reservation from several lots have no recorded lot, so they widen every lot's limit |
| Posting date chosen by the user, open cost period (BR-GEN-08) | Always plant today; no period lock until Phase 6 |
| New codes | `MFG_MAT_012` (reservation being updated), `MFG_GEN_007` (Idempotency-Key reused for another request) |
| T3.1 / T3.2 / T3.3 / T3.11 | P3-1 (done); T3.4 / T3.9 P3-2 (done); T3.5 / T3.6 P3-3 (done, 2026-11-10); T3.7 + T4.1 backflush P3-4; T3.8 / T3.10 P3-5; T3.12 P3-6; T3.14 P3-7 |
