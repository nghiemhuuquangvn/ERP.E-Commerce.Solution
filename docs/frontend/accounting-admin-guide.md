# Admin Accounting CRUD pattern

The Admin app's `/accounting/*` pages are the UI for **AccountingService**
(port 5005, Gateway prefix `/acc`) business data: Chart of Accounts, Journal
Entries, Invoices (AR), Bills (AP), Fiscal Periods, Tax Rates, Bank Accounts,
and read-only Reports. This is separate from the pre-existing Accounting
**Settings** page (`src/pages/settings/TaxSettingsPage.tsx`,
`api/settings/tax.settings.api.ts`, `settings:tax:*` permissions) — that page
is tenant tax-rate *configuration*, not the business-data pages this guide
covers. Don't confuse the two permission families
(`settings:tax:{system|tenant}:{view|manage}` vs. `accounting:tax:{view|manage}`).

## Fixed 2026-07-21: the `:view` permission half didn't exist

Before this UI was built, `AccountingService`'s controllers already checked
`accounting:{entity}:view` on every GET action, but that half of the
view/manage pairing had never been added anywhere — not in
`ERP.Shared.Authorization.PermissionConstants`, not in `RoleDefinitions`, not
in any DB seed. Every list/detail endpoint across all 7 business controllers
was unconditionally `Forbid()`-ed for every role, including SuperAdmin. Fixed
by adding `Accounting_{Entity}View` constants + role mappings (Admin gets
view+manage, Manager/Viewer get view-only). Also fixed in the same pass:
`AccountsController.Create` was checking `accounting:invoices:manage`
instead of `accounting:accounts:manage` (copy-paste bug present since the
service's original `git` commit). If you add a new AccountingService
endpoint, verify its permission string is actually granted to some role in
`RoleDefinitions.cs` before assuming the UI will work — don't repeat this gap.

## Backend quirk: most entities have no update endpoint

Unlike `masterdata-admin-guide.md`'s default Create+Edit modal pair, only
**Tax Rates** has a real `PUT` update endpoint. The other 6 entities are
Create-only, with specific status-transition actions instead of a generic
Edit:

| Page | Create | Status actions | Edit? |
|---|---|---|---|
| Accounts | ✅ | — | ❌ none |
| Fiscal Periods | ✅ (`Open`) | Close | ❌ none |
| Tax Rates | ✅ | Deactivate | ✅ real `PUT` |
| Invoices | ✅ | Mark Paid | ❌ none |
| Bills | ✅ | Approve, Mark Paid | ❌ none |
| Journal Entries | ✅ (create = post, one step) | — | ❌ none (view-only detail modal) |
| Bank Accounts | ✅ | Statements: Add, Reconcile (nested sub-resource) | ❌ none on the account itself |

Don't add an Edit modal for the 6 Create-only entities just because
`masterdata-admin-guide.md`'s default pattern has one — the backend doesn't
support it.

## Backend quirk: "paginated" list endpoints return a plain array

Invoices, Bills, and Journal Entries accept `pageNumber`/`pageSize` query
params, but their handlers (`AccountingQueryHandlers.cs`) return a bare
`List<TDto>` — **no total count, no total-pages field**. The shared
`Pagination` component (`@/components/shared/Pagination.tsx`) requires a real
`totalCount` to compute page buttons and cannot be used here. These 3 pages
instead use a minimal inline Previous/Next control: track `pageNumber` in
local state, disable Next when the returned array's length is less than
`pageSize`. Accounts, Fiscal Periods, Tax Rates, and Bank Accounts have no
pagination at all (full list every call).

## File layout

1. `src/api/accounting/accClient.ts` — mirrors `masterdata/mstClient.ts`'s
   pattern: `apiClient` re-export + `ACC_BASE` (`/acc/api/v1`, rooted the same
   way `MST_BASE` strips a trailing `/api` from `VITE_API_URL`). No
   `AccPagedResult<T>` type — see the pagination quirk above.
2. One `.api.ts` per entity under `src/api/accounting/`, mirroring the real
   controller's request/response shapes exactly (read
   `src/services/AccountingService/src/ERP.ACC.API/Controllers/*.cs` and the
   DTOs/commands under `ERP.ACC.Application` before writing — don't guess
   field names or enum values). Two serialization quirks already confirmed:
   - `AccountDto.accountType`/`normalBalance` are strings on read, but
     `CreateAccountCommand` sends them as the raw **int** enum value
     (`AccountType`: Asset=1, Liability=2, Equity=3, Revenue=4, Expense=5;
     `NormalBalance`: Debit=1, Credit=2).
   - `InvoiceDto.status`/`BillDto.status` are strings on read
     (`"Unpaid"`/`"Paid"`/`"Overdue"`, `"Pending"`/`"Approved"`/`"Paid"`), but
     list filters take the raw int. `CreateBillCommand.POId` → TS field
     `poId` (camelCase of the all-caps acronym).
