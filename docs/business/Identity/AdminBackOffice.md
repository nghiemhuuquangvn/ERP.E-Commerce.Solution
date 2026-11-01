# Identity — Admin back office (Users, Roles, Tenants) and the Settings rule screens

Business analysis of the Admin screens as shipped on 2026-11-01 (ROADMAP P2-9, U3 batch 6): **Users**, **Roles & permissions**, **Tenants** (IdentityService), plus the three Settings screens that still had their own dialogs: **Carrier selection rules**, **Delivery fee rows** (ShippingService, see [CarrierIntegration.md](../Shipping/CarrierIntegration.md) and [ShippingFee.md](../Shipping/ShippingFee.md)) and **Sent notifications** (NotificationService, see [WebhooksAndTemplates.md](../Notification/WebhooksAndTemplates.md)). Sign-in, 2FA, external login and the auth settings are described in [module-02-identity-security.md](module-02-identity-security.md), [ExternalAuthAndTwoFactor.md](ExternalAuthAndTwoFactor.md) and [MultiTenantLogin.md](MultiTenantLogin.md); the security rules this screen must respect are in [SecurityReview-2026-10.md](SecurityReview-2026-10.md). Vietnamese user guide: [docs/user-guides/identity.md](../../user-guides/identity.md) and [shipping-settings.md](../../user-guides/shipping-settings.md).

## 1. Purpose and scope
Let a shop administrator decide **who may sign in to the shop and what they may do** (users, roles, permissions), let the platform operator create and switch off shops (tenants), and let the shop decide **which carrier takes a parcel and what the customer pays for delivery** (two rule lists), and read **what the system sent to customers** (history). In scope: the screens above and the API calls behind them. **Out of scope today (no screen):** creating a user account from the Admin (accounts are created by sign-up / external login; the Admin can only *invite an existing account*), re-activating a deactivated user, resetting another user's password, tenant domains and licence (the Licence page exists separately), API keys, permission catalogue editing (read-only by design).

## 2. Actors and permissions
| Actor | Does | Permission |
|---|---|---|
| Shop administrator | Lists users of the shop; edits a profile; locks / unlocks; deactivates; assigns and removes roles; invites an existing account | `identity:users:manage` (invite, lock, roles), `identity:users:edit` (profile), `identity:users:delete` (deactivate) |
| Shop administrator | Creates, renames, deletes roles; ticks permissions of a role | `identity:roles:create`, `:edit`, `:delete`, `:manage` (permissions) |
| Platform operator | Lists / creates / renames / activates / deactivates tenants; reads the users and roles of another tenant through the tenant picker | `identity:tenants:view`, `:create`, `:edit`, `:manage` (effective only in the platform tenant) |
| Shipping manager | Maintains carrier rules and delivery fee rows, tries a parcel | shipping settings `manage` (the card receives `canManage`) |
| Support / operations | Reads the notification history and one message's log | notification history view |
A button appears only with its permission. The platform-only permissions are listed in `PermissionScopes` and are stripped from every token outside the platform tenant.

## 3. Process overview
```
Account (sign-up / external login) ──invite──> Tenant membership ──assign──> Role ──holds──> Permissions
Lock / Deactivate / role change / permission change / tenant switch-off  ──>  sessions and tokens revoked at once (all services + Gateway)
Order ──> Carrier rule list (first match by priority, else cheapest quote) ──> parcel booked
Order ──> Fee row list (first match by priority) else flat fee ──> delivery fee the customer pays
Every message the system sends ──> Sent notifications (status, error, delivery log)
```

## 4. State models and rules
### 4.1 User (`Active ⇄ Deactivated`, independently `Unlocked ⇄ Locked`)
- **Lock** and **Deactivate** end every session of the user and revoke the user's tokens (`RevokeAllForUserAsync` + `UsersChanged`); the user cannot sign in while locked / deactivated. **Unlock** lets them sign in again. **Deactivate** is only offered while the user is active; there is **no Activate in the Admin** (open issue).
- The screen hides **Lock and Deactivate on your own row**: ending your own session from the list is never intended. The API itself does not refuse it (open issue R91).
- **Edit profile** changes full name and user name. **Manage roles** assigns one role at a time (the list offers only the roles not yet held) and removes one at a time; each change revokes the user's tokens.
- **Invite** *adds an existing account to this tenant*; it does **not** create an account and sends **no e-mail**. Outcomes: account unknown → "No account found for e-mail …" (404); the same e-mail exists in several tenants → refused as ambiguous; already a member → "already a member"; otherwise the account is added to the caller's tenant with the optional role. (Known defect: see R89.)
- All actions apply to users **of the caller's tenant**; the tenant picker (platform operator) lists another tenant's users but the API answers "user not found" for an action on them (R90); the dialog now shows that reason.

