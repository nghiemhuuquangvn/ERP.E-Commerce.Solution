# Manufacturing - Plants, production parameters and item manufacturing profile (M1)

> Service: ManufacturingService (code **MFG**, port 5018, PostgreSQL `ManufacturingDB`). Source specs: `docs/business/M/01_PRODUCTION_BA_SPEC.md` §5.1-5.3, §7.1, §9;
> tasks T0.1.4-5, T0.4-0.6 of `02_PRODUCTION_DEV_TASKS.md`; tests TC-SET, TC-PLT, TC-ITM, TC-SEC-001 of `03_PRODUCTION_TEST_CASES.md`.
> Differences from the specs: `00_Mapping-to-ERP.md`. Decision record: `docs/architecture/adr-manufacturing-service.md`. Status: built and checked live 2026-11-06 (milestone M1).

## 1. Purpose and scope
Before anything can be planned or produced, the shop must say **where** it produces (plants and their warehouses), **under which rules** (tolerances, costing method, scheduling switches, MRP horizon),
and **how each item is sourced** (made or bought, lead times, lot-sizing policy, safety stock, final-quality check, shelf life). This milestone delivers those three master-data areas, the rule that a
user only works in the plants they were given, and the read-only calls to Product, MasterData and Warehouse that keep them honest.
Not in this milestone: calendars and work centers (M2), BOM (M3), routing (M4), Admin screens (M5), orders and stock movements (Phase 3).

## 2. Actors
| Actor | What they do here | Permission |
|---|---|---|
| Administrator (R-ADM) | Creates and changes plants, sets production parameters, grants plant access | `manufacturing:masterdata:manage`, `manufacturing:settings:manage` |
| Engineer / planner (R-ENG, R-PLN) | Maintains the manufacturing profile of items | `manufacturing:item-profile:manage` |
| Anyone working in production | Reads plants, parameters and profiles of plants they may see | `manufacturing:masterdata:view` |
| Other services (system) | Product, MasterData and Warehouse answer lookups for Manufacturing | service token `manufacturing` (never a user token) |

## 3. Process
1. The administrator creates a **plant**: code, name, time zone, raw-material / finished-goods / quarantine warehouse (scrap optional). The system checks the warehouses with the Warehouse service
   and creates the plant's **production parameters** with the default values in the same save.
2. The administrator adjusts the **parameters** of the plant. Every changed field is audited (old and new value, who, when). The change counts for transactions after the save, never backwards.
3. The administrator **grants** plant access to users who are not administrators (otherwise those users see no plant).
4. The engineer opens an item (a product of ProductService) in a plant and saves its **manufacturing profile**; until then the item has the documented defaults.

## 4. Requirements and acceptance criteria
| ID | Requirement | Acceptance criteria |
|---|---|---|
| M1-01 | A plant has a unique code per tenant, 2-20 characters A-Z 0-9 '-' (BR-PLT-01) | `pl01` is stored as `PL01`; a second `PL01`/`pl01` is refused with `MFG_GEN_006`; `P`, `PL 01`, `PL_01`, 21 characters are refused |
| M1-02 | A plant names a raw-material, a finished-goods and a quarantine warehouse; scrap is optional (BR-PLT-02) | Missing quarantine warehouse refused; a warehouse that does not exist or is inactive is refused and nothing is saved; if the Warehouse service cannot be reached the plant is not created (`MFG_INT_001`) |
| M1-03 | The default calendar is optional until calendars exist (BR-PLT-03) | A plant can be saved without a calendar; M2 adds the calendar and the rule "required to release an order" |
| M1-04 | Creating a plant creates exactly one parameter row with the default values (BR-SET-01) | Plant and parameters appear together; defaults: tolerances 0, block-release off, negative stock off, sequential operations on, auto-complete on, manual issue, Actual costing, MRP horizon 90, BOM levels 20, lot format `{ItemCode}-{yyMMdd}-{seq2}` |
| M1-05 | Parameter ranges (§5.1) | Tolerances 0-100 (100 accepted, 100.01 and -1 refused); MRP horizon 7-365 (`MFG_MRP_002`); BOM levels 1-50; lot format required |
| M1-06 | A change is audited and effective afterwards only (BR-SET-02) | The audit lists field, old value, new value, user, time (e.g. `OverProductionTolerancePct 0 -> 5`); saving unchanged values writes nothing; a cached value is dropped on save |
| M1-07 | The costing method cannot change while orders are open (BR-SET-03) | `MFG_SET_001` when Released / InProgress / Completed-not-closed orders exist (guard is a stub "no orders" until Phase 3; the rule is unit-tested with a guard that says "open"); other fields may change anyway |
| M1-08 | An item profile exists per (item, plant) for an **active product with a base unit** (BR-ITM-01) | A missing, inactive or unit-less product is refused; a Product-service outage fails closed |
| M1-09 | The lot-sizing policy needs its parameters (BR-ITM-03) | FixedQty needs a lot quantity > 0; MinMultiple needs min >= 0 and multiple > 0; PeriodOrder needs 1-90 days; all give `MFG_ITM_002`; parameters of other policies are dropped; lead times, safety stock >= 0, standard lot > 0 |
| M1-10 | Low-level code and standard cost belong to the system (BR-ITM-04) | They are not in the update body and survive every profile update |
| M1-11 | An item without a profile shows documented defaults (T0.6.3) | `GET item-profiles/{id}?plantId=` returns `isDefault = true`, Make, lot-for-lot, no lead time |
| M1-12 | A user sees only the plants they were granted (BR-GEN-09, TC-SEC-001) | List filtered; reading a foreign plant answers 404 (not 403); holders of `manufacturing:settings:manage` see all plants |
| M1-13 | A refusal carries its code (BR-GEN-07) | The answer has `errorCode` (e.g. `MFG_GEN_006`) next to the Vietnamese `detail` |

