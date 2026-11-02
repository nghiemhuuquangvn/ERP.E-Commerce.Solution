# Admin component library (U2)

Done 2026-10-31 (branch `feat/ui-u2-core-components`, stacked on U1). Spec: [design-system/components.md](design-system/components.md); colours and icons: [tokens-and-icons-guide.md](tokens-and-icons-guide.md). **Rule: never build a modal, toast, label + error block or status colour inline in a page - use these.**

All components live in `admin/src/components/ui/` and are imported from the barrel: `import { Button, Modal, FormField, Input, useToast } from '@/components/ui'`. The shared `ConfirmDialog`, `ReasonDialog`, `ProductPickerModal`, `DataTable` and `StatusBadge` stay in `components/shared/` with their old props.

## Catalogue
| Component | Use it for | Key props and behaviour |
|---|---|---|
| `Button` | every action | `variant` primary / secondary / outline / danger / ghost / link / link-danger; `size` sm 32 / md 36 / lg 44; `loading` (label keeps its width, spinner on top, click ignored, `aria-busy`); `leftIcon` / `rightIcon`; `fullWidth`; `iconOnly` (TypeScript requires `aria-label`). `type` defaults to `button`; a submit button outside its form uses `type="submit" form="form-id"`. A text action in a table row is `variant="link"` (`link-danger` for delete / cancel) with `className="text-xs"`. |
| `FormField` | label + control + hint / error | `label`, `hint`, `error` (replaces the hint and is announced), `required` (star + `aria-required`). It links `htmlFor`, `aria-describedby`, `aria-invalid` to the control through context. |
| `Input`, `Textarea`, `Select` | form controls | Forward refs (react-hook-form `{...register('x')}` works), pick up the FormField linkage, `error` marks a standalone control invalid. A control fills its container; a `w-*` class you pass replaces `w-full`. |
| `Checkbox`, `Switch` | choices | `Checkbox` has its label inside the click target; `Switch` is for settings that apply at once (`checked`, `onChange(bool)`, `label` = accessible name) - follow it with a toast. |
| `Modal` | any dialog | `open`, `title`, `onClose`, `size` sm 400 / md 560 / lg 800, `footer` (buttons), `dismissOnBackdrop` (default off: forms must not lose data), `closeOnEsc` (turn off while saving). Portal, focus trap, Esc, page scroll lock, focus returns to the trigger, `role="dialog"` + title as its name. A dialog may open another (picker inside a form): the inner one handles its own keys. |
| `ConfirmDialog`, `ReasonDialog` | yes / no, reason required | Same props as before, now built on `Modal` (Esc cancels). |
| `Toast` (`ToastProvider`, `useToast`) | the result of an action | `toast.success(text)` disappears after 4 s, `toast.error(text)` stays until closed, `toast.info`; at most 3, top right. Provider is mounted in `App.tsx`. Text for failures: `serverMessage(e, fallback)`. |
| `Alert` | a persistent message inside a form or card | `variant` info / success / warning / danger (danger is announced at once), optional `title`, `onClose`. Use it for a refusal that stays next to the form; use a toast for "saved". |
| `EmptyState` | nothing to show | `variant` empty / no-results (action: clear filters) / error (action: retry) / forbidden; `title`, `description`, one `action`. |
| `Badge`, `StatusBadge` | status and tags | `Badge variant` success / warning / danger / info / muted / brand, `dot`. `StatusBadge status label` picks the role from **one map**, `components/ui/statusRoles.ts` (unknown = muted). Add a new status there, never as a colour in a page. |
| `Spinner`, `Skeleton`, `SkeletonRows` | loading | Skeleton rows replace "Loading..." text; motion stops under reduced-motion. |
| `DataTable` | lists | Old props unchanged (`columns`, `data`, `isLoading`, `emptyMessage`). New: `emptyState` (e.g. no-results with a Clear button), `stickyHeader`, column `align: 'right'` for numbers, `sortable` + `sortKey` / `sortDir` / `onSort` (`aria-sort`). While loading it shows skeleton rows and `aria-busy`. |

