# Admin Master Data CRUD pattern

The Admin app's `/masterdata/*` pages are the UI for `SharedMasterDataService` (port
5015, Gateway prefix `/mst`). Six entities exist today: Departments, Positions,
Employees, Units of Measure, Geography (Countries/Provinces/Districts/Wards),
Languages. Use this as the template for any new SharedMasterDataService entity added
later — don't re-derive the shape from `ProductListPage.tsx`, it's a different
(non-master-data) pattern.

## File layout for one entity ("Foo")

1. `src/api/masterdata/foo.api.ts` — thin wrapper, mirrors the real backend
   controller's request/response shapes **exactly**. Read the actual controller in
   `src/services/SharedMasterDataService/src/ERP.MST.API/Controllers/FooController.cs`
   before writing this file — do not guess field names or which fields are
   create-only vs. update-only. Example (`departments.api.ts`): `CreateDepartmentRequest`
   has `parentDepartmentId`, `UpdateDepartmentRequest` does not — the backend doesn't
   support moving a department's parent via update, so the frontend type must not
   pretend it does.
2. `src/api/masterdata/mstClient.ts` — shared axios instance + `MST_BASE` +
   `MstPagedResult<T>` — reuse this, don't create a second client.
3. `src/pages/masterdata/FooPage.tsx` — one file containing both the list page and
   its modal(s) (`FooModal` function above the page component, not a separate file —
   matches `DepartmentsPage.tsx`/`PositionsPage.tsx`/`UnitsOfMeasurePage.tsx`).
   - List: `useQuery` + `DataTable` + `Pagination`, gated by
     `usePermission().hasPermission('masterdata:foo:view')`.
   - Mutations: `react-hook-form` + `useMutation` + `queryClient.invalidateQueries`,
     gated by `masterdata:foo:manage`.
   - No generic "Edit" action if the backend doesn't expose a single update endpoint
     — Employees, for instance, has **no** generic edit; instead four distinct
     actions (Hire, Update Profile, Transfer, Terminate) each with their own modal,
     matching the backend's distinct endpoints. Positions/Departments/UnitsOfMeasure
     do have single update endpoints, so those get one Edit modal.
   - If the backend endpoint is create-only (no update at all, e.g. Geography's
     Province/District/Ward), don't build an Edit modal — build Create-only, like
     `GeographyPage.tsx`.
4. Wire into `src/router/index.tsx` under `masterdata/foo` (see the existing block
   there — six sibling routes, flat, no nesting).
5. Wire into `src/components/layout/Sidebar.tsx` nav item, gated on
   `masterdata:foo:view` — verify the exact permission string by grepping the real
   controller's `[AuthorizePermission(...)]` attributes, don't assume the naming
   convention holds without checking.
6. Add i18n keys under `masterdata.foo.*` in both `en.json`/`vi.json` — see
   [i18n-guide.md](i18n-guide.md).

## Conventions already established

- Permission strings: `masterdata:{entity-kebab-case}:view` and
  `masterdata:{entity-kebab-case}:manage` (e.g. `masterdata:units-of-measure:view`).
- **Dialogs and forms (U3, 2026-11-01):** every Master data page uses the component library
  (`docs/frontend/components-guide.md`): `<Modal open title size onClose closeOnEsc={!mutation.isPending} footer={...}>` with
  `<form id="x-form">` inside and a footer submit button `type="submit" form="x-form"`; each field is
  `<FormField label required error={errors.x ? t('common.required') : undefined}>` + `Input` / `Select` / `Textarea` /
  `Checkbox` (`register()` works unchanged); a refusal is `<Alert variant="danger">{serverMessage(mutation.error, t('...failedToSave'))}</Alert>`
  (`lib/serverMessage.ts`); success is `toast.success(t('common.saved'))`. Never write a `fixed inset-0` block. Copy
  `DepartmentsPage.tsx` for a new entity. `masterdata.pages.test.tsx` lists every page: **add the new page to its `PAGES` table**
  (it checks the empty state, the add dialog, linked required errors, axe and Esc).
- `StatusBadge` for boolean/enum status columns (see `PositionsPage.tsx`'s
  Active/Inactive), `ConfirmDialog` for destructive/state-changing confirmations
  (deactivate, terminate) — both are shared components, reuse as-is.
- Old top-level routes that pointed at a since-removed HRM page (e.g. `/employees`)
  are kept as a `<Navigate replace>` redirect to the new `/masterdata/*` path rather
  than deleted outright, so existing bookmarks/links don't 404.

## When SharedMasterDataService gains a 7th entity

Repeat the 6 steps above. If the new entity has a translation child table on the
backend (per `TranslationEntity`), add an optional `languageCode` param to `list`/
`get` in its `.api.ts` (see `departments.api.ts`'s `ListDepartmentsParams.languageCode`)
— the frontend does not need a language picker per-row, the Admin app always
requests in the currently active UI locale via the `Accept-Language` header; the
`languageCode` param exists for the rare case of viewing/editing a specific
translation directly.