## 5. Business rules
BR-PLT-01..03, BR-SET-01..03, BR-ITM-01, 03, 04, BR-GEN-06 (decimal places, via the unit of measure), BR-GEN-07, BR-GEN-09 - wording and codes in the spec (§5.1-5.3, Appendix A).
Rules added by the implementation: the plant **code is never edited** (it identifies the plant in documents); a plant is deactivated, never deleted; a plant's warehouses are re-checked whenever the plant is edited.

## 6. Use cases
**UC-1 Create a plant** - Main: administrator enters code, name, warehouses -> system normalises the code, checks uniqueness and the warehouses -> plant and default parameters are saved -> 201.
Alternative: time zone left empty -> `Asia/Ho_Chi_Minh`. Exceptions: duplicate code (400 `MFG_GEN_006`); warehouse unknown / inactive (400, nothing saved); Warehouse service down (400 `MFG_INT_001`).

**UC-2 Change production parameters** - Main: administrator edits values -> system validates ranges, compares with the stored values, writes one audit row per changed field, saves, drops the cache -> 200 with the new values.
Alternative: nothing changed -> 200, no audit, no save. Exceptions: value out of range (400); costing method change with open orders (400 `MFG_SET_001`).

**UC-3 Save an item's manufacturing profile** - Main: engineer picks an item and plant, enters make / buy, lead times, lot policy, safety stock, FQC, shelf life -> system checks the policy parameters, asks Product for the item (active, base unit) -> profile created or updated, system-owned fields untouched.
Exceptions: invalid policy parameters (400 `MFG_ITM_002`, no call to Product is made); item missing / inactive / without base unit (400); Product service down (400 `MFG_INT_001`).

**UC-4 Give a user access to a plant** - Main: administrator grants (idempotent) or revokes access -> the user sees / no longer sees the plant. Exception: revoking a grant that does not exist (404).

## 7. Data
Tables (PostgreSQL, `src/Database/Manufacturing/002_PlantsSettingsItemProfiles.sql`): `Plants` (unique `TenantId + Code`), `ProductionSettings` (unique per plant), `ProductionSettingAudits`, `ItemManufacturingProfiles` (unique `TenantId + ItemId + PlantId`),
`PlantUserAccesses` (unique `TenantId + PlantId + UserId`). Every row carries `TenantId`, audit columns, soft-delete flag; optimistic locking by `xmin`. Item and warehouse are referenced by id only (no foreign key across services);
item code and name are a display snapshot refreshed on every profile save. Enumerations are stored as smallint (Make 1 / Buy 2 / Both 3; LotForLot 1 / FixedQty 2 / MinMultiple 3 / PeriodOrder 4; Manual 1 / Backflush 2; Actual 1 / Standard 2).

