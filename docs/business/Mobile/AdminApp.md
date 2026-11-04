# ERP Admin mobile app - business analysis

Version 1.0, 2026-11-04. Technical companion: `docs/frontend/mobile-admin-app.md`. Screen elements: `docs/frontend/mobile-admin-ui-elements.md`. Verification record: section 13.

## 1. Purpose
Managers and approvers act on work that waits for them (purchase orders, sales orders, leave requests) and watch the sales figures from a phone, without the web back office. One code base, one installable app **per tenant** (default tenant SYSTEM), built so more modules can be added.

## 2. Scope
In: sign-in (including the two-step code), Home (sales figures and "needs your action"), Purchasing, Sales, HR (employees, leave requests, attendance), language (en / vi), sign out, tenant build.
Out (first release): creating or editing orders, hiring / terminating / transferring staff, payroll, shipping steps (pick, ship, return), warehouse and accounting modules, push notifications, offline writes, biometric unlock, tablet layout.

## 3. Actors
| Actor | Needs |
|---|---|
| Approver (purchasing / finance manager) | see submitted purchase orders; approve, finance-approve, reject with a reason, cancel |
| Sales manager | see sales orders; approve, reject, cancel with a reason; spot flagged orders |
| HR manager | see employees and attendance; approve, reject, cancel leave |
| Executive | Home figures only |
| Platform operator | the SYSTEM build; sees the platform tenant's data |
| Shop administrator | installs the build made for their tenant |

## 4. Requirements and acceptance criteria
| # | Requirement | Acceptance |
|---|---|---|
| R1 | Sign in with the tenant's own account | wrong password and unreachable server give different messages; the account belongs to the tenant of the build |
| R2 | Two-step code when the account has it | six digits only; resend after 30 s; a wrong code is refused with the server's sentence |
| R3 | Stay signed in | after closing the app the session is restored by one refresh; a refused token returns to sign-in; an unreachable server keeps the token |
| R4 | See only what the account may use | a tab appears only when the account holds one of the module's permissions and the tenant licence includes the module |
| R5 | Home | revenue, orders, average order for 7 / 30 / 90 days, revenue-by-day chart, counts of items waiting for this user with a link to the filtered list; "no access" without `reports:sales:view` |
| R6 | Lists | status filter, loaded-rows search, endless paging, pull to refresh, empty / error / offline / no-access states |
| R7 | Approve / reject / cancel | confirm sheet first; reject and cancel (and the sales cancel) need a reason; the server's refusal is shown and nothing changes; success shows a message and refreshes the record and the lists |
| R8 | Buttons follow status and permission | e.g. finance approval only in "Pending finance", for `purchase_order:finance_approve` |
| R9 | Tenant build | `TENANT_CODE` sets the address, name and package; two tenants install side by side; production builds refuse plain http |
| R10 | Extensible | a new module needs a folder, one registry line, route files and translations; a test fails otherwise |
| R11 | Accessible | targets from 44 px, labels on every control, status never by colour alone, live regions for errors |

## 5. Business rules
1. Authority comes from the **permissions in the token**, never from a role name. The server decides again on every call; the app only hides what cannot work.
2. A purchase order has two approval steps: `approve`, then `finance-approve` while "Pending finance approval". Reject uses the approve permission.
3. Sales order actions: `approve` / `reject` need `sales_order:approve`; `cancel` needs `sales_order:cancel` and a reason (the server stores it).
4. Leave: only a Pending request is approved or rejected; an Approved one can be cancelled; all need `hrm:leave:manage`.
5. Names on leave and attendance rows need `hrm:employees:view`; without it the row shows its own number.
6. Home counts: sales orders and leave use the server total; purchase orders are counted from the first 100 rows because the list gives no total.
7. Sign-out clears the session on the phone and tells the server; the refresh token is the only secret stored (keystore).

## 6. Use cases
**UC1 Approve a purchase order.** Main: Purchasing > "Submitted" > open PO > Approve > confirm > message "PO-x approved", status refreshes. Alternative: PO is in "Pending finance" > only "Finance approve" and Reject show. Exception: someone else approved it first > the server refuses, its sentence is shown in the sheet, the sheet stays open; no permission > "Your account is not allowed to do this".
**UC2 Reject with a reason.** Reject > the sheet asks for a reason > empty reason is refused locally > confirm sends it.
**UC3 Cancel a sales order.** Same as UC2 with the "Cancel order" button; allowed for Submitted / Approved / Picked.
**UC4 Decide a leave request.** HR > Leave > Pending > open > Approve or Reject (reason).
**UC5 Open work from Home.** Home > "3 purchase orders to approve" > the list opens on "Submitted".
**UC6 Sign in with two steps.** Email + password > code screen > six digits > Home. Alternative: wrong code > message, retry; resend after 30 s.
**UC7 Build for a tenant.** Operator runs the build with `-Tenant <code> -ApiUrl <tenant host>`; installs the APK; the header shows the tenant.

