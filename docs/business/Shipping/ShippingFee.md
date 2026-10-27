# Delivery fee charged to the customer (ROADMAP P1-11b, 2026-10-28)

Business analysis of what the customer pays for delivery, the ward in the address and the real product weight sent to the carrier.
Carrier booking itself is in `CarrierIntegration.md`.

## 1. Purpose and scope
Before P1-11b the shop paid the carrier and the customer saw nothing: the order total was goods only. Now the customer sees and pays the shop's **delivery fee**; it is part of the order total, of the online payment, of the cash a carrier collects on delivery and of the invoice.

In scope: the flat fee with a free-delivery threshold, the fee on the order, the ward (phuong / xa) in the address, the product weight sent to the carrier.
Out of scope (P1-11c): the carrier's real price shown to the customer, a fee table by province / weight, returns through carriers, several parcels per order, J&T and a verified Viettel Post, the real sandbox run (parked by the owner, DEFERRED 4w).

## 2. Actors
| Actor | Role |
|---|---|
| Customer | Sees the fee at the review step and in the confirmation; pays it with the goods. |
| Shop admin (`settings:shipping:tenant:manage`) | Sets the flat fee and the free-delivery threshold in Settings → Shipping → Tenant. |
| Sales staff | Create orders in the admin; the fee is added automatically. |
| ShippingService | Owns the fee rule and answers the question "what does this shop charge for goods worth X?". |
| SaleOrderService | Asks Shipping, stores the fee on the order, includes it in the total. |
| Carrier | Collects the whole total (goods + fee) on a COD order. |

## 3. Requirements with acceptance criteria
| ID | Requirement | Acceptance criteria |
|---|---|---|
| FR-1 | The shop sets a flat fee and a free-delivery threshold. | Fee 30 000 / threshold 500 000: goods 100 000 → fee 30 000; goods 500 000 or more → 0. Threshold 0 = never free. Fee 0 = no charge. |
| FR-2 | The threshold looks at the goods after any coupon. | Goods 600 000 with a coupon of 150 000 → 450 000 → the fee is charged. |
| FR-3 | The fee is decided by the server, never by the client. | An order request that carries `shippingFee` / `totalAmount` ignores them (live check D: total 80 000 = 50 000 + 30 000). |
| FR-4 | The fee is part of the total everywhere. | Order total, payment amount, COD amount sent to the carrier and the invoice amount are all goods − coupon + fee (live check B: 130 000 each). |
| FR-5 | The customer is told before paying. | The review step shows Goods / Delivery (or "Free") / Total, and how much is missing for free delivery. The preview never decides: the order recomputes. |
| FR-6 | If the fee cannot be worked out, the order is refused. | Shipping stopped → HTTP 400 "The delivery fee cannot be worked out right now"; no order is saved (live check E). |
| FR-7 | The address has a ward and the carrier receives it. | Checkout and the admin order form have a ward field; GHTK receives `ward` (live check B). |
| FR-8 | The parcel weighs what the products weigh. | A 0.8 kg variant ordered twice → GHTK receives weight 0.8 per item; a product without a weight uses the shop's default item weight; a Product outage never blocks a booking. |
| FR-9 | Loyalty points are earned on the goods only. | Order total 230 000 with fee 30 000 → 200 000 points base. |

## 4. Business rules
1. Fee = 0 if the shop's flat fee is 0; else 0 if threshold > 0 and goods ≥ threshold; else the flat fee (`ShippingFeePolicy`).
2. Goods = Σ lines − coupon discount, never below 0. A coupon never reduces the fee.
3. The fee is set once, when the order is created; it is not changed afterwards. Cancelling an unpaid order closes the payment for the whole total; a captured payment refunded on cancellation is refunded in full, fee included.
4. A completed return refunds the goods; the delivery fee is **not** refunded unless the shop does so by hand (owner decision pending, R36).
5. The fee is the shop's price, not the carrier's cost; the difference stays with the shop.
6. The ward is optional in the data (older orders have none) but GHTK and Viettel Post may refuse a parcel without it; the refusal moves booking to the next carrier as before.