3. `src/pages/accounting/FooPage.tsx` — one file containing the list page and
   its modal(s), same convention as masterdata pages. Gate list rendering on
   the entity's `:view` permission, mutations on `:manage` — see the table
   above for which entities have `:manage` actions at all.
4. **Journal Entries is the most complex form in this batch**: `POST` builds
   `PostJournalEntryCommand { EntryNumber, FiscalPeriodId, Description, Lines[] }`
   via `react-hook-form`'s `useFieldArray` (first use of it in this codebase —
   check for it before assuming a different dynamic-rows pattern exists). The
   form shows a live Total Debit/Total Credit banner and blocks submit until
   they're equal — journal entries must balance, and there's no server-side
   recovery UI if you post an unbalanced one. `AccountId` and `FiscalPeriodId`
   dropdowns are read-only cross-references fetched directly in this page
   (`GET {ACC_BASE}/accounts`, `GET {ACC_BASE}/fiscal-periods`) — don't
   duplicate the Accounts/Fiscal Periods pages' logic, just fetch the list.
5. Wire into `src/router/index.tsx` under `accounting/foo` (flat, no
   nesting — see the existing 8 routes).
6. Wire into `src/components/layout/Sidebar.tsx`'s `ACCOUNTING_CHILDREN`
   array — a collapsible nav group (same mechanism as `SETTINGS_CHILDREN`/
   `HRM_CHILDREN`, introduced when the Accounting UI was merged alongside the
   HRM UI's grouped-nav refactor), gated on the page's `:view` permission.
   The group header itself needs `nav.accounting` in both locale files
   (label only, not a page).
7. Add i18n keys under `accounting.*` in both `en.json`/`vi.json` — see
   [i18n-guide.md](i18n-guide.md).

## Conventions already established

- Permission convention: `accounting:{entity}:{view|manage}` — entity codes
  are `accounts`, `journals`, `invoices`, `bills`, `periods`, `tax`, `bank`,
  and `reports` (view-only, no manage variant exists for Reports).
- `StatusBadge` colors for Accounting statuses live in the same shared
  `STATUS_COLORS` map in `src/components/shared/StatusBadge.tsx` as every
  other domain.
- Destructive/significant actions (Close a fiscal period, Deactivate a tax
  rate, Reconcile a bank statement) go through the shared `ConfirmDialog`
  component — these are hard-to-reverse business actions, not simple deletes.
- Modal styling: same fixed-overlay `<div className="fixed inset-0 z-50 ...">`
  wrapping a `.card`, copied from the masterdata pages.

## When AccountingService gains a new sub-domain or endpoint

Repeat the steps above: read the real controller first, add a `.api.ts`
mirroring it exactly, and — given this domain's history — explicitly verify
in `src/Shared/Authorization/PermissionConstants.cs`/`RoleDefinitions.cs` that
whatever permission string the new endpoint checks is actually defined and
granted to some role. Don't assume it exists just because the controller
references it.
