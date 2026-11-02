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
- **Lock** and **Deactivate** end every session of the user and revoke the user's tokens (`RevokeAllForUserAsync` + `UsersChanged`); the user cannot sign in while locked / deactivated. **Unlock** lets them sign in again. **Deactivate** is only offered while the user is active. The API now has **Activate** (`POST users/{id}/activate`, permission `identity:users:manage`, audit `AccountActivated`); the Admin button is a UI follow-up.
- The screen hides **Lock and Deactivate on your own row**: ending your own session from the list is never intended. Since 2026-11-02 the API refuses it too (section 4.6).
- **Edit profile** changes full name and user name. **Manage roles** assigns one role at a time (the list offers only the roles not yet held) and removes one at a time; each change revokes the user's tokens.
- **Invite** *adds an existing account to this tenant*; it does **not** create an account and sends **no e-mail**. Outcomes: account unknown → "No account found for e-mail …" (404); the same e-mail exists in several tenants → refused as ambiguous; already a member → "already a member"; otherwise the account is added to the caller's tenant with the optional role. Fixed 2026-11-02 (R89): the membership and the role are now inserted (they used to be saved as an UPDATE of rows that do not exist, HTTP 500).
- All actions apply to users **of the caller's tenant**. Since 2026-11-02 (R90) the tenant picker (platform operator) is **read-only for another tenant**: its users / roles are listed under an information notice, and Invite, Add, Edit, Lock, Deactivate, Activate, Manage roles and the role actions are not offered (the API would answer "not found"); choosing My Tenant brings them back, and the page returns to page 1 when the tenant changes. Acting across tenants would need a backend change (`[TenantRouteAccess]`), recorded as R102.
- **Activate** (2026-11-02) is offered on an inactive user's row to whoever holds `identity:users:manage` (not on your own row): a confirmation says the person can sign in again and keeps their roles; refusals appear as a toast.

### 4.2 Role
- Code is unique per tenant, **fixed after creation**; reserved platform codes (`PlatformRoles.IsReserved`) are refused; edit changes the name only.
- **Delete** removes the role and its assignments; **every holder loses the role's permissions at once** (their tokens are revoked). The confirmation says so. The API refuses deleting the default roles CUSTOMER / STAFF / USER (section 4.6).
- **Permissions** are granted / withdrawn **one tick at a time and saved at once**. A grant is refused (`PermissionNotGrantable`) when the permission is platform-only outside the platform tenant or the granter does not hold it ("only grant what you hold"); the refusal is shown as an error toast and the box returns to its previous state.

### 4.6 Account safety rules (R91, server-side, 2026-11-02)
An **administrator** of a tenant is an *active, unlocked member who holds a role that grants `identity:users:manage` in that tenant* - permissions decide, never role names. These actions are refused with HTTP 400 (business rule, message in the user's language, shown as is by the Admin) and change nothing:
| Action | Refused when | Message (en) |
|---|---|---|
| Lock / Deactivate a user | the target is **you** | "You cannot lock your own account." / "…deactivate…" |
| Lock / Deactivate a user; take a role from a user | the target is an administrator and **no other active, unlocked administrator** remains | "This would leave the shop without an administrator (…). Give another user that permission first." |
| Delete a role; remove `identity:users:manage` from a role | every current administrator holds that permission only through this role | same text |
| Delete a role | it is a default role (CUSTOMER, STAFF, USER) | "The default role 'X' cannot be deleted: new accounts and sign-ups depend on it." |
| Deactivate a tenant | it is the platform tenant | "The platform tenant cannot be deactivated: it holds the operators of every shop." |
A second administrator who is locked or inactive does not count; an administrator who keeps the permission through another role may lose one role. Unlocking and activating are never refused.

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
| ID-9 | A shop cannot lock itself out | Locking / deactivating yourself, the last administrator, deleting the only role that makes administrators, deleting a default role and deactivating the platform tenant answer 400 with the message of section 4.6; nothing is saved and no session is revoked |
| ID-10 | Invite works | An existing account of another tenant is added to the caller's tenant with the optional role (HTTP 200, `alreadyMember:false`), is found in that tenant's user list and can sign in there; a second invite answers `alreadyMember:true` |
| ID-11 | A deactivated user can come back | An inactive row offers Activate (permission `identity:users:manage`, never on your own row); it asks first, posts only after the confirm, shows the server's refusal as a toast and the row is Active afterwards |
| ID-12 | Another tenant is read-only | While the picker is on another tenant, Users and Roles show an information notice and no action button; My Tenant restores them |
| ID-8 | Accessible | Dialogs trap focus, scroll is locked, tab / label / group structure is exposed (fieldset per module), axe finds no violation |

## 6. Use cases
**UC-1 Give someone access:** an existing account → Users → Invite (e-mail, optional role) → success message; Manage roles to add more. *Exception:* unknown e-mail → "No account found…"; already a member → says so.
**UC-2 Remove access:** Users → Lock (confirm; reversible by Unlock) or Deactivate (confirm; **Activate** brings the user back, with a confirmation). Both end the user's sessions immediately.
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
- **R89 fixed 2026-11-02:** *Invite an existing account from another tenant* answered HTTP 500 because the detached account graph was saved with `Update` (new rows treated as modified). The invite now loads the account tracked and saves the new rows as inserts.
- **R90 fixed 2026-11-02** (read-only other tenant, see 4.1). Cross-tenant actions by a platform operator remain a possible backend feature (R102).
- **R91 fixed 2026-11-02** (section 4.6). The **Activate** button exists since 2026-11-02. Still open: a role create with a code that already exists answers 500 instead of a business-rule 400 (R99).
- **R92:** 25 other Settings pages and the 3 sign-in pages still use the pre-library classes (no dialogs): a later batch.

## 12. UAT scenarios
1. Users → your own row has no Lock / Deactivate; another user's row has both.
2. Lock another user → the text says they are signed out everywhere → Esc → still unlocked; confirm → "Locked" badge; Unlock → badge gone.
3. Manage roles → assign one → one more role listed; Remove → one fewer; error (for example a role already held) shows the server's reason.
4. Invite with an empty e-mail → "Bắt buộc"; with an unknown e-mail → "No account found for email …"; with an account already in the tenant → "already a member".
5. Roles → Add (code, name) → Permissions → tick two → two "Đã lưu." toasts and "2 of n granted"; filter "zzz" → "Không có kết quả" + Xóa lọc.
6. Delete the role → "Everyone who holds it loses its permissions at once" → Esc keeps it; confirm removes it.
13. Users → Deactivate a throw-away user (confirm) → the row shows Inactive and **Activate** → Activate asks "…can sign in again" → Esc keeps Inactive → confirm → Active. As a platform operator pick another tenant → notice, no buttons; My Tenant → buttons.
7. Tenants → Add → Deactivate → confirm names the tenant → Esc keeps Active; confirm → Inactive; Activate → Active.
8. Carrier rules → Add → minimum weight 900, maximum 100 → refused in the form; fix → saved → Edit → Delete → "New orders no longer use it" → Esc keeps the rule; confirm removes it. Repeat for Delivery fees.
10. (API) Invite an account of another tenant with a role → 200; invite again → "already a member"; the person appears in the Users list with the role.
11. (API) Lock your own id → 400 "You cannot lock your own account."; as the only administrator lock/deactivate/remove the admin role → 400; with a second active, unlocked administrator the same actions succeed.
12. (API) Delete the role CUSTOMER → 400; deactivate the platform tenant → 400; deactivate then activate a non-administrator → active again.
9. Sent notifications → View a message → message, error with retries, log; search with no match → "Không có kết quả" + Xóa lọc.
