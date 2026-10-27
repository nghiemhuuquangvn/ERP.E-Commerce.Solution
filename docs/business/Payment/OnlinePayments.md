# Payment — online payments: MoMo, ZaloPay, VNPay, cash on delivery (ROADMAP P1-10)

Business analysis written 2026-10-27. It describes what the system does after P1-10. The older Vietnamese module analysis (`module-11-payment.md`) describes the payment entity, refunds, settlements and VNPay as they were; where the two differ, this document wins (a pointer is at the top of that file). The ledger side of payments is in `docs/business/Accounting/AutoPosting.md`.

## 1. Purpose and scope
Before P1-10 the shop could not take an online payment at all:
- the storefront payment step was a radio list that was **never sent** anywhere; mobile had no choice and no result screen;
- the only gateway was VNPay, chosen once for the whole process from a global setting; the per-shop keys saved on the settings screen were stored but **never used**;
- every order waited at "payment pending" for a payment that nothing could start (the saga creates it as *credit card, USD*);
- the gateway's callback could not even reach the service (the API Gateway demanded a bearer token on it), returned JSON instead of a page, and did not check the amount paid.

Now a customer chooses **cash on delivery, VNPay, MoMo or ZaloPay** at checkout (only the methods the shop has really set up are offered); an online choice sends the customer to the gateway after the order is placed, and a result page shows what really happened; the gateway's own notification confirms the payment. **Each shop uses its own merchant account** (keys stored encrypted, per shop).

Out of scope (DEFERRED §4u): Stripe and card payments in other currencies, saved cards and tokens, installments through a gateway, e-mail / push when a payment fails, a reconciliation report against the gateway's statements, refund status polling for ZaloPay, a payment expiry job, QR / bank transfer by statement matching, a real merchant (production) account was not available — everything ran against the providers' public **sandbox**.

## 2. Actors and permissions
| Actor | Does | Permission |
|---|---|---|
| Customer (storefront / mobile) | picks a method, places the order, pays at the gateway, sees the result; starts payment again while it is still pending | signed-in customer (own orders only) |
| Shop administrator | enters the shop's gateway accounts, switches COD, refunds a payment | `settings:payment:tenant:view/manage`, `payments:refund` |
| Platform administrator | switches a gateway on or off for the whole platform | `settings:payment:system:view/manage` |
| Gateway (VNPay / MoMo / ZaloPay) | returns the customer's browser and sends its server notification | none — authenticated by its **signature** |
| Delivery (Shipping) | confirms delivery, which collects a COD payment | event `DeliveryConfirmed` |
| Staff with `payments:create` / `payments:view` | start or read the payment of any order | as named |

No new permission was added.

## 3. Process
**Choosing.** `GET payment-gateway/methods` returns what this shop can offer: **COD** (unless the shop switched it off) plus each gateway that is (1) available on the platform, (2) enabled by the shop and (3) has all its keys. Nothing else is shown.
**Online (VNPay / MoMo / ZaloPay).**
1. The order is placed with `paymentMethod`; Sales stores it and the order saga asks Payment for a payment of that method in **VND** (customer, order number and amount included). The order waits at *payment pending*.
2. The client calls `POST payment-gateway/orders/{id}/checkout`. Payment waits (up to ~10 s) for the saga's payment, checks the caller is the order's customer, takes a **fresh transaction reference**, asks the gateway to create the payment with the shop's keys and returns the gateway page's address. The client sends the customer there. Starting again while the payment is still pending is allowed (a new reference each time).
3. The customer pays. The gateway (a) sends the **customer's browser** back to `callback/{provider}` and (b) calls the server address `ipn/{provider}`. Both go through one pipeline: find the payment's shop from the reference → verify the signature with **that shop's** keys → check the paid amount equals the payment → apply (idempotent). The browser is then sent on to the shop's result page; the gateway gets the answer it expects (MoMo 204; ZaloPay `return_code 1`; VNPay `RspCode 00`).
4. Paid → the payment is captured, `PaymentProcessed` is published, the saga reserves stock and the order goes on. Failed → the payment fails and the saga **cancels the order**.
5. The result page shows the real state of the payment (polling for up to a minute, because the server notification can arrive after the browser does) and offers *Pay now / Pay again* while it is still pending.
**Cash on delivery.** The order does **not wait**: Payment records a *pending cash* payment and the saga goes straight to reserving stock. When Shipping confirms delivery, Payment captures the payment (`PaymentProcessed`, which Accounting books). If the order is cancelled first, the pending payment is cancelled, not refunded.
**Refund.** From the Payments screen: the refund goes to **the payment's own gateway with the shop's keys** (VNPay, MoMo, ZaloPay) and the payment changes only when the gateway confirms. A payment captured by hand or collected as COD has no gateway call. ZaloPay may answer "still processing": the refund then stays *Processing* (not failed, not confirmed).
**An order cancelled before it was paid.** If the saga gives up (timeout) or stock is short, it asks Payment to cancel the order's pending payment. Money that still arrives for a cancelled payment is **not captured**: the payment is flagged for review with the reason "paid after the payment was cancelled — refund the customer", and the gateway is answered normally so it stops retrying.