## Patterns
**A form in a dialog**
```tsx
<Modal open title={t('x.addTitle')} onClose={onClose} closeOnEsc={!mutation.isPending}
  footer={<><Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
            <Button type="submit" form="x-form" loading={mutation.isPending}>{t('common.save')}</Button></>}>
  <form id="x-form" onSubmit={handleSubmit((v) => mutation.mutate(v))} className="space-y-4">
    <FormField label={t('common.name')} required error={errors.name ? t('x.required') : undefined}>
      <Input {...register('name', { required: true })} />
    </FormField>
    {mutation.isError && <Alert variant="danger">{serverMessage(mutation.error, t('x.failedToSave'))}</Alert>}
  </form>
</Modal>
```
**Result of an action:** `onSuccess: () => { toast.success(t('common.saved')); ... }`, `onError: (e) => toast.error(serverMessage(e, t('common.failedToSave')))`.
**A list with filters:** `DataTable emptyState={filtered ? <EmptyState variant="no-results" ... action={<Button variant="outline" onClick={clear}>{t('common.clear')}</Button>} /> : <EmptyState title={t('x.empty')} />}`.

## Migrating a page (U3 does this area by area)
| Old | New |
|---|---|
| `<div className="fixed inset-0 ...">` modal | `Modal` with `footer` |
| `<label className="mb-1 ...">` + `<input className="input">` + red `<p>` | `FormField` + `Input` / `Select` / `Textarea` |
| `btn-primary` / `btn-outline` / `<button className="text-xs text-primary-600 ...">` | `Button` variants |
| inline green / red message box, `role="alert"` paragraph | `toast` (result) or `Alert` (stays) |
| "No records found" | `EmptyState` through `DataTable emptyState` |
| a status colour table | `statusRole` map |

## Tests
Vitest + Testing Library + axe: `npm test` in `admin/` (54 tests today). A test sits next to its component (`Name.test.tsx`); `src/test/setup.ts` initialises i18n in English; use `import { axe } from '@/test/axe'` and `expect(await axe(container)).toHaveNoViolations()`. jsdom cannot compute colour contrast, so contrast stays guarded by `npm run check:tokens`. For every new component test: its states, the keyboard, its accessible names, and axe in the default and error state.

## Pilot pages (U2)
Promotions, Flash sales, Loyalty, POS Transactions / Shifts / Z report / Terminals, Shipping Returns, Notification Webhooks and Message templates. The other ~60 pages still use `fixed inset-0` modals and the old classes (DEFERRED R71); an old modal and a new `Modal` share the same stacking level (z-50), the later one opened is on top.

## Storefront (Vue) — U4, 2026-11-02
The Storefront has its own small library in `src/frontend/storefront/src/components/ui/`, the Vue twin of the Admin one (same names, same contracts):
| Component | Notes |
|---|---|
| `Button` | variants primary / outline / ghost / link / danger, sizes, `loading` (spinner, disabled, `aria-busy`), `to` renders a router link styled as a button |
| `FormField` + `Input` / `Select` / `Textarea` / `Checkbox` | `provide`/`inject` (`composables/useField.ts`): the control gets `id`, `aria-describedby` (hint or error), `aria-invalid`, `aria-required`; `v-model`; the error text is rendered only by FormField |
| `Alert`, `Badge`, `EmptyState`, `Skeleton`, `Spinner` | `Alert` danger is `role=alert`, the others `status` |
| `ToastHost` + `useToast()` | module-level queue; success / info disappear after 4 s, an error stays; host mounted once in `App.vue` |
| `Drawer` | portal, focus trap, Esc, scroll lock, focus returns to the opener (the mobile menu); no `Modal` yet - nothing in the shop needs one |
| `Pagination`, `QuantityStepper`, `Stepper`, `PriceText` | named buttons, `aria-current="step"`, price bold + tabular, old price struck through with the percentage, "free" in words |
| `useMoney()` | the shop's two formatters (USD en-US, VND vi-VN) in one place, until U6 |
Rules: no raw colours or `brand-100/500/600` aliases (`noRawColours.test.ts`); no `.btn-*` / `.input` classes (deleted from `style.css`); every page keeps its business logic in `<script setup>` untouched (`CheckoutPage` still builds the same create-order request - asserted field by field); tests: `npm test` (Vitest + @vue/test-utils + vitest-axe, `src/test/setup.ts` mounts with the real en / vi messages), component tests in `components/ui/ui.test.ts`, page tests in `pages/pages.test.ts`. Worked examples: `CheckoutPage.vue` (stepper, per-field errors shown after the first attempt, radio group in a `fieldset`, pay button naming the amount), `StorefrontLayout.vue` (skip link, drawer, footer), `ProductGrid.vue` (loading / error with retry / empty slot). BA spec: [docs/business/Sales/StorefrontScreens.md](../business/Sales/StorefrontScreens.md).

