# Position

**Service:** SharedMasterDataService (Port 5015, Code: MST)
**Database:** MasterDataDB

## Purpose

Job title / position master data (e.g. "Senior Accountant", "Warehouse Supervisor"),
optionally scoped to a Department. Did not exist anywhere in the platform before this
service; Employee now carries an optional `PositionId`.

## Fields

| Field | Type | Notes |
|-------|------|-------|
| Title | string | Default-language display name |
| Code | string | Unique per tenant |
| Description | string? | |
| DepartmentId | Guid? | Optional — a position can be department-agnostic |
| IsActive | bool | Deactivated positions are hidden from new-hire pickers, not deleted |

Translatable via `PositionTranslation` (Title, Description per language code).

## Events

`PositionCreatedEvent` — published to `mst-topic`. No current subscribers.
