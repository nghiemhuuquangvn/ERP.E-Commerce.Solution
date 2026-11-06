# Manufacturing - Calendars, work centers, machines, standard operations, reason codes and crews (M2)

> Service: ManufacturingService (MFG). Source specs: `docs/business/M/01_PRODUCTION_BA_SPEC.md` §5.4-5.7, §7.1; tasks T1.1-T1.6 of `02_PRODUCTION_DEV_TASKS.md`; tests TC-CAL, TC-WC, TC-MCH, TC-OPS, TC-RSN, TC-LBR of `03_PRODUCTION_TEST_CASES.md`.
> Differences from the specs: `00_Mapping-to-ERP.md`. Previous milestone: `PlantSettingsItemProfile.md`. Status: built and checked live 2026-11-06 (milestone M2). Screens are milestone M5.

## 1. Purpose and scope
Scheduling, capacity and costing all rest on four facts: **when** a plant works (shifts and calendars), **where** work is done (work centers and their machines), **what** a typical step looks like (standard operations)
and **who** does it (crews). Reason codes are the controlled vocabulary used when something is scrapped, reworked, stopped, held or cancelled. This milestone delivers those master data and the two calculations built on them:
**working-time arithmetic** (add / subtract working minutes and days over a calendar) and **work-center capacity**.
Not in this milestone: BOM (M3), routing (M4), orders, downtime logs and OEE (later phases), the Admin screens (M5).

## 2. Actors
| Actor | What they do | Permission |
|---|---|---|
| Administrator / plant manager | Maintains shifts, calendars, work centers, machines, reason codes, crews | `manufacturing:masterdata:manage` |
| Engineer / planner | Reads them; uses the standard operations when building routings (M4) | `manufacturing:masterdata:view` |
| Scheduling and costing (system) | Ask the calendar for working time, the work center for capacity and rates | internal calls |
| MasterData (system) | Confirms that crew members are real, employed people | service token `manufacturing` |

## 3. Process
1. Define the **shifts** of the plant (start, end, break). Define a **calendar**: for each weekday the shifts worked; add **exceptions** (holidays, extra working days, days with other shifts). Make the calendar the plant's default.
2. Define **work centers** (machine group, labour station or subcontractor) with calendar, number of resources, efficiency and utilization, rates, queue and move time; define the **machines** of each center and their ideal cycle time.
3. Define **standard operations** engineers pick from when building routings. Review the **reason codes** (a default set appears for every shop).
4. Define **crews** and put employees into them for a period, optionally with an own labor rate.
5. Ask the system: the working minutes of any day (month view), "what is 1000 working minutes before the end of Monday 16 November", the capacity of a work center over a range.