## Migration status (U3)
| Area | Pages with own modals | Status |
|---|---|---|
| Pilot (U2) | 10 | on the library |
| **Master data** | 17 pages / 25 dialogs | **on the library (U3 batch 1, 2026-11-01)** - worked example: `pages/masterdata/DepartmentsPage.tsx`; smoke test `masterdata.pages.test.tsx` |
| **Products** | 11 files (6 dialogs, product form, list, images, attribute tabs) | **on the library (U3 batch 2, 2026-11-01)** - worked examples: `ProductVariantsTab.tsx` (modal + dynamic fields), `ProductFormPage.tsx` (plain-state form, per-field errors); smoke test `products.pages.test.tsx` |
| **HRM** | 6 pages / 13 dialogs | **on the library (U3 batch 3, 2026-11-01)** - worked examples: `PayrollPage.tsx` (two-step dialog, detail with a reason flow), `PayrollSettingsPage.tsx` (grid of inputs with aria-labels); smoke test `hrm.pages.test.tsx` |
| **CRM** | 1 page / 5 tabs / 11 dialogs | **on the library (U3 batch 4, 2026-11-01)** - worked examples: `LeadsTab.tsx` (action with a side effect behind a `ConfirmDialog`), `OpportunitiesTab.tsx` (stage counters as filter buttons, future default date), `CustomersPage.tsx` (URL tab + keyboard tablist); smoke test `crm.pages.test.tsx`; BA spec `docs/business/CRM/AdminBackOffice.md` |
| **Inventory / Warehouse** | 12 files (4 dialogs, stocktake and transfer actions, detail page, forms) | **on the library (U3 batch 5, 2026-11-01)** - worked examples: `StocktakeDetailPage.tsx` (state-dependent action bar, one `ConfirmDialog` per irreversible step, mutations sharing `onSuccess` / `onError`), `TransferOrderListPage.tsx` (one confirm dialog driven by a `step` state), `InventoryDetailPage.tsx` (tabs over `DataTable`s); smoke test `inventory.pages.test.tsx`; BA spec `docs/business/Warehouse/AdminBackOffice.md` |
| **Settings + Identity (dialogs)** | 6 files (Users 3 dialogs, Roles 2, Tenants, Carrier rules, Delivery fee rows, Notification history detail) | **on the library (U3 batch 6, 2026-11-01)** - worked examples: `UsersPage.tsx` (own-row guard, three dialogs, confirm texts with consequences), `RolesPermissionsPage.tsx` (save-per-tick dialog with a toast and filter), `CarrierRulesCard.tsx` (rule form with client-side range checks, delete confirm, "try a parcel" tester); smoke tests `identity.pages.test.tsx`, `settings.dialogs.test.tsx`; BA spec `docs/business/Identity/AdminBackOffice.md`. **The other 25 Settings pages and the 3 sign-in pages still use the old classes (R92).** |
| **Purchasing + Orders (PO list / detail)** | 5 files (RFQ list / detail / quotation, supplier invoices incl. match and settings, vendor returns, purchase order list and detail) | **on the library (U3 batch 7, 2026-11-02)** - worked examples: `RfqPages.tsx` (one `step` state driving a consequence-stating `ConfirmDialog` for Send / Cancel / Award / Decline, labelled line grid, `fieldset` groups), `PurchaseOrderDetailPage.tsx` (Receive dialog stacked with its confirm dialog, per-line limits), `SupplierInvoicePages.tsx` (reason `Textarea` + approve / reject confirms, verdict `Badge`s); smoke tests `purchasing.pages.test.tsx`, `orders.pages.test.tsx`; BA spec `docs/business/Purchasing/AdminBackOffice.md` |
| **Sales / Payments / Shipping / Logs** | 4 files (commission plans, payments + refund, shipments detail + split, system logs) | **on the library (U3 batch 8, 2026-11-02)** - worked examples: `ShipmentsPage.tsx` (detail dialog with stacked split dialog and confirm dialogs, written-out grid state), `PaymentsPage.tsx` (form dialog + consequence confirm for money going back), `LogsPage.tsx` (a drawer turned into a `Modal`); smoke tests `payments.pages.test.tsx`, `shipments.pages.test.tsx`, `sales.pages.test.tsx`, `logs.pages.test.tsx`; BA specs `docs/business/{Payment/AdminBackOffice,Shipping/ShipmentsBackOffice,Sales/CommissionPlansBackOffice,Reporting/LogsBackOffice}.md` |
| **Accounting set-up** | 5 files (tax rates, fiscal periods, chart of accounts, bank accounts + statements, `BankReconciliationModal`) | **on the library (U3 batch 9, 2026-11-02)** - worked examples: `BankReconciliationModal.tsx` (a `lg` dialog over another dialog with a `step` union driving three consequence confirms), `AccountsPage.tsx` (template dialog that opens its own confirm), `FiscalPeriodsPage.tsx` (date-order validation); smoke tests `accounting/accounting.pages.test.tsx`; BA spec `docs/business/Accounting/AdminSetup.md` |
| **Settings (25 pages) + Sign-in (3 pages)** | General ... Authentication, Feature flags, Licence, `PaymentGatewaysCard`, `ShippingCarriersCard`; Login, Two-factor, Twitter callback | **on the library (U3 part 2, 2026-11-02)** - worked examples: `CompanySettingsPage.tsx` (plain tenant form), `EmailSettingsPage.tsx` (System / Tenant tabs through `TabList`, sealed password), `FeatureFlagsPage.tsx` (platform toggles behind a consequence `ConfirmDialog`), `LoginPage.tsx`; smoke tests `settings/settings.forms.test.tsx` (table-driven over every page: loads, axe, every tab, Save sends one write + "Saved.", server refusal shown, read-only), `auth/auth.pages.test.tsx`; BA spec `docs/business/Identity/AdminSettingsScreens.md`. |
| **Accounting documents** | 3 pages + 4 shared parts (journal entries, bills, invoices with e-invoice and adjust-VAT dialogs, `PostingGapsBar`, `FxRevaluationPanel`, `ForeignCurrencyFields`, `VatLinesEditor`) | **on the library (U3 batch 10, 2026-11-02)** - worked examples: `BillsPage.tsx` (a `step` union driving an approve dialog with its optional VAT-rate field and a shared `PayDialog` that asks for the payment rate of a foreign document), `InvoicesPage.tsx` (`EInvoiceDialog`: a dialog with a confirm per step, nested `ReasonDialog`), `JournalEntriesPage.tsx` (labelled line grid, live balance status), `PageTabs.tsx` (tablist kept in the URL; its `TabList` is the controlled variant); smoke tests `accounting/postings.pages.test.tsx`; BA spec `docs/business/Accounting/AdminPostings.md`. **R71 is closed for dialogs; the 25 Settings pages and 3 sign-in pages (R92) and the numeric colour classes remain.** |


