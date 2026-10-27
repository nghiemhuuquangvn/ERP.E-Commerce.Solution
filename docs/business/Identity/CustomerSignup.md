# Customer sign-up and shop access (storefront and mobile)

> Business analysis, 2026-10-27 (risk clean-up before P1-11). Owner: Identity. Related: `MultiTenantLogin.md` (tenant = request host), `docs/business/Sales/OrderProcessing.md`, `docs/business/Payment/OnlinePayments.md`, `docs/frontend/mobile-apps.md`.

## 1. Purpose and scope
A shopper creates an account on a shop's storefront (web or mobile app), signs in, and sees their own orders. A browser walk-through of the storefront found that this chain did not work end to end: a new shop had no customer role, the register form was refused, a second shopper with the same name caused a server error, and a signed-in customer was refused their own order list. This document fixes the expected behaviour. Out of scope: staff and administrator accounts (created by invitation), external login (Google, Microsoft, Facebook, X), password reset.

## 2. Actors
| Actor | Role in this process |
|---|---|
| Shopper (customer) | Registers, signs in, places and follows orders |
| Shop administrator | Owns the tenant; can grant permissions to roles |
| Platform operator | Creates tenants (`POST /tenants`) or lets a company sign up (`tenant-signup`) |
| Gateway | Resolves the tenant from the request host; the client never names it |

## 3. Process
1. A tenant is created (by the operator or by self sign-up). **The system gives it the default roles CUSTOMER, STAFF and USER at once** (and ADMIN with sign-up).
2. The shopper opens the shop's storefront host (`<code>.shop.erp.local` or the shop's own domain) and registers with e-mail, password and a display name.
3. The system creates a Customer account in that tenant, assigns CUSTOMER, and signs the shopper in.
4. The shopper places orders and opens *My orders* and the order confirmation.

## 4. Requirements
| ID | Requirement | Acceptance |
|---|---|---|
| FR-C1 | Every tenant has CUSTOMER, STAFF and USER from its first day. | Create a tenant (either way) → the three roles exist for it. Existing tenants were repaired by migration Identity 030 (idempotent). |
| FR-C2 | Registration needs no tenant code from the client. | `POST /auth/register` with e-mail, password and name only → 200; the tenant is the request host. |
| FR-C3 | Registration always creates a Customer, whatever the client sends. | A body with another user type still creates a Customer (existing rule C1). |
| FR-C4 | The account name is the e-mail; the given name is only the display name. | Two shoppers both named "Shop Walker" register without error; the e-mail is unique inside the tenant (existing rule). The same e-mail may exist in two shops. |
| FR-C5 | A shop whose default role is missing refuses registration with a clear business error, never a server error. | Delete the CUSTOMER role of a test tenant → register → 400 "This shop is not ready for customer sign-up". |
| FR-C6 | A customer sees their own orders and the confirmation of their own order without holding any permission. | `GET orders/my` → 200 with only their orders; `GET orders/{id}` of their order → 200. |
| FR-C7 | A customer cannot see another customer's order, and cannot tell whether it exists. | `GET orders/{id}` of someone else's order → 404. Staff with `sales_order:read` still see any order. |

## 5. Business rules
- BR-C1 The tenant of an anonymous request is the request host; a tenant code in the body is ignored (overwritten by the controller).
- BR-C2 The default roles carry **no permissions** (same as the platform tenant). Customer features work through "own resource" checks, not permissions; a new customer endpoint must do the same (DEFERRED R21).
- BR-C3 The user (account) name is unique per tenant, not across tenants (migration Identity 031).
- BR-C4 Default roles are created only if missing; running the seeding again changes nothing.

## 6. Use cases
**UC-C1 Register.** Main: shopper fills the form → signed in, taken to the home page, name shown in the header. Alternative: e-mail already used in this shop → 409 "already exists". Exception: shop without CUSTOMER role → 400 (FR-C5).
**UC-C2 Follow my orders.** Main: shopper opens *My orders* → list (number, date, status, total) → opens an order. Alternative: no orders → "You haven't placed any orders yet". Exception: another customer's order id → "not found".
**UC-C3 New tenant.** Main: company signs up → tenant, ADMIN role + first administrator, and CUSTOMER / STAFF / USER → its storefront accepts registrations immediately.

## 7. Data and state
`Roles (TenantId, RoleCode, RoleName, IsSystem, IsActive)`: one row per tenant and code. `Users.UserName`: unique per `(TenantId, UserName)` (was global `IX_Users_UserName`); `Users.FullName`: the display name. No new tables.

## 8. Integrations
None added. Sales answers `orders/my` and `orders/{id}` from the caller's id in the token. The Gateway gives the tenant from the host.

## 9. Screens
- **Storefront → Register:** first name, last name, e-mail, password; on success the header shows the full name. **My orders** (`/account/orders`): one card per order with number, date, status badge and total (the list no longer shows an item count: the list does not carry the lines). **Profile** shows full name and e-mail.
- **Mobile → Register / Orders / Order:** same data; the user's name comes from `fullName`.

## 10. Permissions
No permission is needed for the customer's own data. `sales_order:read` lets staff read any order. Administrators grant permissions to roles in *Identity → Roles*.

## 11. Assumptions and open issues
- One account per e-mail per shop; the same person registers separately in each shop.
- CUSTOMER has no permissions by design today (DEFERRED R21). A tenant admin from sign-up holds only Identity / System permissions (R24).
- The walk-through used the local `glcheck` tenant over plain http; production uses https.

## 12. UAT scenarios (live results on `glcheck`, 2026-10-27)
| ID | Scenario | Result |
|---|---|---|
| UAT-C1 | Migration 030 on the three existing tenants | each has CUSTOMER, STAFF, USER |
| UAT-C2 | Create a tenant through tenant-signup, register a customer on its host | roles ADMIN, CUSTOMER, STAFF, USER; registration 200 |
| UAT-C3 | Register through the storefront form, twice with the same display name | both succeed; header shows "Shop Walker" |
| UAT-C4 | *My orders* as a new customer | "You haven't placed any orders yet" (was 403) |
| UAT-C5 | Another customer's order id | 404 (unit-tested; staff with read permission → 200) |
| UAT-C6 | Admin bell hub | negotiate 200 (was 401 on every page) |