## 4. Provider mapping
| | VNPay | MoMo (`captureWallet`, v2) | ZaloPay (v2) |
|---|---|---|---|
| Shop's account | TMN code + hash secret | partner code + access key + secret key | app id + key 1 + key 2 |
| Create | signed URL (HMAC-SHA512) | `POST /v2/gateway/api/create`, HMAC-SHA256 over the sorted fields → `payUrl` | `POST /v2/create` (form), MAC = HMAC-SHA256(key 1, `app_id|app_trans_id|app_user|amount|app_time|embed_data|item`) → `order_url` |
| Reference | `<payment id>` + time (ms) | `M` + payment id + time (ms) | `yyMMdd_` (Vietnam date) + payment id + time (ms) |
| Browser return | query, signed | query, same fields as the notification | query `apptransid / status / checksum` (key 2); `status 1` = paid |
| Server notification | query, signed (`RspCode`) | POST JSON, signed; `resultCode 0` = paid; answer 204 | POST JSON `{data, mac}` (key 2); ZaloPay calls only for paid orders; answer `return_code 1` |
| Refund | refund API (pipe-signed) | `POST /v2/gateway/api/refund` with the MoMo `transId` | `POST /v2/refund` with `zp_trans_id`; code 3 = processing |
| Currency | VND only | VND only | VND only |
| Hosts (config, never from the shop) | `VnPay:BaseUrl` / `ApiUrl` (+ `Production…`) | `MoMo:BaseUrl` (default `test-payment.momo.vn`) / `ProductionBaseUrl` | `ZaloPay:BaseUrl` (default `sb-openapi.zalopay.vn`) / `ProductionBaseUrl` |
A shop's account marked *sandbox* uses the sandbox host; a production account needs its production host configured, otherwise the call is refused (no silent fallback to the sandbox).

## 5. Requirements
| ID | Requirement | Acceptance |
|---|---|---|
| FR-O1 | The shop offers at checkout only the methods it has really set up. | Without keys: `COD`. After MoMo and ZaloPay keys are saved: `COD, MoMo, ZaloPay`. COD switched off + no gateway: none, and the checkout says so. |
| FR-O2 | Each shop keeps its own gateway accounts, encrypted, and never sees them again. | Saving returns only "has key" flags; the stored columns are ciphertext; an empty secret on a later save keeps the stored one. |
| FR-O3 | The order records the payment method; an unknown method is refused. | `paymentMethod: Bitcoin` → 400; an order with no method behaves as before. |
| FR-O4 | An online order is paid through `checkout`; only its customer (or staff) may start it. | Another customer gets 404 for status and checkout; staff with `payments:create` may start it. |
| FR-O5 | Every attempt has its own reference. | Two checkouts in the same second give different references. |
| FR-O6 | A gateway notification is accepted only when its signature is valid for that shop and its amount equals the payment. | Bad signature → refused, nothing changes. Wrong amount → refused, the payment stays pending and is **flagged**. Right amount → captured; the same message again changes nothing. |
| FR-O7 | A failed payment cancels the order; a pending one can be started again. | Notification `resultCode ≠ 0` → payment failed, order cancelled; `checkout` afterwards → 400. |
| FR-O8 | The browser return lands on the shop's result page and only a verified return says "paid". | Signed return → `/payment/result?orderId=…&status=paid`; forged → `status=invalid` and no change. |
| FR-O9 | Cash on delivery does not hold the order; the cash is collected on delivery. | COD order reaches stock reservation at once; delivery event → payment captured once; replay → unchanged; `checkout` on COD → "paid on delivery". |
| FR-O10 | Refunds use the payment's gateway and the shop's keys. | A refund on a MoMo payment calls MoMo (here: refused by MoMo for a fake transaction → refund recorded *Failed*, payment still *Captured*). |
| FR-O11 | Money arriving for a cancelled payment is never lost. | Late "paid" → 204, payment stays *Cancelled* and is flagged "refund the customer". |
| FR-O12 | The admin manages the accounts and refunds; storefront and mobile pay. | See section 9. |

