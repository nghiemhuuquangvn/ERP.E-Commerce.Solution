# Delivery fee charged to the customer (ROADMAP P1-11b, 2026-10-28)

Business analysis of what the customer pays for delivery, the ward in the address and the real product weight sent to the carrier.
Carrier booking itself is in `CarrierIntegration.md`.

## 1. Purpose and scope
Before P1-11b the shop paid the carrier and the customer saw nothing: the order total was goods only. Now the customer sees and pays the shop's **delivery fee**; it is part of the order total, of the online payment, of the cash a carrier collects on delivery and of the invoice.

In scope: the flat fee with a free-delivery threshold, the fee on the order, the ward (phuong / xa) in the address, the product weight sent to the carrier.
Added by P1-11c (2026-10-28), section 13: the **fee table by province × weight**, **delivery revenue on its own account** and the **delivery-fee refund chosen on a return**.
Out of scope (later branches): the carrier's real price shown to the customer, returns through carriers, several parcels per order, J&T and a verified Viettel Post, the real sandbox run (parked by the owner, DEFERRED 4w).

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
4. A completed return refunds the goods; the delivery fee is refunded **only when the approver ticks it** (P1-11c, rule 13.4). *(Before P1-11c the admin sent the order total, so the fee came back by default; the P1-11b text said the opposite.)*
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
The invoice carries the fee inside its gross amount; with the tenant's VAT rate it splits like goods. Since P1-11c the delivery part of the net is booked on its own key `ShippingRevenue` (section 13.3).

## 10. Assumptions
Delivery to one address, one fee per order. The shop's flat fee is acceptable instead of the carrier price. Fee is VAT-bearing at the tenant's single rate.

## 11. Open issues
R34–R40 in `docs/DEFERRED-ISSUES.md` §6; the real sandbox run is §4w.

## 12. UAT scenarios (all passed live on tenant `glcheck`, fake GHTK server)
| # | Scenario | Result |
|---|---|---|
| S1 | Preview 100 000 and 600 000 with fee 30 000 / threshold 500 000 | 30 000 / free; 401 without token; `/internal` 404 via Gateway |
| S2 | COD order 2 × 50 000 | total 130 000, fee 30 000, ward kept, invoice 130 000, GHTK `pick_money` 130 000 |
| S3 | Order 2 × 300 000 | fee 0, total 600 000 |
| S4 | Client sends `shippingFee: 0, totalAmount: 1` | ignored: 80 000 / 30 000 |
| S5 | Shipping stopped | 400, nothing saved |
| S6 | Variant 0.8 kg | GHTK receives weight 0.8 (without the variant: default 0.5) |

## 13. P1-11c — fee table, delivery revenue, delivery-fee refund (2026-10-28)

### 13.1 Actors
Shop admin (`shipping:carriers:manage` edits the price list, `shipping:view` reads it), customer (sees the fee), approver of a return (`sales_order:approve`), accountant (reads the invoice and the ledger).

### 13.2 The delivery price list
**Purpose.** A flat fee is too blunt: a 3 kg parcel to Hanoi costs the shop more than a 300 g one across town. The shop keeps a price list: rows of "provinces × weight band → fee".

| ID | Requirement | Acceptance criteria |
|---|---|---|
| FR-10 | The shop defines fee rows: name (unique), priority, active, provinces (comma list, empty = any), weight from / to in grams (both optional, inclusive), fee ≥ 0. | A duplicate name, a negative fee, or from > to is refused with HTTP 400; a customer cannot read or change the list (403). |
| FR-11 | The first active row, by ascending priority, whose province and weight match sets the fee. | Rows "HCM light ≤ 1 000 g → 20 000", "HCM heavy ≥ 1 001 g → 30 000", "Ha Noi → 45 000": 800 g to Ho Chi Minh = 20 000; 1 600 g = 30 000; Hanoi = 45 000. |
| FR-12 | A parcel no row matches pays the flat fee of the Tenant tab. | Hue (no row) = 40 000. A shop with no rows behaves exactly as in P1-11b. |
| FR-13 | The free-delivery threshold applies on top of the row. | Goods ≥ 500 000 → 0 whichever row matched; a row whose fee is 0 is not reported as "free because of the threshold". |
| FR-14 | Provinces are matched without case, diacritics or "Tỉnh / Thành phố / TP." | "TP. Hồ Chí Minh", "ho chi minh" and "Hồ Chí Minh" are the same province. |
| FR-15 | The parcel weight is the sum of quantity × the product's own weight (ProductService), the shop's default item weight for a product without one. | The answer says the weight used and whether it was estimated; a Product outage uses the defaults. The admin "try a parcel" box prices a typed weight. |
| FR-16 | The same rule serves the preview and the order. | The storefront posts the cart and the typed province; Sales posts the same when the order is created and trusts only its own answer. |