### 4.2 Role
- Code is unique per tenant, **fixed after creation**; reserved platform codes (`PlatformRoles.IsReserved`) are refused; edit changes the name only.
- **Delete** removes the role and its assignments; **every holder loses the role's permissions at once** (their tokens are revoked). The confirmation says so. The API does not refuse deleting the default roles CUSTOMER / STAFF / USER (R91).
- **Permissions** are granted / withdrawn **one tick at a time and saved at once**. A grant is refused (`PermissionNotGrantable`) when the permission is platform-only outside the platform tenant or the granter does not hold it ("only grant what you hold"); the refusal is shown as an error toast and the box returns to its previous state.

### 4.3 Tenant (`Active ⇄ Inactive`)
Code fixed after creation. **Deactivate** blocks every user of the tenant in every service at once (`TenantChanged`); **Activate** restores access. Both ask for confirmation; the text names the tenant. A new tenant receives the default roles CUSTOMER / STAFF / USER (no permissions).

### 4.4 Carrier selection rule / Delivery fee row
Rules are tried by **ascending priority**; only **active** rows count. *Carrier rule:* matching fields are all optional (provinces list, weight band, order-value band, COD / prepaid) and the first match chooses the carrier; with no match the **cheapest quote** wins; a carrier that refuses passes the parcel on. *Fee row:* the first active row matching province and weight sets the delivery fee; a parcel no row matches pays the **flat fee** of the tenant tab; goods above the free-delivery threshold are free. "Try a parcel" runs the same rule but books / charges nothing. **Delete** affects **future orders only**; parcels already booked and orders already placed are unchanged. The confirmation says so (new in this batch: delete used to be immediate).

### 4.5 Sent notification (`Queued → Sending → Sent → Delivered`, or `Failed` / `Bounced`)
Read-only list filtered by status, channel and a text search (recipient e-mail or subject); the detail dialog shows the message, the error with the retry count and the delivery log.

## 5. Requirements with acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| ID-1 | An action that ends a session or removes access is never one click | Lock, Deactivate (user), Delete role, Deactivate tenant, Delete carrier rule, Delete fee row open a dialog that states the consequence; **no request is sent until confirmed**; Esc / Cancel change nothing |
| ID-2 | You cannot lock or deactivate yourself from the list | Your own row shows Edit and Manage roles only; other rows show Lock and Deactivate (with permission) |
| ID-3 | Every dialog validates before sending | Required fields starred with a linked "Bắt buộc" error (`aria-invalid` + `aria-describedby`); rule forms also refuse a minimum above a maximum and a non-integer priority, in the form |
| ID-4 | Failures are explained | The server's reason (or the first validation message) appears in the dialog or as an error toast; a silent failure no longer exists on these screens |
| ID-5 | Invite is honest | The dialog says the person must already have an account and that nothing is e-mailed; the server's refusal (unknown / ambiguous / already a member) is shown verbatim |
| ID-6 | Permission ticks give feedback | Each tick shows "Đã lưu." or the server's reason and the box matches the server; a text filter narrows the list; each module shows "n of m granted" |
| ID-7 | Lists are usable | Empty states; Sent notifications: filter with no result → "Không có kết quả" + Xóa lọc; row buttons carry the row's name in their accessible name |
| ID-8 | Accessible | Dialogs trap focus, scroll is locked, tab / label / group structure is exposed (fieldset per module), axe finds no violation |

## 6. Use cases
**UC-1 Give someone access:** an existing account → Users → Invite (e-mail, optional role) → success message; Manage roles to add more. *Exception:* unknown e-mail → "No account found…"; already a member → says so.
**UC-2 Remove access:** Users → Lock (confirm; reversible by Unlock) or Deactivate (confirm; no Activate in the Admin). Both end the user's sessions immediately.
**UC-3 Define a role:** Roles → Add (code, name) → Permissions → tick; a refused grant shows its reason. *Alternative:* filter permissions by text. **Delete role:** confirm; holders lose the permissions.
**UC-4 Open or close a shop (platform):** Tenants → Add (code, name); Deactivate / Activate with confirmation.
**UC-5 Choose the carrier by rule:** Settings → Shipping → Carrier rules → Add (name, priority, carrier, provinces, bands, payment) → Try a parcel to see which rule wins → Edit / Delete (confirm).
**UC-6 Set delivery prices:** Settings → Shipping → Delivery fees → add rows by province × weight → Try a parcel → the flat fee on the Tenant tab covers the rest.
**UC-7 Investigate a message:** Settings → Sent notifications → filter → View → read the error, retries and log.