### U3 part 2 notes (colours, Settings, sign-in)
- **No raw colours.** `noRawColours.test.ts` fails the build on any `text|bg|border|ring|divide-<gray|red|green|amber|blue|...>-NNN`, any `primary-NNN`, `bg-white` / `text-white` / `bg-black` (alpha overlays `bg-black/5` excepted). Use `text-ink / ink-secondary / ink-muted`, `bg-surface / surface-muted / page`, `border-line / line-strong`, `brand-*`, `danger-* / success-* / warning-* / info-*`. The numeric aliases are removed from the Admin preset (storefront and mobile keep theirs).
- `text-ink-muted` is **#616876** (was #6b7280): it measured 4.39:1 on `surface-muted` and 4.21 on `brand-soft`; `build-tokens --check` now enforces ink-muted on page, surface-muted and brand-soft.
- `Checkbox` is a block (`flex w-fit`): two checkboxes in a form stack instead of running together; put them in a `flex gap-*` wrapper to sit side by side.
- A Settings form is `FormField` + `Input` / `Select` / `Checkbox`, `Button type=submit loading`, `Alert` with `serverMessage(error, fallback)`, and `toast.success(t('common.saved'))` in `onSuccess`. A page that keeps its own tab state uses `TabList`; tabs in the URL use `PageTabs`.
- Two codemods did 90 % of the work (colour map with a dry-run list of unmapped classes; label-wrapped inputs -> `FormField`); the table-driven test is what proves the requests did not change.

