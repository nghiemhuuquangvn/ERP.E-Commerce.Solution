# Promotion back office in the Admin app (ROADMAP P2-1a)

Branch `feat/p2-1a-admin-promotion-pos` (2026-10-28). No migration. Builds on [module-20-promotion.md](module-20-promotion.md) (domain model) and [LoyaltyAndCouponSecurity.md](LoyaltyAndCouponSecurity.md) (who may redeem and adjust).

## 1. Purpose and scope
PromotionService already had the whole lifecycle in its API, but marketing could only reach it with a REST client. This task gives the Admin app the screens to **run promotions** (create, edit while still a draft, activate, pause, cancel), **hand out coupons** (generate in bulk, see which are used), **schedule flash sales** and **adjust a customer's loyalty points**. Two small backend gaps were closed so the screens need no workaround: editing a draft promotion and listing a promotion's coupons.

Out of scope (see §8): a list of loyalty accounts or their point history, coupon export, paging / filtering of the promotion list, editing an active promotion, editing a flash sale, storefront coupon UI (P2-2).

## 2. Actors
| Actor | Does |
|---|---|
| Marketing manager (`promotions:manage`) | Creates and edits draft promotions, activates / pauses / cancels, generates coupons, creates / starts / cancels flash sales |
| Customer service (`promotions:manage`) | Looks a customer up by user id and adds or redeems loyalty points by hand |
| Viewer (`promotions:view`) | Sees flash sales and looks a loyalty account up (read only) |

## 3. Requirements and acceptance criteria
| ID | Requirement | Acceptance criteria |
|---|---|---|
| FR-1 | List every promotion with type, value, period, usage and status | `GET promotions/all` rows appear with a status badge; usage shows `used / limit` when a total limit exists |
| FR-2 | Create a promotion | Form for type (percentage, fixed amount, buy X get Y, free shipping), scope (order, products, categories), value, maximum discount, minimum order, usage limits, dates; BuyXGetY needs both quantities; the new promotion is a **Draft** |
| FR-3 | Edit a promotion **only while Draft** | `PUT promotions/{id}` changes every field; any other status answers HTTP 400 "Business rule violation"; the *Edit* link is shown for drafts only |
| FR-4 | Activate / pause / cancel with confirmation | Each asks for confirmation; *Cancel* is danger-styled; a cancelled or expired promotion shows no further actions |
| FR-5 | Generate coupons and see them | Dialog takes a count (1–10 000) and an optional prefix; the list shows code, used / available and when it was used; `GET promotions/{id}/coupons`, unknown promotion → 404 |
| FR-6 | Create a flash sale | Name, window, products picked from the catalogue with price, sale price (must be lower) and quantity; created as **Scheduled** |
| FR-7 | Start or cancel a flash sale | *Start now* only while Scheduled; *Cancel* while Scheduled or Active; the list shows sold / allocated and each item's remaining stock |
| FR-8 | Adjust loyalty points | Look up by user id (GUID); tier, available / total points and points to the next tier are shown; *Add points* needs a reason; no account → a clear message, not an error |
| FR-9 | Menu and permissions | Sidebar group *Promotions*; Promotions needs `promotions:manage`, Flash sales and Loyalty need `promotions:view`; write buttons need `promotions:manage` |
| FR-10 | Two languages | Every label exists in English and Vietnamese |

## 4. Business rules
1. A promotion's terms can change only while it is a Draft: once it is live, customers and orders rely on them. To change a live promotion: cancel it and create a new one.
2. End date must be after start date; BuyXGetY needs buy and get quantities (refused as HTTP 400, not 500).
3. Coupons are generated 1–10 000 per call; a coupon is single-use.
4. A flash sale's sale price must be lower than its price and its quantity above zero (checked in the form and by the domain).
5. Loyalty adjustments are staff actions (`promotions:manage`); customers never reach these screens (default roles hold no permissions).
6. Enums in requests are numeric (the API has no string-enum converter); responses carry names. The screen maps between them in one place (`promotion.api.ts`).

## 5. Use cases
**UC-1 Run a seasonal promotion (main).** Marketing opens *Promotions* → *New promotion* → fills the form → saves (Draft) → edits a typo → *Generate coupons* (500, prefix `SPRING`) → *Activate* → later *Pause* during a stock shortage → *Activate* again.
*Alternative:* the promotion is already active and the discount is wrong → *Edit* is not offered → cancel and create a new one.
*Exception:* an edit sent for a non-draft promotion (stale screen) → 400 and the message under the form.

**UC-2 Flash sale.** Marketing opens *Flash sales* → *New flash sale* → picks products, sets sale price and stock → saves (Scheduled) → *Start now* or lets the scheduler start it → watches sold / remaining.

**UC-3 Goodwill points.** Customer service pastes the customer's user id → sees the tier → *Add points* 200 with the reason "late delivery". *Exception:* unknown id → "no loyalty account yet".

## 6. Data and integrations
No new tables or events. New endpoints (permission in brackets): `PUT promotions/{id}` (`promotions:manage`), `GET promotions/{id}/coupons` (`promotions:manage`). Gateway prefix `/prm/api/v1`. The product picker reuses ProductService through the existing admin component. Tenant isolation is the DbContext's existing filter.

## 7. UAT (all passed live on tenant `glcheck`, API level; test data removed afterwards)
| # | Scenario | Result |
|---|---|---|
| S1 | Create → edit draft (type, value change) | 201, then 200 with the new terms |
| S2 | Generate 5 coupons with prefix, list them | 200, 5 codes `P21A-…`, 0 used |
| S3 | Activate, then edit | activate 200; edit 400 "Business rule violation" |
| S4 | Coupons of an unknown promotion | 404 |
| S5 | Pause, cancel, list all | 200 / 200, status Cancelled in the list |
| S6 | Flash sale: create (20 % off), list, cancel | 201 Scheduled, listed, 200 |
Not done: a browser walk-through of the screens (checked: `tsc -b` clean and the deployed bundle contains the routes).

## 8. Open issues
No list of loyalty accounts or point history; coupons cannot be exported; promotions are not paged (client side only); a flash sale cannot be edited; product / category ids on a promotion are typed as text, not picked. See DEFERRED-ISSUES R48–R51.