## 7. Data and state
No data is stored by the app except the refresh token (keystore) and the chosen language. Status sets: PO `DRAFT, SUBMITTED, PENDING_FINANCE_APPROVAL, APPROVED, ORDERED, PARTIALLY_RECEIVED, RECEIVED, REJECTED, CANCELLED`; SO `Draft, Submitted, Approved, Picked, Shipped, PartiallyShipped, Delivered, Rejected, Cancelled, ReturnRequested, Returned`; leave `Pending, Approved, Rejected, Cancelled`. Tone table: UI elements doc section 5.

## 8. Integrations
Through the tenant host and the Gateway: Identity (`auth/login`, `2fa/verify`, `2fa/resend`, `refresh`, `logout`), Reporting (`reports/sales`), PurchaseOrder, SaleOrder, HRM. No new backend endpoints. Two Sales fixes made for this app (list status filter applied before paging, order number on list rows) - section 11.

## 9. Permissions
`reports:sales:view`; `purchase_order:read, approve, finance_approve, cancel`; `sales_order:read, approve, cancel`; `hrm:employees:view, hrm:leave:manage, hrm:attendance:view`. Licensed module codes: PO, SO, HRM (Home needs none).

## 10. Reports
None of its own. Home shows the Reporting sales dashboard.

## 11. Findings in the existing backend (fixed or recorded)
- **Fixed** (`GET /orders`): the status / customer filter was applied to a page of all orders after paging while the total counted every match, so a status filter could show an empty first page with hundreds of matches; the list rows also had no order number. The filter is now part of the query (`ListOrdersTests`).
- **Recorded** (R123): `GET /po/api/v1/purchase-orders` answers a bare array without a total (the Admin web types say otherwise); the app accepts both and treats a full page as "maybe more".

## 12. UAT scenarios
1. Install the SYSTEM build, sign in as the platform administrator: Home shows figures; the four tabs show.
2. Install the glcheck build next to it: both icons exist; the header chip shows `glcheck`; the glcheck account cannot sign in to the SYSTEM build.
3. Open Sales > Submitted: only submitted orders show, each with its number.
4. Open a Submitted order as a user without `sales_order:approve`: no Approve / Reject.
5. On a throw-away order press Reject with an empty reason: refused; with a reason: status changes (**the owner presses; the assistant does not act on real data**).
6. Turn the network off on a list: saved rows stay with an offline banner, nothing is lost.
7. Sign out and reopen: back at sign-in.

## 13. Verification record
2026-11-04, Pixel 6 / Android 14 emulator (erp-pixel), Docker stack, **read-only** (no approve / reject / cancel was pressed; the assistant never acts on real data).
- **Install:** the SYSTEM (`com.erp.admin`) and glcheck (`com.erp.admin.glcheck`) APKs install side by side; each shows its own tenant chip on sign-in and in every header.
- **Contract (script, both tenants):** login `userType 2` answers tokens (no 2FA on these accounts); the token carries 194 / 267 permissions and 16 licensed modules; all eight calls of the app answer 200; refresh answers 200. Findings: PO list is a bare array (R123); the Sales list filter bug (section 11).
- **glcheck admin:** sign-in, Home (revenue $8,170,004.00, 73 orders, chart, "nothing is waiting"), Sales list (3 draft orders with numbers), a draft order detail (no action buttons), Purchasing and HR empty states, More (tenant, server, version), language switch to Vietnamese, sign-out confirm, signed-out state kept after a restart, **session restored after force-stop** (one refresh).
- **SYSTEM superadmin:** Home with "116 sales orders to approve" and "18 leave requests waiting" (the purchase-order line is absent because no order is submitted); the 116 link opens Sales on Submitted rows; a Submitted order shows Approve / Reject / Cancel order; the Reject sheet opens with a required Reason, closed with Back; Purchasing lists the 3 orders (bare-array answer); HR > Leave lists pending requests with names.
- **Refusals:** the glcheck account on the SYSTEM build is refused ("Unauthorized." from the server; the screen now says "Wrong email or password." for a plain 401). A wrong password shows the server's refusal.
- **Not verified:** a real approve / reject / cancel, the two-step code with a real account, TalkBack, a real phone, dark mode.
- **Fixes found by the check:** PO list shape, Sales list filter + order number, "Unauthorized." wording, "1 days" plural.


## 14. Assumptions and open issues
Home purchase-order count is capped at 100; the list search narrows loaded rows only (no server text search for PO / SO); the two-step code is covered by tests, not by a real 2FA account; the screen reader pass (TalkBack) is not done; the store release (signing, icon per tenant, `TENANT_ICON`) is not done. Registered in DEFERRED-ISSUES R123-R127.
