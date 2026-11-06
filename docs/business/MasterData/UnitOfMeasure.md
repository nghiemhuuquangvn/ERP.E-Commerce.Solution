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

## Decimal places (2026-11-06)

`DecimalPlaces` (0-6, default 0) says how many decimals a quantity in the unit may have. Editable on the Admin unit screen (create and edit); migration `MasterData/009` set 3 for weight / volume / length / area units that were still at the default and left counted units at 0 (a starting point, R155). ManufacturingService reads it through `POST internal/v1/units-of-measure/lookup` and converts quantities of units sharing a base with `POST internal/v1/units-of-measure/convert` (quantity x from-factor / to-factor; units of different bases are refused). See `docs/business/Manufacturing/PlantSettingsItemProfile.md`.