## 6. Business rules
- BR-O1 A gateway is usable only if the platform offers it **and** the shop enabled it **and** its keys are complete (MoMo needs the access key, ZaloPay key 2).
- BR-O2 The shop is always found from the **payment** (its reference), never from the request host or the message body; the keys of that shop verify the message.
- BR-O3 The reference read from a message before verification is used only to find the shop; the verified reference must be the same.
- BR-O4 Online gateways settle **VND** only; a payment in another currency is refused at checkout.
- BR-O5 A payment that is *Captured* or *Failed* is never changed by a repeated notification.
- BR-O6 Starting a payment again is allowed only while it is *Pending*. A *Failed* payment cancelled its order; the customer places a new one.
- BR-O7 A COD payment is captured only by the delivery event; cancelling the order cancels it.
- BR-O8 The result page decides from the payment's status, not from the address the browser came back with.
- BR-O9 Public callback routes are anonymous **only** because the signature is verified before anything is applied; every other payment-gateway route needs a signed-in user.
- BR-O10 An order that did not name a method keeps the pre-P1-10 behaviour (waits for a payment; credit card / USD).

## 7. Use cases
**UC-O1 Pay online.** Main: customer chooses MoMo → places the order → is sent to MoMo → pays → returns to the result page → "Payment received". Alternative: the customer closes the page → the order shows *waiting for payment* and *Pay now*. Exception: the gateway is unreachable → the order exists; the result page offers *Pay now* again; the gateway refuses (e.g. MoMo error) → 400 with its message.
**UC-O2 Pay on delivery.** Main: choose COD → order → stock reserved → delivered → payment captured → Accounting books the cash. Alternative: order cancelled before delivery → the pending payment is cancelled.
**UC-O3 Set up a gateway.** Main: administrator opens *Settings → Payment → Tenant*, enters the account for MoMo / ZaloPay / VNPay, enables it. Exception: missing keys → the method is simply not offered.
**UC-O4 Refund.** Main: administrator opens *Payments*, *Refund*, amount + reason → sent to the gateway → *Refunded / Partly refunded* when confirmed. Exception: the gateway refuses → refund *Failed*, payment unchanged.
**UC-O5 Suspicious notification.** Main: forged or wrong-amount message → refused and (for a wrong amount) the payment is flagged for a person.

