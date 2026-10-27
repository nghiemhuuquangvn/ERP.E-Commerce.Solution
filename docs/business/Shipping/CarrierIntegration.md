# Carrier integration and carrier selection (GHTK, Viettel Post, GHN, J&T)

> Business analysis, 2026-10-27 (ROADMAP P1-11). Owner: ShippingService. Related: `module-13-shipping.md` (the shipment aggregate), `docs/business/Payment/OnlinePayments.md` (cash on delivery), `docs/business/Sales/OrderProcessing.md` (the order saga), `docs/business/Identity/CustomerSignup.md`.

## 1. Purpose and scope
Before P1-11 every shop booked its parcels through one global GHN client (or a stub that invented tracking codes), the first active carrier by name was always chosen, a refusal by the carrier was swallowed (the shipment was saved with no tracking number), the carrier never learned the cash to collect, and nobody listened to the carriers' status messages.
Now each shop has **its own accounts** at GHTK, Viettel Post and GHN; an order is offered to a carrier chosen by **the shop's rules, then the cheapest quote**; a carrier that refuses passes the parcel to the next; the carriers' **notifications** (and a polling safety net) move the shipment forward, and delivery captures the cash-on-delivery payment.
Out of scope (P1-11b): showing / charging the shipping fee to the customer, returns through the carrier, one order in several parcels, product weights from the catalog, J&T Express.

## 2. Actors
| Actor | Role |
|---|---|
| Shop administrator | Sets the pickup address, the carrier accounts and the selection rules; cancels / rebooks parcels |
| Customer | Gives a delivery address **and a phone number** at checkout; receives the parcel |
| Carrier (GHTK, Viettel Post, GHN) | Books, collects, delivers, reports status, collects cash on delivery |
| Order saga (Sales) | Asks Shipping for a shipment once the order is approved and stock reserved; compensates when no carrier accepts |
| Payment | Captures a cash-on-delivery payment when delivery is confirmed |
| Platform operator | Switches carrier integrations on or off for all shops |

