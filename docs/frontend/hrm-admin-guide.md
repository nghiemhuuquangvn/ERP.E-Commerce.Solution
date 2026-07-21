# Admin HRM CRUD pattern

The Admin app's `/hrm/*` pages are the UI for **HRMService** (port 5007, Gateway
prefix `/hrm`) — Attendance, Leave Requests, Payroll & Pay Slips, and Work Shifts.
This is a **different pattern from `masterdata-admin-guide.md`**: HRM is its own
service with its own gateway route and its own `PagedResult<T>` shape, not
SharedMasterDataService. Don't reuse `mstClient.ts`/`MST_BASE` for HRM calls, and
don't re-derive the shape from a masterdata page — use this guide and the existing
`src/pages/hrm/*.tsx` files as the template instead.

HRMService no longer owns Employee or Department master data — those moved to
SharedMasterDataService (see root `CLAUDE.md`). HRM keeps a local, read-only
`EmployeeSnapshot` projection instead, exposed at `GET /api/v1/hrm/employees`
(`hrm:employees:view`). Every employee picker on `/hrm/*` pages (check-in, leave
submission, pay slip generation, filters) is fed by this HRM-local snapshot, not by
`masterdata/employees.api.ts` — the snapshot exists specifically so HRM's own pages
don't need a cross-service call for something as basic as populating a dropdown.

## File layout

1. `src/api/hrm/hrmClient.ts` — thin base, mirrors `masterdata/mstClient.ts`'s
   pattern: `apiClient` re-export + `HRM_BASE` (`/hrm/api/v1/hrm`, rooted the same
   way `MST_BASE` strips a trailing `/api` from `VITE_API_URL`) + `HrmPagedResult<T>`.
2. One `.api.ts` per HRM sub-domain, mirroring the real controller's
   request/response shapes exactly (read the actual controller in
   `src/services/HRMService/src/ERP.HRM.API/Controllers/*.cs` before writing —
   don't guess field names):
   - `employeeSnapshots.api.ts` — `EmployeesController` (list/get, read-only).
   - `attendance.api.ts` — `AttendanceController` (list/get/getByDate/getSummary/
     checkIn/checkOut/markAbsent).
   - `leaveRequests.api.ts` — `LeaveRequestsController` (list/get/submit/approve/
     reject/cancel).
   - `payroll.api.ts` — `PayrollController` (listPaySlips/getPaySlip/
     generatePaySlip/processPayroll). Note the controller has no class-level
     `[Route]` — each action's route is spelled out in full
     (`api/v1/hrm/payroll/...`, `api/v1/hrm/payslips/...`).
   - `workShifts.api.ts` — `WorkShiftsController` (list/get/create/update/
     deactivate). `List` returns a bare `WorkShiftDto[]`, **not** a paged result —
     don't wrap it in `HrmPagedResult` or add a `Pagination` component for this
     page.
   Each numeric enum constant (`ATTENDANCE_STATUS`, `LEAVE_TYPE`, `LEAVE_STATUS`,
   `PAYMENT_STATUS`) must match `ERP.HRM.Domain.Enums.HrmEnums.cs` exactly — the
   backend serializes the string via `.ToString()` on the DTO (e.g. `"Approved"`,
   not `"APPROVED"`), so `StatusBadge` keys must be PascalCase enum names, not
   upper-case.
3. `src/pages/hrm/FooPage.tsx` — one file containing both the list page and its
   modal(s), same convention as masterdata pages. Gate list visibility on the
   `:view` permission and mutations on the `:manage` permission — but check the
   actual `[AuthorizePermission]` attributes first, because HRM's permission
   granularity isn't uniform:
   - Attendance and Work Shifts have separate `:view`/`:manage` permissions.
   - Leave Requests and Payroll have **only** `hrm:leave:manage` /
     `hrm:payroll:manage` — there is no separate `:view` permission, so even the
     list endpoint requires `:manage`. Don't invent a `:view` permission string
     for these two pages.
   - No generic "Edit" action where the backend doesn't expose one: Work Shifts
     has Create + Update + Deactivate only (no Activate — once deactivated, a
     shift stays deactivated in the UI, matching the backend).
4. Wire into `src/router/index.tsx` under `hrm/foo` (flat, no nesting — see the
   existing `hrm/attendance`, `hrm/leave-requests`, `hrm/payroll`, `hrm/work-shifts`
   routes).
5. Wire into `src/components/layout/Sidebar.tsx`'s `HRM_CHILDREN` array (a
   collapsible nav group, same mechanism as `SETTINGS_CHILDREN`), gated on the
   page's `:view` (or `:manage`, per the point above) permission.
6. Add i18n keys under `hrm.*` in both `en.json`/`vi.json` — see
   [i18n-guide.md](i18n-guide.md). Shared cross-page strings (`hrm.employee`,
   `hrm.allEmployees`, `hrm.notes`, `hrm.required`) live at the top of the `hrm`
   namespace, not duplicated per sub-page.

## Conventions already established

- Employee picker query key: `['hrm', 'employees', 'picker']`, `pageSize: 200`,
  `staleTime: 60_000` — same shape as the masterdata pattern's department/employee
  pickers, just pointed at `employeeSnapshotsApi.list` instead.
- `StatusBadge` colors for HRM statuses live in the same shared
  `STATUS_COLORS` map in `src/components/shared/StatusBadge.tsx` as every other
  domain — add new keys there rather than a second badge component.
- Modal styling: same fixed-overlay `<div className="fixed inset-0 z-50 ...">`
  wrapping a `.card`, copied from the masterdata pages.

## When HRMService gains a new sub-domain or endpoint

Repeat the steps above: read the real controller first, add a `.api.ts` mirroring
it exactly, confirm the actual permission string(s) rather than assuming
`hrm:{feature}:view`/`:manage` both exist, then wire page → router → sidebar → i18n.