### Batch 2 notes (Products)
- `DynamicAttributeField` now takes a `label` and renders its own `FormField` (or a `fieldset` for multi-select, a labelled `Checkbox` for yes/no) so every attribute input has a linked label.
- A file drop zone is a `role="button"` with `tabIndex=0` and Enter / Space; the hidden `<input type="file">` sits **beside** it, never inside (axe: nested interactive control).
- `DataTable` names an empty column header (the actions column) "Actions" for screen readers (`sr-only`), which fixes axe's empty-table-header on every page that uses it.
- Links styled as buttons (`<Link className="btn-primary">`, 2 in Products) still use the old `.btn-*` classes: `Button` does not render as a link yet.
- Batch 2 found that Brands, Categories and Price lists had **no translations at all** (the screens showed raw keys); the new test `Products: translations` checks every key used by the folder exists in en and vi. Copy it into the next batches.

### Batch 3 notes (HRM)
- The HRM API files use `gatewayClient` (`api/client`), not `apiClient`: a test that mocks `@/api/client` must export **both**, or every query fails silently into the "No records found" state and the tests prove nothing (the first version of `hrm.pages.test.tsx` passed that way; the pay-slip test exposed it). The Master data / Products tests use `apiClient` only.
- `Modal` has no description line: put the person into the title (`Salary terms — Name (No.)`).
- A hand-made grid of inputs (insurance rates) names each input with an `aria-label` that combines row and column; dynamic rows (tax brackets) number them.
- `useEmployeePicker` now lives in `pages/hrm/useEmployeePicker.ts` (one query key, three pages).
- The codemod mangles a component that returns two different `fixed inset-0` blocks (`ProcessPayrollModal`: form step and result step): revert it and write that file by hand.
- Money-moving actions (process payroll, generate / pay / cancel a pay slip) were migrated without changing a payload or a confirmation step; the browser walk never executes them.

### Batch 4 notes (CRM)
- A page made of tabs keeps the active tab in the URL (`useSearchParams`, `?tab=`) and implements the tablist keyboard pattern (arrow keys, roving `tabIndex`, `aria-controls` / `aria-labelledby`); the Product form tabs still use the simpler version.
- A **confirm dialog's confirm button must not have the same name as Cancel or the dialog's X** (the first draft had two buttons called "Cancel" and two called "Close"): use the action's own title ("Close case", "Cancel activity").
- An action whose result the user cannot see (Qualify creates another record) gets a `ConfirmDialog` that names the side effect.
- A date input whose server rule is "in the future" proposes a valid default and has `min`.
- Stage counters that are also filters are real `<button aria-pressed>`s, not clickable cards.
- Walk lesson: **a cached list can hide a new row** (CRM caches the contact list 5 min without invalidation, R84) - the walk uses an existing contact and seeds anything the API cannot create (R82) by SQL, then deletes it.

