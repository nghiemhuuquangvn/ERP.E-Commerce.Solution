# Department

**Service:** SharedMasterDataService (Port 5015, Code: MST)
**Database:** MasterDataDB

## Purpose

Organizational department hierarchy used by any service that needs to attribute a
person, cost, or approval to an org unit (currently HRMService, via employee
assignment). Departments can nest via `ParentDepartmentId`.

## Fields

| Field | Type | Notes |
|-------|------|-------|
| DepartmentName | string | Default-language display name |
| Code | string | Unique per tenant |
| ManagerId | Guid? | References Employee.Id |
| ParentDepartmentId | Guid? | Self-referencing hierarchy |

Translatable via `DepartmentTranslation` (Name per language code) — see
`docs/architecture/database-map.md` for the translation-table pattern shared across
this service's reference entities.

## Workflow

Create department → assign manager (optional) → assign employees (Employee.DepartmentId)
→ update/reparent as the org changes.

## Events

`DepartmentCreatedEvent`, `DepartmentUpdatedEvent` — published to `mst-topic`. No
current subscribers; reference data is otherwise read via
`GET /api/v1/masterdata/departments`.

## Moved from HRMService

This entity was originally owned by HRMService. It moved here because Department
(and Employee) master data is used across multiple domains, not just HR — see
CLAUDE.md's Service Boundaries section for the rationale.