## 7. Data model (screen view)
User(Id, Email, UserName, FullName, IsActive, IsLocked, LastLoginAt, Roles[], RoleAssignments[RoleId, RoleCode, RoleName]) · Role(Id, RoleCode, RoleName, IsActive) · Permission(Id, PermissionCode, PermissionName, Module) · Tenant(Id, TenantCode, TenantName, Status 1/0) · CarrierRule(Name, Priority, IsActive, MatchProvinces, Min/MaxWeightGrams, Min/MaxOrderValue, CodOnly, CarrierCode) · ShippingFeeRule(Name, Priority, IsActive, MatchProvinces, Min/MaxWeightGrams, Fee) · SentNotification(RecipientEmail, Channel, Subject, Body, Status, RetryCount, ErrorMessage, Logs[]). Every row carries the tenant.

## 8. Integrations
Identity changes reach the shared revocation store through `IUnitOfWork.Revocations` after commit: `UsersChanged` (password, lock, deactivation, role assign / remove, role deletion), `TenantChanged` (tenant status, licence); every service, the Gateway and the hubs reject revoked tokens. Role assignments publish `RoleAssigned` and permission grants `PermissionGranted` on `identity-topic` (consumed by Notification). Carrier and fee rules are read by ShippingService when Sales asks for the fee and when a parcel is booked; the notification history is NotificationService's own data.

## 9. Reports
None (lists only). Security-relevant actions are written to `SecurityAuditLogs` (account locked, user invited …); the walk-through left such audit rows, which are deliberately not deleted.

## 10. Assumptions
A user belongs to one home tenant and can be a member of others (`UserTenants`); the platform tenant is the only one that sees the tenant picker; role codes and tenant codes are typed in capitals by convention (not enforced).

## 11. Open issues (also in DEFERRED-ISSUES §6)
- **R89 (backend, feature does not work):** *Invite an existing account from another tenant* answers HTTP 500 — `UserService.InviteToTenantAsync` throws `DbUpdateConcurrencyException` at `UpdateAsync` (the new `UserTenant` row is treated as modified). Only the "unknown e-mail" and "already a member" answers work; the dialog now shows the server's generic message instead of an invented one.
- **R90:** the tenant picker lists another tenant's users, but profile edit, lock, deactivate and role changes answer "user not found" (they take the caller's tenant). Either hide those buttons when the picker is on another tenant or let the platform operator act across tenants.
- **R91:** the API does not refuse locking or deactivating yourself or the last administrator, deleting the default roles CUSTOMER / STAFF / USER, or deactivating the platform tenant; the screen only hides your own row. There is no Activate for a deactivated user.
- **R92:** 25 other Settings pages and the 3 sign-in pages still use the pre-library classes (no dialogs): a later batch.

## 12. UAT scenarios
1. Users → your own row has no Lock / Deactivate; another user's row has both.
2. Lock another user → the text says they are signed out everywhere → Esc → still unlocked; confirm → "Locked" badge; Unlock → badge gone.
3. Manage roles → assign one → one more role listed; Remove → one fewer; error (for example a role already held) shows the server's reason.
4. Invite with an empty e-mail → "Bắt buộc"; with an unknown e-mail → "No account found for email …"; with an account already in the tenant → "already a member".
5. Roles → Add (code, name) → Permissions → tick two → two "Đã lưu." toasts and "2 of n granted"; filter "zzz" → "Không có kết quả" + Xóa lọc.
6. Delete the role → "Everyone who holds it loses its permissions at once" → Esc keeps it; confirm removes it.
7. Tenants → Add → Deactivate → confirm names the tenant → Esc keeps Active; confirm → Inactive; Activate → Active.
8. Carrier rules → Add → minimum weight 900, maximum 100 → refused in the form; fix → saved → Edit → Delete → "New orders no longer use it" → Esc keeps the rule; confirm removes it. Repeat for Delivery fees.
9. Sent notifications → View a message → message, error with retries, log; search with no match → "Không có kết quả" + Xóa lọc.
