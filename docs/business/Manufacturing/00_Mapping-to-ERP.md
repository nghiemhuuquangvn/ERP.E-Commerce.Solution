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