## 4. Requirements and acceptance criteria
| ID | Requirement | Acceptance criteria |
|---|---|---|
| M2-01 | Shift duration (BR-CAL-01) | 06:00-14:00 with 30 min break = 450; overnight 22:00-06:00 = 450; 06:00-06:00 = 1440 allowed; a break equal to or longer than the shift is refused `MFG_CAL_001` |
| M2-02 | Shifts of one weekday may not overlap (BR-CAL-02) | 06-14 with 13-15 refused `MFG_CAL_002` (names both shifts); 14-22 with an overnight 21-05 refused; 06-14 + 14-22 (touching) and all three DS-01 shifts accepted; changing a shift so that a calendar using it would overlap is refused and nothing is saved |
| M2-03 | Working minutes of a day (BR-CAL-03) | CAL-5D: Monday 900, Saturday 450, Sunday 0, holiday 0; an extra working day or a modified day uses the exception's shifts; one exception per date (`MFG_GEN_006`); a holiday has no shifts, other exceptions need at least one |
| M2-04 | Working-time arithmetic (BR-CAL-05) | End of Monday 16/11 minus 1000 working minutes = Saturday 14/11, working minute 350 (clock 11:50); +1 working day from Friday 13/11 = Saturday 14/11, from Saturday = Monday 16/11; -1 working day from Monday 04/01/2027 = Saturday 02/01/2027; days without working minutes are skipped; a calendar without working time answers with a refusal within 366 days, never loops |
| M2-05 | Work center rules (BR-WC-01, 04, 05) | Code `[A-Z0-9-]{2,20}` (`MFG_WC_001`), unique per plant (`MFG_GEN_006`); efficiency 1-200, utilization 1-100, resources 1-999, rates and times >= 0 (`MFG_WC_002`); a Subcontract center needs a vendor (`MFG_WC_004`); the calendar must be an active calendar of the same plant |
| M2-06 | Capacity (BR-WC-02) | Working minutes x resources x efficiency% x utilization%: WC-CUT (2 resources, 90%) Monday 1620, Saturday 810, holiday 0; WC-PNT (80% efficiency) Monday 720; a range query lists every day (at most 400 days) |
| M2-07 | Deactivating a work center (BR-WC-03) | Refused `MFG_WC_003` while operations of released / in-progress / on-hold orders are on it (the order check is a stub until Phase 3 and is covered by a unit test with a fake that says "busy"); reactivating is always allowed |
| M2-08 | Machines (BR-MCH-01/02) | Belong to a work center of the plant; ideal cycle time > 0; status Active / UnderMaintenance / Retired; a Retired machine cannot be picked for reporting (`MFG_OP_010`, enforced when reports exist), a machine under maintenance can with a warning |
| M2-09 | Standard operations (BR-OPS-01) | Code unique per plant; setup >= 0, run > 0, batch size for per-batch time (`MFG_RT_002`); a routing copies the values (M4) |
| M2-10 | Reason codes (BR-RSN-01/02) | Code unique **per type**; a downtime reason is Planned or Unplanned; every tenant gets the 11 default reasons (SCRAP-01/02, RWK-01, DT-01/02/03, CAN-01, HLD-01, SC-01, UPM-01, OVI-01) on first use; seeding again adds nothing |
| M2-11 | Crews (BR-LBR-01/02) | An employee is in at most one crew at a time (overlapping periods refused); members must be existing, not terminated employees (checked with MasterData); an own rate of the membership beats the work center's labor rate on its valid dates |
| M2-12 | Master data are not deleted when used (BR-GEN-02) | A shift in a calendar, a calendar used by a work center, a work center with machines / standard operations cannot be deleted (`MFG_GEN_005`: deactivate instead); an unused one is soft-deleted and disappears from lists |
| M2-13 | The plant's default calendar (BR-PLT-03) | Must be a calendar of that plant; a new plant has none and gets one after the calendar exists |

## 5. Business rules
BR-CAL-01..05, BR-WC-01..05, BR-MCH-01..02, BR-OPS-01, BR-RSN-01..02, BR-LBR-01..02, BR-GEN-02, BR-PLT-03, BR-RT-02 (as applied to standard operations) - wording and codes in the spec.
Conventions fixed by this implementation: (a) **a shift works from its start for its working minutes, the break is taken at the end** (so 06:00-14:00 with 30 minutes works until 13:30); the spec gives only minutes, the clock position matters only for the planned start / end timestamps (R162);
(b) a position in working time is a **date plus the number of working minutes of that day already passed**; the end of a day's work stays on that day; (c) codes of shifts, calendars, work centers, machines, standard operations and crews are unique per plant, reason codes per type, all per tenant.

## 6. Use cases
**UC-1 Build a calendar** - Main: create shifts -> create the calendar -> set the shifts of each weekday -> add holidays -> read the month view -> set it as the plant's default calendar. Alternative: an extra working day (Sunday overtime) with one shift. Exceptions: overlapping shifts (`MFG_CAL_002`); two exceptions on one date (`MFG_GEN_006`); an inactive or foreign shift (400).
**UC-2 Ask the calendar** - Main: planner asks "subtract 1000 working minutes from the end of 16 Nov" -> the system answers date, working minute and clock time. Exception: calendar without working time (400).
**UC-3 Set up a work center and its machines** - Main: create the center (calendar, resources, efficiency, rates) -> add machines -> read the capacity chart. Exceptions: bad code, out-of-range percentages, Subcontract without vendor, calendar of another plant.
**UC-4 Retire a machine / stop a center** - Main: set the machine Retired; deactivate a center without open operations. Exception: deactivating a center with open operations (`MFG_WC_003`).
**UC-5 Maintain reasons** - Main: open the list (defaults appear for a new shop), add a shop-specific reason, deactivate one. Exceptions: same code twice in one type; downtime without a category.
**UC-6 Staff a crew** - Main: create a crew, add employees with a period (and an own rate). Exceptions: employee already in a crew in that period; unknown or terminated employee; end before start; negative rate.
**UC-7 Delete master data** - Main: an unused record is deleted (hidden). Exception: a used record is refused with `MFG_GEN_005`; the user deactivates it instead.