### Batch 5 notes (Inventory / Warehouse)
- **A step that moves stock or locks something gets a `ConfirmDialog` that states the consequence** (not just "Are you sure?") and the confirm button carries the action's own name; the test asserts `post` was NOT called before the confirm click.
- One confirm dialog for several actions: keep a `step: { kind, order } | null` state and build the title / message keys from `kind` (`inventory.transfers.${kind}Title`); the translations test lists those dynamic keys by hand.
- `mutation.variables` gives a per-row loading state (`recordCountMutation.variables === line.productId`) without extra state.
- **The request enum must match the API.** The adjustment request sent `reason: 'Damaged'`; WareHouseService has no string-enum converter and answers 400, so no adjustment could be requested. Check a request enum against the API before trusting a migrated form (`ADJUSTMENT_REASON` in `stockAdjustments.api.ts`). `serverMessage` now also reads a ProblemDetails `errors` map, so a validation refusal shows its reason.
- A select labelled with a page title ("Warehouses") reads wrongly in a dialog: give a form its own label key (`inventory.locations.warehouse`).
- The walk lesson: never start a stocktake or approve anything on shared data without a cleanup plan (the walk starts and cancels one stocktake and deletes it by SQL).

### Batch 6 notes (Settings + Identity)
- **Hide, do not disable, the action you must not take on yourself:** the Users list hides Lock / Deactivate on the signed-in user's row (`useAuthStore` user id); the API does not refuse it, so this is a convenience, not security (R91).
- **A one-click save needs its own feedback.** The permission checkboxes of a role save on every tick: each tick toasts "Saved" or the server's reason, and the refetch puts a refused box back (`after = { onSuccess, onError }` shared by both mutations).
- **A list that is not a table can still be accessible:** the carrier / fee rule lists stay a divided list; every row button carries the row's name in `aria-label` ("Delete {name}"), tests find them by that name.
- **Client-side range checks** (`validate()` in the rule cards: minimum ≤ maximum, integer priority) use `FormField error` with a plain `useState` form (these forms never used react-hook-form); `noValidate` + the submit handler keep the linked-error behaviour.
- **Do not trust the screen's own wording about a feature** (the Invite dialog): read the service. Invite *adds an existing account* and e-mails nothing, so the dialog now says so (and the walk found the feature itself fails, R89).
- A tenant picker that lists other tenants' data does not mean the actions work there (R90); show the server's reason instead of staying silent.
- Axe: a dialog's inner heading must continue the heading order (`h3` under the dialog's `h2`, not `h4`).
- Walk lesson: the user list is paged (1 104 users in the platform tenant): the throw-away account sorts first by e-mail (`0u8check@…`), and a walk that moved to a later page must reload the route before looking for it.

### Batch 7 notes (Purchasing + Orders)
- **`Input` takes `error`, not `aria-invalid`.** Outside a `FormField` the control computes `aria-invalid` itself from the `error` prop and overwrites one passed in `rest`; an inline per-row input (receive quantity, price per line) needs `error={...}` and a message elsewhere (a `role="alert"` paragraph under the table).
- **A dialog that asks a second question stacks, it does not replace:** the Receive dialog stays open under its `ConfirmDialog`; the first dialog's Esc is switched off (`closeOnEsc={!receive.isPending && !confirming}`) so one Esc closes only the top one. In tests take the last `dialog` (`slice(-1)[0]`) and give the confirm button a different name from the one that opened it ("Record receipt" opens, "Receive into stock" confirms).
- **Inline-validated forms without react-hook-form:** a `validate()` that fills an `errors` object, `noValidate` + `onSubmit` (as in batch 6) and a submit button bound with `form="…"`; the footer submit and Enter both go through it.
- **`common.error` was a missing key (R79):** the shared fallback of `serverMessage` now exists in both languages and a test asserts the translated text, never the key.
- **Walk lesson (cost an hour):** headless Chrome started with `proc.kill()` can leave child processes holding the debugging port; the next run then talks to the OLD browser (old profile, old bundle) and every new dialog "fails". Kill leftovers (`chrome-uN-` in the command line) before a run and use a new port per script.
- Deploying: `deploy-changed.ps1` returns when the container is recreated; wait for its "Started" line before the walk (the first run hit the old page while the container was still being replaced).