## 3. Process
1. **Setup (admin, Settings → Shipping).** Pickup address + default item weight (*Tenant* tab); an account per carrier (*Carriers* tab: ids, secrets stored encrypted, a notification secret); optional rules (*Carrier rules* tab) with a parcel tester.
2. **Order.** Checkout collects address and phone; the order records the phone and the payment method.
3. **Booking** (saga → `CreateShipmentCommand` with recipient, phone, order total, cash to collect = order total for COD, line prices). Shipping builds the parcel (weight = items × the shop's default weight), then offers it to: (a) the active rules, lowest priority number first, whose conditions all hold and whose carrier the shop can use; (b) every other usable carrier, cheapest quote first, carriers that cannot quote last. The first carrier that accepts wins; its tracking code, fee, ETA and the reason it was chosen are stored. Nobody accepts → `ShipmentCreationFailed` with every carrier's reason → the saga cancels the order. Only unreachable carriers → retried (up to 5 times, a few seconds) before giving up.
4. **Tracking.** The carrier posts to `…/api/v1/shipping-webhooks/<carrier>?hash=<the shop's notification secret>`; the shop is found from the tracking code and verified with *that shop's* secret; each (carrier, parcel, status, time) is applied once. A polling job asks about parcels in carrier hands that have been silent for 6 hours.
5. **Delivery.** `Delivered` → `DeliveryConfirmed` → Payment captures the COD payment, Sales marks the order delivered. If the carrier states the cash it collected and that differs from the order, the shipment is flagged for a person.
6. **Cancellation.** An order cancelled (by hand or by the saga) cancels a parcel the carrier has not collected, at the carrier and here; a parcel already collected is flagged "have it returned". A person can also cancel at the carrier or book again from the Shipments page.

## 4. Requirements
| ID | Requirement | Acceptance |
|---|---|---|
| FR-S1 | A shop has its own account at each carrier; secrets are encrypted and never returned. | Saving → GET shows only "has secret" flags; the stored values are ciphertext; a blank secret on a later save keeps the stored one. |
| FR-S2 | A carrier takes orders only when the platform offers it, the shop enabled it and its account is complete. | Enabling GHTK without a token → 400; switching GHTK off on the platform → the shop's account shows "off for the platform" and no order goes to it. |
| FR-S3 | The pickup address is required to book. | No pickup address → the order is cancelled with "the shop's pickup address is not set". |
| FR-S4 | Rules decide first, then the cheapest quote; a refusing carrier passes the parcel on. | A COD→GHTK rule beats a cheaper Viettel Post quote; with GHTK refusing the parcel goes to Viettel Post and the note says "after: GHTK: …"; with no rule the cheapest quote wins and the note says so. |
| FR-S5 | No carrier accepting is never silent. | All refusing → order cancelled; the failure event lists every carrier's reason. |
| FR-S6 | The carrier is told the cash to collect, the declared value, the weight and the recipient's phone. | GHTK: `pick_money` = order total for COD, 0 when prepaid; Viettel Post: `MONEY_COLLECTION` and `ORDER_PAYMENT` 3 / 1. |
| FR-S7 | A carrier notification is applied only when verified, and only once. | Wrong secret → 400, nothing changes; unknown parcel → 400; a repeat → 200, nothing doubled. |
| FR-S8 | Delivery reported by a carrier captures the COD payment once. | Delivered → payment *Captured* (one `PaymentProcessed`); repeat → unchanged. |
| FR-S9 | Cash collected that differs from the order is flagged. | Carrier says 90,000 for a 100,000 order → shipment review note; the payment is still captured at the order's amount. |
| FR-S10 | A cancelled order cancels its uncollected parcel at the carrier. | Cancel the order → the carrier receives a cancel call, the shipment is *Cancelled*. A parcel already collected is flagged. |
| FR-S11 | The admin sees and manages shipments. | Shipments page: filter by status / search / needs attention; detail with timeline, fee, reason, review note; cancel at the carrier, book again. |

## 5. Business rules
- BR-S1 A parcel is booked with **one** carrier; the first acceptance ends the search (no double bookings). Re-booking the same order reference at GHTK or GHN returns the existing parcel.
- BR-S2 The shop pays the carrier's fee; the recipient pays only the cash on delivery (GHTK `is_freeship = 1`, GHN `payment_type_id = 1`). The customer is **not** charged shipping yet (P1-11b).
- BR-S3 The weight of the parcel is the number of items × the shop's default item weight until the catalog's weights reach Shipping.
- BR-S4 A shipment's state only moves forward and never leaves *Delivered*, *Returned* or *Cancelled*; a carrier that skips steps (picked up and delivered in one message) walks the shipment through them.
- BR-S5 Statuses typed by a shipper (GHTK 45 / 49 / 123 / 127 / 128 / 410) are only tracking lines, never a state change.
- BR-S6 Viettel Post status is read from its **name** ("Giao hàng thành công" = delivered, "không thành công" = failed): delivery captures money, so an unrecognised status is only a tracking line.
- BR-S7 The carrier's host comes from configuration (sandbox or production), never from tenant input.
- BR-S8 Anonymous webhook routes are safe only because the secret is verified before anything is applied; there is one path for them.
- BR-S9 A parcel the carrier has collected cannot be cancelled by the shop; it is flagged for a person.
- BR-S10 An unreachable carrier is not a verdict: the booking is retried before the order is cancelled.

## 6. Use cases
**UC-S1 Set up Viettel Post.** Admin enters login name and password, a notification secret, enables it; gives Viettel Post the address `…/api/v1/shipping-webhooks/vtp?hash=<secret>`. Alternative: account incomplete → cannot enable.
**UC-S2 Route big COD orders to GHTK.** Admin adds the rule "COD, ≥ 2 kg → GHTK, priority 10", tests a parcel in the tester (shows the rule and every quote). Exception: rule naming J&T → refused (not integrated).
**UC-S3 Order shipped.** Saga asks → rules → cheapest → booked → carrier tells "picked up", "delivering", "delivered" → COD captured. Exception: carrier says "delivery failed" → shipment *DeliveryFailed*, can be retried.
**UC-S4 Nobody accepts.** All carriers refuse (bad phone, no coverage) → the order is cancelled with all reasons; the admin fixes the data and the customer orders again.
**UC-S5 Order cancelled before pickup.** Parcel cancelled at the carrier; exception: carrier refuses → the shipment stays and carries a review note.

## 7. Data and state
| Item | Content |
|---|---|
| `TenantCarrierCredentials` (Shipping 008) | shop, carrier, enabled, sandbox, ids, login, secrets **encrypted** (token / password; the notification secret) |
| `CarrierSelectionRules` | name, priority, active, provinces, weight and order-value limits, COD only / prepaid only, carrier |
| `CarrierWebhookLogs` | carrier, tracking code, status, time — unique: a repeat changes nothing |
| `Shipments` + | `CodAmount`, `DeclaredValue`, `CarrierFee`, `CarrierOrderCode`, `SelectionNote`, `BookingError`, `LastCarrierSyncAt`, `ReviewNote` |
| `TenantShippingSettings` + | pickup name / phone / address / ward / district / province, `DefaultItemWeightGrams` |
| `SystemShippingSettings` + | `GhtkAvailable`, `VtpAvailable`, `GhnAvailable`, `JntAvailable` |
| `SalesOrders.CustomerPhone` (Sales 014) | the recipient's phone |
| Shipment states | Pending → ReadyForPickup → PickedUp → InTransit → OutForDelivery → Delivered; DeliveryFailed ⇄ OutForDelivery; Returned; Cancelled |

## 8. Provider mapping
| | GHTK | Viettel Post | GHN | J&T |
|---|---|---|---|---|
| Documentation checked | **yes** (api.ghtk.vn) | **no** (behind a partner login) | **yes** (developer.ghn.vn) | **no** |
| Auth | `Token` + `X-Client-Source` | login → `Token` | `Token` + `ShopId` | — |
| Quote | `GET /services/shipment/fee` (grams) | `POST /order/getPrice` (numeric place ids found by name) | not possible without district ids | — |
| Book | `POST /services/shipment/order?ver=1.5` | `POST /order/createOrder` | `POST /v2/shipping-order/create` (place **names**) | — |
| Cash to collect | `pick_money` | `MONEY_COLLECTION`, `ORDER_PAYMENT` | `cod_amount` | — |
| Cancel | `POST /services/shipment/cancel/{code}` | `POST /order/UpdateOrder` TYPE 4 | `POST /v2/switch-status/cancel` | — |
| Status poll | `GET /services/shipment/v2/{code}` | none (webhook only) | `POST /v2/shipping-order/detail` (unverified) | — |
| Webhook authentication | the secret in the callback address (`?hash=`) — GHTK does not sign | `TOKEN` in the payload or `?hash=` | the secret in the callback address | — |
| Status map | -1 cancelled · 3 picked up · 4 delivering · 5/6 delivered · 9 failed · 20 returning · 21/11 returned | by status **name** | `picked/storing/transporting/sorting` in transit · `delivering` · `delivered` · `delivery_fail` · `return*` · `returned` · `cancel` | — |
| Sandbox hosts | services-staging.ghtklab.com | partnerdev.viettelpost.vn | dev-online-gateway.ghn.vn | — |

J&T Express is a **placeholder**: the code is reserved and accepted in settings, it says plainly that it is not integrated and never takes part in selection.

## 9. Screens
- **Settings → Shipping → Tenant:** the old fee form plus *Pickup address* (contact, phone, street, ward, district, province) and the default item weight. **Carriers:** four cards (GHTK, Viettel Post, GHN, J&T "Not integrated"): Enabled, Sandbox, ids, secret boxes ("Set — leave blank to keep"), the address to give the carrier. **Carrier rules:** the rules (priority, name → carrier, conditions), *Add rule* form, and *Try a parcel* (province, district, weight, value, cash → matching rule and every quote). **System** tab: four carrier switches.
- **Shipments:** list (tracking, carrier, recipient, cash, carrier fee, status with "Needs attention", date), filters, detail with timeline, "why this carrier", review note, *Book with a carrier again*, *Cancel at the carrier*.
- **Storefront checkout:** a phone field in the delivery address.

## 10. Integrations
Sales → Shipping: `CreateShipmentCommand` now carries `RecipientPhone`, `PaymentMethod`, `OrderTotal`, `CodAmount`, line `UnitPrice` (RabbitMQ `sales_order_events`). Sales → Shipping: `SalesOrderCancelled` (new consumer). Shipping → Sales / Payment: `ShipmentCreated`, `DeliveryConfirmed`, `ShipmentCreationFailed` (Kafka `shipping-topic`, unchanged). Carriers → Shipping: anonymous `POST /api/v1/shipping-webhooks/{carrier}` (Gateway routes with priority 5, before the authenticated catch-all). Shipping → carriers: HTTPS, 20-second timeout, hosts from configuration (`Carriers:Ghtk|Vtp|Ghn:SandboxBaseUrl|BaseUrl`).

## 11. Permissions
`shipping:view` (list, detail, rules list, parcel tester), `shipping:update` (cancel at carrier, book again), `shipping:carriers:manage` (add / change / delete rules), `settings:shipping:tenant:view|manage` (pickup address, carrier accounts), `settings:shipping:system:view|manage` (platform switches). Webhooks need no token (secret).

## 12. Assumptions and open issues
- **Not run against a real carrier.** Create / cancel / quote calls were checked against a local fake carrier server shaped from the docs that could be read; the carriers' notifications were simulated with the shop's secret. A first real run needs sandbox accounts and a tunnel (DEFERRED R26).
- **Viettel Post is unverified** (field names, status table, place-id endpoints); **J&T is a placeholder**; **GHN cannot quote** (needs district ids), so it is reached through rules.
- The address has **no ward** (Sales stores street / district / province): carriers that require a ward (GHTK, Viettel Post) will refuse and the parcel falls to the next candidate — a ward field is the next improvement (DEFERRED AN3).
- Weight is a default per item (BR-S3).
- Open issues: DEFERRED §4v and the risk register §6 (R26…).

## 13. UAT scenarios (live on `glcheck` against the fake carrier, 2026-10-27)
| ID | Scenario | Result |
|---|---|---|
| UAT-S1 | Save GHTK / Viettel Post / GHN accounts; read them back | enabled & complete; secrets stored as ciphertext; GET returns no secret; J&T and a token-less account refused (400) |
| UAT-S2 | Order with no pickup address | cancelled; reason "the shop's pickup address is not set" |
| UAT-S3 | COD order, no rule | Viettel Post (26,000) chosen over GHTK (30,000); note "quote 26,000 VND, the cheapest of 2"; Viettel Post received COD 100,000, phone, weight 800 g, province / district ids |
| UAT-S4 | Rule "COD → GHTK" | GHTK booked; received `pick_money` 100,000, value 100,000, `is_freeship` 1, 800 g, phone, partner headers; the tester shows the rule and the quotes |
| UAT-S5 | GHTK refuses | Viettel Post books it; note "(after: GHTK: GHTK refused the order: Ward is required)" |
| UAT-S6 | Every carrier refuses | order *Cancelled*; the failure event lists all three reasons |
| UAT-S7 | GHTK notifications: wrong secret / unknown parcel / picked up / delivered / repeat | 400 / 400 / 200 → PickedUp / 200 → Delivered / 200; COD payment *Captured*; one `PaymentProcessed`; two log rows |
| UAT-S8 | Viettel Post "delivered" with another cash amount | delivered; review note "collecting 90,000 … expected 100,000"; appears under *needs attention* |
| UAT-S9 | Order cancelled by hand | the carrier received the cancel call; shipment *Cancelled* |
| UAT-S10 | Customer lists shipments | 403; webhook route without a token → 400 (not 401) |
