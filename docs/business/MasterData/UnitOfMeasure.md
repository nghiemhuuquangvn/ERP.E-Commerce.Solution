# Unit of Measure (UoM)

**Service:** SharedMasterDataService (Port 5015, Code: MST)
**Database:** MasterDataDB

## Purpose

Shared measurement units (weight, volume, length, count, area) for use by any service
that needs to express product/inventory quantities consistently (e.g. ProductService,
WareHouseService, PurchaseOrderService). Supports derived units via `BaseUnitId` +
`ConversionFactor` (e.g. "g" derived from "kg" with factor 0.001).

## Fields

| Field | Type | Notes |
|-------|------|-------|
| Code | string | e.g. "KG", "PCS" — unique per tenant |
| Name | string | Default-language display name |
| Symbol | string | e.g. "kg" |
| UnitType | int | 0=Weight, 1=Volume, 2=Length, 3=Count, 4=Area |
| BaseUnitId | Guid? | Self-referencing — null for base units |
| ConversionFactor | decimal | Multiplier to convert to BaseUnitId's unit; 1 for base units |

Translatable via `UnitOfMeasureTranslation` (Name, Symbol per language code).

## Events

`UnitOfMeasureCreatedEvent` — published to `mst-topic`. No current subscribers;
consuming services read via `GET /api/v1/masterdata/units-of-measure`.