### Batch 8 notes (Sales / Payments / Shipping / Logs)
- **A "Save" that sends money back is a two-step action:** the form validates (`noValidate` + own checks, linked errors), then a `ConfirmDialog` states amount and provider; only its confirm button (named differently from the form's) posts. Same pattern as Receive goods (batch 7).
- **Three dialogs deep (detail → split → confirm):** every layer below the top switches its Esc off (`closeOnEsc={!splitting && step === null}`), so one Esc closes exactly one dialog; render the layers as siblings, not nested (portals).
- **A grid state is written, not only coloured:** the split grid prints "n unit(s) are not assigned…" / "every parcel needs at least one unit" under the table (`aria-live="polite"`) and keeps the red "left" numbers as extra. Compute one `problem` string and show it.
- **A drawer becomes a wide `Modal`** (the library has no drawer): the long stack trace scrolls inside the body; rows open through a "View" button (keyboard) because a clickable `<tr>` is not reachable.
- `noValidate` is needed on react-hook-form forms whose inputs carry `min`/`max`: the browser (and jsdom) blocks the submit before the library shows its linked message.
- Test lookups: a row button named after the row ("Refund SO-1", "View GHN123") must be matched with a regexp when the name falls back to another field (no tracking code); a role of `Admin` in the fake user may bypass permission checks, so a "no permission" test uses `roles: []`.
- Walk: a button inside a table row is excluded from "main button" helpers; the empty-state Clear button lives in the table body, so look it up separately.

### Batch 9 notes (Accounting set-up)
- **Anything that books or locks the ledger asks first, in the user's words:** "A journal entry is booked", "the statement is locked", "takes no new postings; history stays". One `step` union (`{ kind: 'post' | 'delete'; line } | { kind: 'reconcile' } | null`) drives one `ConfirmDialog` so only one confirm can be open; the confirm button is named like the action but the opener carries the row's name in its `aria-label` ("Post as fee Monthly fee").
- **A dialog that opens its own confirm** (Apply template) renders both as siblings in a fragment, with `closeOnEsc={!confirming && !mutation.isPending}` on the lower one.
- **Focus when the opener is gone:** `Modal` returned focus to the element that was active when it opened. If that was the page body (a dialog opened without focus) or an element drawn again by a refetch, focus fell to `<body>` and Esc stopped working in the dialog below. `Modal` now hands the focus to the topmost dialog still open (`Modal.nested.test.tsx`). Found by the walk, not by tests: mouse clicks focus a button, the walk's `.click()` does not.
- **A last empty `<th />`** fails axe (empty-table-header): use `<span className="sr-only">{t('common.actions')}</span>`.
- **Immutable fields on an edit form:** disable the control and say why in the `hint` ("Cannot be changed after the account is created").
- Walk: `setFileInputFiles` through the DevTools DOM domain uploads a CSV into `<input type=file>`; SQL cleanup order for statements: lines, statements, bank accounts.

### How batch 1 was done (so the next batches repeat it)
The Master data pages were regular, so the conversion was scripted (field block `div > label + input/select + error <p>` -> `FormField`; the modal frame -> `Modal` with the footer buttons pulled out of the form; `btn-*` and text-link buttons -> `Button`; filter controls -> `Select` / `Input` with an `aria-label`; checkbox + label -> `Checkbox`; required fields without a visible error get one) and then reviewed by `tsc`, the smoke test (axe included) and a browser walk. Anything that did not match the pattern (the Employees terminate dialog with its confirmation) was edited by hand. `lib/serverMessage.ts` is the one helper for server messages (R73 closed).