## 8. Data and state
| Item | Content |
|---|---|
| `TenantGatewayCredentials` (Payment 008) | shop, provider, enabled, sandbox, public id, access key / secrets **encrypted** |
| `SystemPaymentSettings` | + `MoMoAvailable`, `ZaloPayAvailable`; `TenantPaymentSettings` + `CodEnabled` |
| `Payments` | + `Provider` (the customer's choice), `CustomerId`, `OrderNumber`; `GatewayProvider` is set when it is paid |
| `SalesOrders.PaymentMethod` (Sales 013) | COD / VNPay / MoMo / ZaloPay / Unspecified (null on old orders) |
| `InitiatePaymentCommand` (saga → Payment) | + `PaymentMethod`, `Currency`, `CustomerId`, `OrderNumber` |
| Payment status | Pending → Authorized → Captured → (Partly) Refunded; Pending → Failed; Pending → Cancelled. Request statuses are unchanged. |

## 9. Screens
- **Admin → Settings → Payment → Tenant**: the existing shop form gains *Offer cash on delivery*; below it three cards, **VNPay / MoMo / ZaloPay**, each with Enabled, Sandbox, the public id (TMN code / partner code / app ID), secret boxes that show "set — leave blank to keep" (MoMo: access key + secret key; ZaloPay: key 1 + key 2) and, for MoMo and ZaloPay, the address to give the gateway for notifications. *System* tab: MoMo and ZaloPay platform switches.
- **Admin → Payments**: columns order number, method (provider + type), amount in the payment's own currency, status with a "flagged for review" note and the failure reason; filter *Partly refunded*; **Refund** button (needs `payments:refund`) with amount and reason.
- **Storefront → Checkout → step 2**: the list of methods the shop offers, each with a one-line hint; button *Place order and pay* for an online method. **Payment result** page (`/payment/result`): ⏳ waiting / ✅ received / ❌ failed, polling, *Pay now / Pay again*. **Order confirmation** shows "pay the courier on delivery" or the payment status.
- **Mobile → Cart**: pill buttons for the methods; an online method opens the gateway in the browser. New **Order** screen: lines, total, payment status (refreshes every 3 s and when the app returns to the foreground), *Pay now*.

## 10. Integrations
Sales → Payment: `InitiatePaymentCommand` / `RefundPaymentCommand` (RabbitMQ `sales_order_events`; a refund command with no payment id and an order id means "close this order's pending payment"). Payment → Sales / Accounting / Notification: `PaymentProcessed`, `PaymentFailed`, `PaymentRefunded` (Kafka `payment-topic`, unchanged). Shipping → Payment: `DeliveryConfirmed` (Kafka `shipping-topic`, new consumer, 5 attempts). Gateway routes (Ocelot): `GET /api/v1/payment-gateway/callback/{provider}` and `GET|POST …/ipn/{provider}` are anonymous; everything else under `payment-gateway` needs a bearer token. Configuration: `Security:SettingsEncryptionKey` (now also on PaymentService in Docker), `Payment:PublicBaseUrl` (the public address the gateways use when the shop's own host is not reachable from outside, e.g. a tunnel), provider hosts as in section 4.

## 11. Assumptions and open issues
- Public **sandbox** accounts were used (MoMo test account; ZaloPay app 2554). A real merchant account of each was not available.
- The providers cannot call a local Docker stack, so their **notifications were simulated** (signed with the same stored secrets). Create and refund calls were real sandbox calls.
- The order has no currency; online methods and COD are sent as VND.
- Open issues: DEFERRED §4u and the risk register §6 (R11…R18).

## 12. UAT scenarios
| ID | Scenario | Expected |
|---|---|---|
| UAT-O1 | Shop without keys | checkout offers only COD |
| UAT-O2 | Save MoMo and ZaloPay accounts | offered: COD, MoMo, ZaloPay; GET shows only "has key" flags |
| UAT-O3 | Order with an unknown method | 400 |
| UAT-O4 | MoMo order → checkout | payment page address from MoMo; reference stored; status *Pending* |
| UAT-O5 | Another customer reads / starts that payment | 404 |
| UAT-O6 | Notification with a bad signature | refused (400), payment unchanged |
| UAT-O7 | Notification for the wrong amount | refused, payment pending and flagged |
| UAT-O8 | Correct paid notification | 204, payment captured, order moves to stock; repeat → 204, nothing doubled |
| UAT-O9 | Two checkouts in a row | different references |
| UAT-O10 | Failed notification | payment failed, order cancelled, checkout → 400 |
| UAT-O11 | ZaloPay order → checkout → server callback → signed browser return | sandbox page address; captured; redirect to result with `paid`; forged return → `invalid` |
| UAT-O12 | COD order | stock reserved without waiting; payment *Pending (cash)*; checkout → "paid on delivery" |
| UAT-O13 | Delivery event for the COD order, then again | captured once; unchanged |
| UAT-O14 | Refund a captured MoMo payment | MoMo is called; its refusal is recorded; payment stays *Captured* |
| UAT-O15 | Order's pending payment cancelled, then a paid notification | 204; payment *Cancelled*, flagged "refund the customer" |
| UAT-O16 | Unknown provider in the callback address | 400 |

Verified live on tenant `glcheck`, 2026-10-27: UAT-O1…O16 (notifications and the delivery event simulated as stated in section 11; UAT-O14 against the real MoMo sandbox, which refuses a fake transaction).
