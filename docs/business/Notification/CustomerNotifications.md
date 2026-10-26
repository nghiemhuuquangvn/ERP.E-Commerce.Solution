# Notification — customer messages, loyalty and coupons, CRM, reporting facts (ROADMAP P1-6)

Business analysis written 2026-10-26. The design-spec style document of the service is `module-21-notification.md`; this document describes what the system now does for the customer and who sees it. Related: `docs/business/Sales/OrderProcessing.md` (the order flow that raises the events), `docs/business/HRM/Payroll.md` (the pay slip facts).

## 1. Purpose and scope
Before P1-6 a customer almost never got a message: the order, payment and shipment events carried no e-mail address, the seeded templates used other placeholder names than the handlers sent, order messages were addressed to the order id, a failed handler lost the Kafka message, and CRM, coupon and loyalty events had no consumer. Now:
- every important step of an order and its payment writes to the customer's e-mail, once;
- loyalty points and coupon use are announced, and the points of a cancelled or returned order are taken back;
- a resolved support case tells the customer; a won opportunity reaches Reporting;
- Reporting stops reading a topic nobody writes, tells a partial refund from a full one, and reads the pay slip's real figures;
- administrators can see what was sent (and what failed) and read the templates.

Out of scope (DEFERRED §4r): editing or deleting templates (P1-12), SMS / push for these messages, a customer-facing message centre, delivery receipts from the mail provider, language per customer, a "send again" button, earning points on payment instead of order creation, Reporting's inventory facts (no producer for `warehouse-topic`).

## 2. Actors
| Actor | Role |
|---|---|
| Customer / contact | Receives the messages; can turn the e-mail channel off in their preferences (then they are skipped quietly) |
| Administrator | Reads the history and the templates (`notifications:send` for the history, `notifications:view` for the templates) |
| Sales, Payment, Shipping, Promotion, CRM | Publish the events |
| Notification | Looks up the recipient, renders the template, sends |
| Reporting analyst | Reads the facts |

## 3. Messages
Template codes live as platform defaults (tenant `SYSTEM`, seeded by migration Notification 013); a tenant overrides one by creating its own with the same code. Placeholders are case-insensitive (`{OrderNumber}` = `orderNumber`); the subject is rendered too.

| Event (source) | Template | Recipient | One message per | Variables |
|---|---|---|---|---|
| SalesOrderCreated (Sales, RabbitMQ) | ORDER_CONFIRMATION | order's customer e-mail | order | customerName, orderNumber, orderDate, totalAmount |
| PaymentProcessed (Payment, Kafka) | PAYMENT_RECEIPT | order recipient | payment | amount, currency |
| ShipmentCreated (Shipping) | SHIPMENT_NOTIFICATION ("being prepared") | order recipient | shipment | carrier |
| SalesOrderShipped | ORDER_SHIPPED | order recipient | order | carrier, trackingNumber |
| SalesOrderDelivered | ORDER_DELIVERED | order recipient | order | totalAmount |
| SalesOrderCancelled | ORDER_CANCELLED | order recipient | order | reason |
| SalesOrderReturned | ORDER_RETURNED | order recipient | order | reason, refundAmount |
| PaymentFailed | PAYMENT_FAILED | order recipient | payment | amount, reason |
| PaymentRefunded | PAYMENT_REFUNDED | order recipient | refund | amount |
| LoyaltyPointsEarned (Promotion) | LOYALTY_POINTS_EARNED | account e-mail | earn (user + reason) | points, totalPoints, reason ("for your order") |
| CouponRedeemed | COUPON_REDEEMED | account e-mail | redemption | code, discount |
| CaseResolved (CRM) | CASE_RESOLVED | contact e-mail (carried by the event) | case | title |

## 4. Requirements
| ID | Requirement | Acceptance |
|---|---|---|
| FR-N1 | `SalesOrderCreated` carries `CustomerEmail` and `OrderNo`; Notification stores them per order (`OrderRecipients`) | live: row written, order number in the subject |
| FR-N2 | Order, payment and shipment events are addressed to the customer of the order found by order id; `RecipientId` is the customer id | message to the e-mail given at order creation |
| FR-N3 | No known e-mail: the message is skipped and logged, never sent to a made-up address | unit test |
| FR-N4 | Each business fact is notified once: a dedup key per (template, fact) unique per tenant; a redelivered event is a no-op | unit tests; unique index |
| FR-N5 | A recipient who turned the channel off is skipped quietly by event handlers (no retry loop); a direct send is still refused | unit test |
| FR-N6 | Templates render the subject and the body, placeholders case-insensitive | unit test; live subjects rendered |
| FR-N7 | Notification consumers retry a failing message 5 times (RabbitMQ: attempt header; Kafka: bounded loop) and then log it with its payload; no unlimited requeue, no silent loss | code; error log line |
| FR-N8 | Loyalty points are earned once per order and taken back when the order is cancelled or returned (never below zero; the tier follows) | live: Earn 200000 / Reverse -200000, tier back to Bronze |
| FR-N9 | Notification announces points earned and coupon use to the account's e-mail (from `UserCreated`); a user without a known e-mail gets nothing | live (account contact row) |
| FR-N10 | CRM `OpportunityWon` carries the contact and name, `CaseResolved` the contact, title, e-mail and name; old events still read | unit tests |
| FR-N11 | Reporting: won opportunities become `FactCrmOpportunities`; refunds are stored per refund (`FactPaymentRefunds`) and the payment shows Processed / PartiallyRefunded / Refunded with its refunded amount; the pay slip fact carries the net pay (it was always 0), gross, deductions, income tax and employer insurance, and a cancelled slip is removed; a returned order shows "Returned"; the dead Kafka `sales-order-*` consumer is removed | live and unit tests |
| FR-N12 | Admin: Settings → Sent notifications (all messages of the tenant with status, channel, search, detail with log) and Message templates (read-only, origin badge) | live API 200; screens (section 8) |