## 8. Integrations
| Call | Direction | Purpose | Failure |
|---|---|---|---|
| `POST internal/v1/product-lookup` (ProductService) | MFG -> PRD | exists / Active / base unit / code / name of items | fail closed `MFG_INT_001` |
| `POST internal/v1/warehouses/lookup` (WareHouseService) | MFG -> WH | warehouse exists, active, of the tenant | fail closed |
| `POST internal/v1/units-of-measure/lookup` and `/convert` (MasterData) | MFG -> MST | decimal places of a unit; quantity between units of one base | fail closed; units of different bases = 400 |
All three use the service token of `manufacturing` (receivers list it under `ServiceAuth__TrustedCallers__manufacturing`), answers are cached 60 s with the tenant in the key, and none of the paths is routed by the Gateway.
New in MasterData: `UnitsOfMeasure.DecimalPlaces` (0-6; migration 009 sets 3 for weight / volume / length / area units, 0 for counted units; editable in the Admin unit screen).

## 9. Permissions
`manufacturing:masterdata:view` (read plants, parameters, profiles), `manufacturing:masterdata:manage` (create / change / (de)activate plants), `manufacturing:settings:manage` (parameters, audit, plant access, sees all plants),
`manufacturing:item-profile:manage` (save profiles). Identity migration 033 gives them to SuperAdmin and Admin; shops grant them to their own roles. Licence module `MFG` (PROFESSIONAL and ENTERPRISE plans).

## 10. API
`GET/POST api/v1/manufacturing/plants`, `GET/PUT plants/{id}`, `POST plants/{id}/activate|deactivate`, `GET/PUT plants/{id}/settings`, `GET plants/{id}/settings/audit`, `GET/POST plants/{id}/access`, `DELETE plants/{id}/access/{userId}`,
`GET item-profiles?plantId=&search=`, `GET/PUT item-profiles/{itemId}?plantId=`. Through the Gateway as `/mfg/api/v1/manufacturing/...`. Enumerations are sent as names or numbers and answered as names.

## 11. Assumptions and open issues
Assumptions: a plant's warehouses live in the Warehouse service; the item is a ProductService product; one profile per item per plant; a user who is not a settings administrator needs explicit plant access.
Open issues are recorded in `docs/DEFERRED-ISSUES.md` R154-R161 (Warehouse has no company, decimal-place defaults, plant access has no screen yet, cache staleness, unit-less products, concurrency / duplicate-code race, PostgreSQL drift test).

## 12. UAT scenarios
1. Create plant `PL01` with three warehouses -> parameters exist with defaults. Create `pl01` again -> refused with `MFG_GEN_006`.
2. Set over-production tolerance 5 -> audit shows `0 -> 5`; reload shows 5. Try 100.01 -> refused. Change costing method to Standard (no orders) -> accepted.
3. Save a profile for an active product with a base unit: lead time 2, safety stock 10, FQC on -> reload shows it. Same for a draft product -> refused. FixedQty with quantity 0 -> `MFG_ITM_002`.
4. As a user without grant, list plants -> empty; grant `PL01` -> visible; open another plant by id -> 404.
5. Stop the Product service and save a profile -> refused with `MFG_INT_001`, nothing stored.

## Screens (M5)
Guide: `docs/frontend/manufacturing-admin-guide.md`. **Plants** (`/manufacturing/plants`): list, add / edit (warehouses chosen from the warehouse list, default calendar), activate / deactivate with confirmation, and the tab **Who may work here** (grant / revoke users; only holders of the settings permission). **Production settings** (`/manufacturing/settings`): the tolerances, behaviour switches, issue and costing method, planning horizon, maximum BOM levels and lot-number format of the selected plant, saved with one button (disabled until something changed), the change history beside it; a refusal (costing method while orders are open) appears in a red box; without the settings permission the form is read-only. **Item profiles** (`/manufacturing/item-profiles`): searchable list with the low-level code, add an item through the product picker, edit dialog whose fields follow the lot-sizing policy (fixed quantity, minimum and multiple, days per period).
UAT: create a plant with the three warehouses (a refused warehouse is named); change the over-production tolerance and see "0 -> 5" in the history; choose *Fixed quantity* for an item and see the quantity field appear.
