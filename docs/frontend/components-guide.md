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

## Migration status (U3)
| Area | Pages with own modals | Status |
|---|---|---|
| Pilot (U2) | 10 | on the library |
| **Master data** | 17 pages / 25 dialogs | **on the library (U3 batch 1, 2026-11-01)** - worked example: `pages/masterdata/DepartmentsPage.tsx`; smoke test `masterdata.pages.test.tsx` |
| Accounting 7, Products 6, HRM 6, CRM 5, Inventory 4, Settings 3, Purchasing 3, Identity 3, Orders 2, Sales / Payments / Shipping / Logs 4 | ~46 files | still old (DEFERRED R71); one batch per plan |

### How batch 1 was done (so the next batches repeat it)
The Master data pages were regular, so the conversion was scripted (field block `div > label + input/select + error <p>` -> `FormField`; the modal frame -> `Modal` with the footer buttons pulled out of the form; `btn-*` and text-link buttons -> `Button`; filter controls -> `Select` / `Input` with an `aria-label`; checkbox + label -> `Checkbox`; required fields without a visible error get one) and then reviewed by `tsc`, the smoke test (axe included) and a browser walk. Anything that did not match the pattern (the Employees terminate dialog with its confirmation) was edited by hand. `lib/serverMessage.ts` is the one helper for server messages (R73 closed).