## 5. Business rules
- BR-N1 The customer's address is the one given on the order at creation; changing the customer's e-mail later does not change an existing order's recipient.
- BR-N2 A message about money (payment, refund) says "{amount} {currency}"; order totals are shown without currency (the order has none).
- BR-N3 A tenant's template overrides the platform default of the same code; the list shows one row per code.
- BR-N4 Points are earned at order creation (unchanged) and reversed on cancellation or return; points already spent are not recovered (the balance stops at 0).
- BR-N5 A failing send (no mail server, bad address) leaves the notification **Failed** with its error; the dispatch job retries it (3 times, growing pause). It is not lost and not sent twice.
- BR-N6 Only a payment's own refunds count toward "refunded": Refunded means the refunds add up to the payment, PartiallyRefunded less than that.

## 6. Use cases
**UC-N1 A customer follows an order by e-mail.** Main: order placed → confirmation; paid → receipt; shipment prepared → "being prepared"; shipped → tracking number; delivered → delivered; returned → return complete and refund. Alternative: cancelled → cancelled message (and refund message when paid). Exception: no e-mail on the order → nothing is sent; customer opted out of e-mail → skipped.
**UC-N2 An administrator checks what was sent.** Main: Settings → Sent notifications → filter by status / channel or search the e-mail → *View* shows the text and the delivery log. Alternative: a Failed row shows the error, the number of retries.
**UC-N3 An administrator reads the wording.** Main: Settings → Message templates → *View* shows subject, body and the placeholders. Exception: editing is not offered.
**UC-N4 A customer earns and loses points.** Main: order placed → points earned → message; order returned → points reversed (no message).
**UC-N5 A support case is resolved.** Main: agent resolves the case → the contact receives the message with the case title.

## 7. Data and integration
Tables (migration Notification 013): `OrderRecipients` (OrderId, CustomerId, OrderNo, name, e-mail, phone), `UserContacts` (UserId, e-mail, name), `Notifications.DedupKey` (unique per tenant when set), 12 platform templates inserted or updated. Reporting 008: `FactCrmOpportunities`, `FactPaymentRefunds`, pay slip columns. Events: new optional fields on `SalesOrderCreated` (CustomerEmail, OrderNo), `OpportunityWon`, `CaseResolved`; Notification now subscribes to the Sales exchange (Created, Approved, Shipped, Delivered, Cancelled, Returned), `payment-topic` (+ Failed, Refunded), `promotion-topic`, `crm-topic`; Promotion subscribes to Cancelled and Returned; Reporting to `crm-topic` and Returned.

## 8. Screens
- **Settings → Sent notifications** (`/settings/notification-history`, permission `notifications:send`): filters *All statuses* (Queued, Sending, Sent, Delivered, Failed, Bounced), *All channels* (Email, Sms, Push, InApp) and a search box (e-mail or subject); table Time, Recipient, Channel, Subject, Status badge, Error, *View*; *View* opens the subject, recipient, channel, status, the full text, the error with the retry count and the delivery log.
- **Settings → Message templates** (`/settings/notification-templates`, `notifications:view`): table Code, Name, Channel, Subject, Source badge (*Platform default* grey / *Your own* blue), *View* opens subject, body and the placeholders ({orderNumber} …).
- The customer-facing result is the e-mail text. Admin screens of other services are unchanged.

## 9. Assumptions and open issues
Mail is sent by SMTP; in the local stack there is no mail server, so every message ends **Failed — "Failure sending mail."** after its retries (content, recipient and dedup are still verified). Message wording is the project's; marketing and legal wording needs the owner's review. Open: DEFERRED §4r.

## 10. UAT
| ID | Scenario | Expected |
|---|---|---|
| UAT-N1 | Create an order with a customer e-mail | `OrderRecipients` row; "Order #SO-… confirmed" to that e-mail |
| UAT-N2 | Capture its payment | "Payment received for order #SO-…" |
| UAT-N3 | Approve; saga completes | "…is being prepared for delivery" |
| UAT-N4 | Ship, then the carrier confirms delivery | "…has shipped" (tracking), "…was delivered" |
| UAT-N5 | The buyer has an account e-mail | "You earned 200,000 points" to that e-mail |
| UAT-N6 | Return the whole order with refund | "Your return … is complete", "Refund for order …"; points reversed (Earn 200000, Reverse -200000) |
| UAT-N7 | Cancel an unpaid order | "…was cancelled" |
| UAT-N8 | Refund 100,000 then 200,000 of a 300,000 payment | Reporting PartiallyRefunded (100,000) then Refunded (300,000); two refund messages |
| UAT-N9 | Win an opportunity | row in `FactCrmOpportunities` (name, value) |
| UAT-N10 | Resolve a case of a contact with an e-mail | "Your support case was resolved: …" |
| UAT-N11 | A message for an order without recipient / a replayed event | skipped / no second message (unit tests; unique dedup index; 0 duplicate keys live) |
| UAT-N12 | History API with status and search filters; templates API | 200; 21 templates, all platform defaults |

Verified live on tenant `glcheck` 2026-10-26: UAT-N1…N10, N12; N11 by unit tests and the duplicate-key query.