## 5. Use cases
**UC-1 Customer places an order (main).** Customer fills the address (street, ward, city, province) and phone → picks a payment method → reviews: goods 100 000, delivery 30 000, total 130 000 → places the order → Sales asks Shipping for the fee on goods after coupon → saves the order with the fee → payment / COD uses 130 000.
**UC-1a Free delivery.** Goods ≥ threshold → delivery shows "Free", total = goods.
**UC-1b Preview unavailable.** The preview fails → the review shows "could not be worked out"; the customer can still place the order, the server adds the fee.
**UC-1c Shipping down at order time (exception).** The order is refused with a clear message; the customer retries.
**UC-2 Shop sets the fee.** Settings → Shipping → Tenant → fee and threshold → save. The explanation text states: free once goods (after coupon) reach the threshold; 0 = none.
**UC-3 Staff creates an order.** Same rule; ward field on the form.
**UC-4 Parcel weight.** On approval the order saga asks Shipping for a shipment; Shipping asks Product for the weight of each product, converts kg / g / lb / oz to grams, and falls back to the default item weight.

## 6. Data model
- Sales `SalesOrders.ShippingFee DECIMAL(18,2) NOT NULL DEFAULT 0`, `ShippingAddress_Ward NVARCHAR(100) NOT NULL DEFAULT ''` (migration Sales 015). `TotalAmount` includes the fee.
- `SalesOrderCreatedEvent` gains `ShippingFee` and `DiscountAmount`; its `TotalAmount` is now the **final** total. (Before P1-11b the event was built before the coupon was applied, so Accounting, Payment-adjacent consumers and Reporting saw the total without the coupon: fixed as a side effect, R34.)
- Saga `CreateShipmentCommand.ShippingAddress.Ward`.
- No new Shipping tables: it uses `TenantShippingSettings.DefaultShippingFee / FreeShippingThreshold` (existing since migration 003).

## 7. Integrations
| From → To | Call | Notes |
|---|---|---|
| Sales → Shipping | `GET internal/v1/shipping-fee?merchandiseValue=` | Service token `salesorder`; Shipping trusts it through `ServiceAuth:TrustedCallers`; fail closed. |
| Storefront → Shipping (via Gateway) | `GET /api/v1/shipping/fee?merchandiseValue=` | Any signed-in user; tenant from the token; preview only. |
| Shipping → Product | `POST internal/v1/product-weights` | Service token `shipping`; answers grams per product; products without weight are absent. |
| Sales → Accounting / Promotion / Reporting / CRM / Notification | `SalesOrderCreatedEvent` | Accounting books the invoice for `TotalAmount` (fee inside, posted on the sales revenue key); Promotion subtracts `ShippingFee` before points. |
`/internal/...` is never routed by the Gateway (live check: 404).

## 8. Permissions
Fee and threshold: existing `settings:shipping:tenant:view|manage`. The preview endpoint needs only a signed-in user; the internal endpoints need a service token.

## 9. Reports and accounting
The invoice carries the fee inside its gross amount; with the tenant's VAT rate it splits like goods. There is **no separate posting key** for delivery revenue yet (R35).

## 10. Assumptions
Delivery to one address, one fee per order. The shop's flat fee is acceptable instead of the carrier price. Fee is VAT-bearing at the tenant's single rate.

## 11. Open issues
R34–R38 in `docs/DEFERRED-ISSUES.md` §6; the real sandbox run is §4w.

## 12. UAT scenarios (all passed live on tenant `glcheck`, fake GHTK server)
| # | Scenario | Result |
|---|---|---|
| S1 | Preview 100 000 and 600 000 with fee 30 000 / threshold 500 000 | 30 000 / free; 401 without token; `/internal` 404 via Gateway |
| S2 | COD order 2 × 50 000 | total 130 000, fee 30 000, ward kept, invoice 130 000, GHTK `pick_money` 130 000 |
| S3 | Order 2 × 300 000 | fee 0, total 600 000 |
| S4 | Client sends `shippingFee: 0, totalAmount: 1` | ignored: 80 000 / 30 000 |
| S5 | Shipping stopped | 400, nothing saved |
| S6 | Variant 0.8 kg | GHTK receives weight 0.8 (without the variant: default 0.5) |
