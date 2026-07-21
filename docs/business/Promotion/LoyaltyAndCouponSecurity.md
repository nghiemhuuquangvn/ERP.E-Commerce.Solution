# Loyalty & Coupon Security Model

**Service:** PromotionService (Port 5012, Code: PRM)

## Context

A platform-wide authentication/authorization audit (2026-07-21) found
`LoyaltyController.Earn`/`Redeem` accepted a caller-supplied `userId` with no
auth check and no ownership check at all — any caller (staff, customer, or
even unauthenticated) could mint or spend loyalty points for any other user by
simply passing a different `userId`. This is the most severe finding of that
audit. Fixing it required deciding what the *intended* business model actually
is, since the code gave no indication either way.

## What was found before deciding the fix

Checked every frontend app (`src/frontend/storefront`, `src/frontend/mobile/customer`,
`src/frontend/admin`) for real call sites of the Loyalty endpoints
(`/loyalty/{userId}/earn|redeem|balance`) — **none exist**. No customer-facing
checkout flow lets a customer redeem their own loyalty points today, and no
staff UI calls these endpoints either. The endpoints exist in the backend with
no consumer anywhere in this codebase yet.

## Model chosen

Given no self-service flow exists to preserve, **Loyalty Earn/Redeem/GetBalance
are staff-only actions**, gated by `promotions:manage` (the existing
staff-only permission) — a staff member (e.g. customer service) acts on a
specific customer's account on their behalf; there is no path for a customer
to mint or spend their own points directly today.

**If a customer self-service flow is built later** (e.g. "redeem my points at
checkout"), it must NOT simply relax this permission check. It needs a
separate code path that requires the caller's own JWT identity to match the
target `userId` (self-service only), with the current staff-only path kept
as-is for admin adjustments — mirroring the pattern used for
`docs/business/Notification/NotificationPrivacy.md`'s ownership check. Do not
widen `promotions:manage` to cover both cases.

## Coupon validate vs. redeem — a different, already-real split

Unlike Loyalty, coupon endpoints on `PromotionsController` DO have real
frontend callers (Storefront checkout), so the fix here reflects an existing,
working flow rather than inventing one:

- **`validate-coupon`** — public, `[AllowAnonymous]`. Read-only, no state
  change, and the Storefront's `/cart` page (where a customer checks a coupon
  code) has no auth guard — customers can be pre-login at this point.
- **`redeem-coupon`** — requires the caller to be authenticated
  (`_userContext.IsAuthenticated`), but needs **no staff permission**. It's a
  real state change (consumes the coupon), but it's the customer's own action
  during their own checkout — the Storefront's `/checkout` page always
  requires login and passes `authStore.user.id`, never a user-suppliable
  field, so this cannot be abused to redeem on someone else's behalf.
- **`list`/`get`/`applicable`** on `PromotionsController`, and `list`/`get` on
  `FlashSalesController` — staff-facing catalog reads, gated by the new
  `promotions:view` permission (Admin/Manager/Viewer).
- **`FlashSalesController.Reserve`** — a real stock-decrementing write with no
  current frontend caller either (same situation as Loyalty). Gated
  conservatively behind `promotions:manage` (staff-only) as a default; revisit
  if a customer-facing flash-sale checkout flow is built, using the same
  self-service-vs-staff split described above for Loyalty.

## Summary table

| Action | Auth required | Reasoning |
|---|---|---|
| `Loyalty.Earn`/`Redeem`/`GetBalance` | `promotions:manage` (staff) | No customer flow exists; staff acts on customer's behalf |
| `validate-coupon` | None (`[AllowAnonymous]`) | Read-only, pre-login checkout step |
| `redeem-coupon` | Authenticated (any logged-in customer) | Real state change, but always self-scoped via JWT identity |
| `Promotions`/`FlashSales` list/get/applicable | `promotions:view` | Staff catalog browsing |
| `FlashSales.Reserve` | `promotions:manage` (staff) | Stock-decrementing write, no current customer caller |