**Business rules.** (1) Priority is ascending: a lower number is tried first; ties by name. (2) Province = the order's *State*; district = *City*. (3) A row with no province matches every province, a row with no weight bounds every weight. (4) The price list is the shop's price to the customer, not the carrier's cost; the shop bears the difference.

**Use cases.** *UC-5 Shop prices delivery (main):* admin opens Settings → Shipping → Delivery fees, adds rows, tries "Hồ Chí Minh, 1 600 g, goods 100 000" → "The customer pays 30 000 ₫ (HCM heavy)". *UC-5a:* a row is switched off → the next matching row or the flat fee applies. *UC-5b (exception):* two rows with the same name → 400. *UC-1 (customer):* review shows Goods / Delivery / Total; changing the province before review changes the fee.

### 13.3 Delivery revenue on its own account
| ID | Requirement | Acceptance criteria |
|---|---|---|
| FR-17 | The delivery part of an invoice is credited to the posting key `ShippingRevenue`. | Invoice 130 000 (goods 100 000 + delivery 30 000, no VAT): Dr 131 130 000 / Cr 5111 100 000 / Cr 5113 30 000 on the Vas99 chart (live check); the entry balances. |
| FR-18 | The key resolves like the others: 5113 on Vas99, 4000 (the one revenue account) on the simple chart; a tenant's own chart without such an account books it with sales revenue; posting never stops because of it. | Unit tests for both charts and for a chart without the account. |
| FR-19 | With VAT the delivery share of the net follows its share of the amount; goods + delivery + VAT = gross. | 1 100 000 incl. 10 % with 110 000 delivery → net 1 000 000 = goods 900 000 + delivery 100 000. Awkward amounts still balance. |
| FR-20 | The invoice shows the delivery part. | `Invoices.ShippingNetAmount` (Accounting migration 027), a "Delivery" column on the admin invoice list. A document with VAT lines or in a foreign currency keeps the delivery in sales revenue. |

### 13.4 Delivery-fee refund on a return
| ID | Requirement | Acceptance criteria |
|---|---|---|
| FR-21 | A return refunds the goods; the delivery fee only when the approver ticks "also refund the delivery fee". | Order goods 100 000 + fee 30 000: refund 130 000 without the tick → 409 "at most 100 000 … the delivery fee is refunded only when it is ticked"; 100 000 → 200; 130 000 with the tick → 200. |
| FR-22 | The refund is capped at the goods (after the coupon) plus the fee when ticked. | A coupon of 100 on goods 1 000 caps the refund at 900 (+ fee). |
| FR-23 | The decision is recorded. | `SalesOrders.ReturnShippingFeeRefunded` (Sales migration 016) and `ShippingFeeRefunded` on `SalesOrderReturnedEvent`; an order with free delivery never records a fee refund. |
Cancelling an order that was paid refunds the whole payment (nothing shipped), fee included.
*Use case UC-6:* approver opens a returned order, the dialog proposes "Refund (goods): 100 000", ticks "Also refund the delivery fee (30 000 ₫)" when the shop is at fault → the proposed amount becomes 130 000 → Complete return.

### 13.5 Data and integrations
`ShippingFeeRules` (Shipping migration 009): TenantId, Name (unique per shop), Priority, IsActive, MatchProvinces, Min/MaxWeightGrams, Fee. Fee request `POST api/v1/shipping/fee` (preview) and `POST internal/v1/shipping-fee` (Sales, service token): `{merchandiseValue, province, items[{productId, quantity}], weightGrams?}` → `{fee, isFree, freeShippingThreshold, ruleName, weightGrams, weightEstimated}`. Rules CRUD `api/v1/shipping/fee-rules`. Accounting: `SalesOrderCreated.ShippingFee` → `CreateInvoiceCommand.ShippingAmount`.

### 13.6 UAT (all passed live on tenant `glcheck`, fake GHTK)
| # | Scenario | Result |
|---|---|---|
| S7 | Rows HCM light / HCM heavy / Ha Noi, flat 40 000, threshold 500 000; preview 800 g HCM, 1 600 g "TP. Hồ Chí Minh", Hanoi, Hue, goods 600 000 | 20 000 (HCM light) / 30 000 (HCM heavy) / 45 000 (Ha Noi) / 40 000 (flat) / 0 free; old GET call 40 000 |
| S8 | Duplicate row name, negative fee, customer reads the list | 400, 400, 403 |
| S9 | COD order, 2 tees (0.8 kg each) to Ho Chi Minh | fee 30 000, total 130 000, GHTK `pick_money` 130 000, weight 0.8 per item, invoice delivery net 30 000 |
| S10 | Invoice entry (October period reopened for the check, then restored) | Dr 131 130 000 / Cr 5111 100 000 / Cr 5113 30 000 |
| S11 | Return of a 130 000 order: 130 000 without tick; 100 000; 130 000 with tick; 70 000 with tick on a 1-tee order | 409; 200 (fee not refunded); 200 (refunded); 200 |