## 7. Data
`Shifts`, `WorkCalendars`, `WorkCalendarPatterns` (weekday + shift), `WorkCalendarExceptions` (+ `WorkCalendarExceptionShifts`), `WorkCenters`, `Machines`, `StandardOperations`, `ReasonCodes`, `Crews`, `CrewMembers` (SQL `Manufacturing/003_CalendarsWorkCenters.sql`);
the plant's `DefaultCalendarId` now has a foreign key. Rows carry `TenantId`, audit columns, soft-delete flag and optimistic locking by `xmin`. Enumerations are smallint: work center type (Machine 1, Labor 2, Subcontract 3), machine status (Active 1, UnderMaintenance 2, Retired 3),
run-time basis (PerUnit 1, PerBatch 2), exception type (Holiday 1, ExtraWorkingDay 2, ModifiedShifts 3), reason type (Scrap 1 ... OverIssue 8), downtime category (Planned 1, Unplanned 2); weekdays are 0 (Sunday) to 6 (Saturday).

## 8. Integrations
| Call | Direction | Purpose |
|---|---|---|
| `POST internal/v1/employees/lookup` (MasterData, new) | MFG -> MST | number, name and "still employed" of crew members and leaders (fail closed `MFG_INT_001`, cached 60 s per tenant) |
Department and vendor ids of a work center are stored as given (no internal endpoint to check them yet, R163). Provided to later milestones: `IWorkingTimeService` (routing lead time, scheduling), `ICapacityService` (load vs capacity), `ILaborRateResolver` (costing), `IOpenOrderGuard.HasOpenOperationsAsync` (order-backed from Phase 3).

## 9. Permissions
Reading: `manufacturing:masterdata:view`; writing: `manufacturing:masterdata:manage` (no new permission codes). Every plant-scoped action also needs the plant to be visible to the user (BR-GEN-09, see `PlantSettingsItemProfile.md`): a record of a plant the user may not see answers 404.

## 10. API (`/mfg/api/v1/manufacturing/...`)
`shifts` (GET ?plantId, POST, PUT {id}, POST {id}/activate|deactivate, DELETE), `calendars` (GET ?plantId, GET {id}, POST, PUT {id}, PUT {id}/days/{dayOfWeek}, POST {id}/exceptions, DELETE {id}/exceptions/{date}, activate / deactivate, DELETE, GET {id}/month?year&month, GET {id}/working-time?operation&date&workingMinute&amount),
`work-centers` (CRUD + activate / deactivate, GET {id}/capacity?from&to), `machines` (CRUD, PUT {id}/status), `standard-operations`, `reason-codes` (GET ?type, POST, PUT, activate / deactivate, DELETE, POST seed-defaults), `crews` (CRUD, POST {id}/members, DELETE {id}/members/{memberId}). Enumerations are sent as names or numbers and answered as names.

## 11. Assumptions and open issues
Open issues are in `docs/DEFERRED-ISSUES.md` R162-R166: break position convention, unchecked department / vendor ids, calendar cache per instance and first-read reason seeding, "used" checks that grow with later phases, masters stay editable in a deactivated plant.

## 12. UAT scenarios
1. Create CA1 06-14 (30), CA2 14-22 (30), CA3 22-06 (30): each shows 450. Try break 480 on 06-14 -> refused.
2. Calendar CAL-5D: Monday-Friday CA1+CA2, Saturday CA1. Month view of November 2026: 9th = 900, 14th = 450, 15th = 0. Add holiday 01/01/2027: that day shows 0 and is marked.
3. Try CA1 + a 13:00-15:00 shift on Monday -> refused naming both shifts.
4. Ask: subtract 1000 working minutes from 16/11/2026 (minute 900) -> 14/11/2026, minute 350, 11:50. Add 1 working day to 13/11 -> 14/11; to 14/11 -> 16/11. Subtract 1 working day from 04/01/2027 -> 02/01/2027.
5. Work center WC-CUT (2 resources, utilization 90%): capacity Monday 1620, Saturday 810, 01/01/2027 = 0. WC-PNT (efficiency 80%): Monday 720. Try efficiency 201 -> refused; Subcontract without vendor -> refused.
6. Machine M-CUT-01 on WC-CUT; set it Retired. Try a machine with cycle time 0 -> refused. Try to delete WC-CUT -> refused (machine on it); deactivate instead.
7. Open Reason codes in a new shop: 11 defaults, DT-01 Unplanned. Add `scrap-01` again -> refused. Downtime reason without category -> refused.
8. Crew T1 with employee A (own rate 70 000); put A into crew T2 from June -> refused; employee B into T2 -> accepted; an unknown employee -> refused.
