# Payment and Sales — Business Analysis: refunds and returns (the money side)

| | |
|---|---|
| **Domains** | Payment (PaymentService, port 5009, code PAY) and Sales (SaleOrderService, port 5002, code SO) |
| **Covers** | ROADMAP P1-1e: a gateway-confirmed **payment refund** and a completed **sales return**, and how both reach the ledger. The wider designs are [module-11-payment.md](module-11-payment.md) and [module-05-sales.md](../Sales/module-05-sales.md). |
| **Status** | Implemented and verified on the local Docker stack, 2026-10-24 (unit tests PAY 7, SO 6). Written afterwards, 2026-10-25, to the standard of [BA-Accounting.md](../Accounting/BA-Accounting.md). |
| **Audience** | Product owner, customer-service lead, finance, chief accountant, QA, developers |
| **Companion documents** | [BA-Accounting.md](../Accounting/BA-Accounting.md) (FR-03, FR-11, FR-12, UAT-11 … 13), [AutoPosting.md](../Accounting/AutoPosting.md), [BA-Warehouse.md](../Warehouse/BA-Warehouse.md) |

> **Limit.** Only the **money** is covered. Putting returned goods back into stock and reversing their cost, triggering the refund from the return automatically, and return lines are ROADMAP **P1-5**. Today the return and the refund are two separate steps done by two people.

---

## 1. Business context and objectives

### 1.1 Problem
* `PaymentRefunded` was published when the refund was **requested**, so a refund the gateway then **declined** still reached other services and looked real;
* the event had no refund id, currency, method, time or gateway reference, so Accounting could not tell two refunds of one payment apart (idempotency) or book cash against bank;
* an over-refund surfaced as a server error (500);
* the sales return event carried a refund amount that nothing validated (zero, negative, above the order total);
* cancelling a **paid** order left the revenue and the invoice in the books.

### 1.2 Objectives
| # | Objective | How we know it is met |
|---|---|---|
| O1 | Only a refund the gateway **confirmed** is announced, once, with enough data to book it. | PAY-FR-01 … 03; UAT-RF-01, 02 |
| O2 | An impossible refund is refused with a clear message, not an error. | PAY-FR-04; UAT-RF-03 |
| O3 | A completed return credits the customer's account for a validated amount. | SO-FR-01, 02; UAT-RF-04 |
| O4 | The customer's receivable nets to zero after a return and its refund. | UAT-RF-05 |

### 1.3 Scope
**In scope:** requesting and completing a refund, the status of the payment, the refund event; requesting and completing a return, the return event; cancelling a paid order (ledger effect only).
**Out of scope:** restock and cost reversal of returned goods, return lines, automatic refund on return, refund idempotency key and gateway-timeout reconciliation (see 15).

---

## 2. Stakeholders and actors

| Actor | Who | What they do |
|---|---|---|
| Customer | Storefront user | Requests a return of a delivered order. |
| Customer-service agent | `sales_order:update` | Registers the return request on the customer's behalf. |
| Return approver | `sales_order:approve` | Completes the return and **types the refund amount**. |
| Finance operator | `payments:refund` | Refunds a captured payment through the gateway. |
| Payment gateway | External | Confirms or declines the refund. |
| Accounting (system) | AccountingService | Books the refund and the return. |
| Warehouse (system) | WareHouseService | Will restock returned goods (P1-5; not yet). |

---

## 3. Glossary

| Term | Meaning |
|---|---|
| Refund | Money sent back to the customer for a captured payment, through the same gateway. |
| Confirmed refund | A refund the gateway reported as completed (status Completed). |
| Return | The business act: a delivered order's goods come back. States ReturnRequested → Returned. |
| Refund amount (of a return) | The amount the approver credits to the customer; at most the order total. |
| Credit note | The ledger effect of a return: Dr Sales returns / Cr Receivables. |

---

## 4. End-to-end business processes

### 4.1 Refund a payment
1. Finance requests a refund on a **captured** (or partly refunded) payment: amount and reason.
2. Payment checks the amount (total confirmed refunds + this one ≤ the payment) and creates the refund; the gateway is called.
3. **Gateway confirms** → the refund is Completed, the payment becomes Refunded (fully) or PartiallyRefunded, and `PaymentRefunded` is published with the refund id, amount, currency, method, time and gateway reference.
   **Gateway declines or fails** → the refund is Failed, the payment returns to Captured (or its earlier partial state), **nothing is published**.
4. Accounting books *Dr Receivables / Cr Cash (method Cash) or Bank* for the refund and lowers the invoice's paid amount; a fully paid invoice that is no longer covered is open again.

### 4.2 Return an order
1. A delivered order → **return requested** (customer or agent, with a reason).
2. The approver **completes the return** with a refund amount (> 0 and ≤ order total) → order Returned → `SalesOrderReturned` published (order, customer, reason, refund amount).
3. Accounting books *Dr Sales returns / Cr Receivables* for that amount.
4. The money back to the customer is a **separate** refund (4.1). Once both are done the order's receivable nets to zero.

### 4.3 Cancel a paid order (ledger effect)
Cancelling an order cancels its invoice and reverses the revenue entry whether or not it was paid. The customer's money stays as a credit on receivables until the refund entry arrives (4.1).

---

## 5. Functional requirements

| ID | Requirement | Pri | Source |
|---|---|---|---|
| PAY-FR-01 | `PaymentRefunded` is raised **only** when the gateway confirms the refund. A declined or failed refund publishes nothing. | M | P1-1e |
| PAY-FR-02 | The event carries refund id, order id, amount, reason, currency, method, completion time and gateway refund id. | M | P1-1e |
| PAY-FR-03 | The payment's status follows only **confirmed** refunds: Captured → PartiallyRefunded → Refunded. | M | P1-1e |
| PAY-FR-04 | A refund above the captured amount, or on a payment that is not captured, is refused with HTTP **409** and a message. | M | P1-1e |
| PAY-FR-05 | Payment events are keyed by the payment id so events of one payment arrive in order. | M | P1-1e |
| SO-FR-01 | A return can be requested only for a **Delivered** order, with a reason. | M | existing |
| SO-FR-02 | Completing a return needs a refund amount **> 0 and ≤ the order total**; otherwise refused. | M | P1-1e |
| SO-FR-03 | `SalesOrderReturned` carries order, customer, reason, refund amount and time. | M | P1-1e |

### 5.1 Acceptance criteria
| ID | Acceptance criteria | UAT |
|---|---|---|
| PAY-FR-01, 02 | Given a captured payment of 1,000 and a refund of 400 confirmed by the gateway, then one `PaymentRefunded` with all the fields; given the gateway declines, then none. | UAT-RF-01, 02 |
| PAY-FR-03 | Given 400 then 600 confirmed, then the status goes PartiallyRefunded then Refunded; given a declined refund, then the status is as before. | UAT-RF-01, 02 |
| PAY-FR-04 | Given a captured payment of 1,000 with 400 refunded, when 700 is requested, then 409 "Refund amount exceeds original payment amount."; given a pending payment, then 409 "Cannot refund payment in Pending state." | UAT-RF-03 |
| SO-FR-02 | Given an order of 1,000, when the return is completed with 0, −5 or 1,200, then refused; with 1,000, then Returned. | UAT-RF-04 |
| SO-FR-03 | Given the completed return, then the event is published to the sales exchange once. | UAT-RF-04 |

---

## 6. Business rules

| ID | Rule |
|---|---|
| RF-BR-01 | **Only confirmed money moves the books.** A refund requested but not confirmed (Pending, Processing, Failed) is invisible to Accounting. |
| RF-BR-02 | **Cap:** confirmed refunds + the new refund ≤ the payment amount. |
| RF-BR-03 | A refund is possible only from **Captured** or **PartiallyRefunded**. |
| RF-BR-04 | **Cash vs bank:** a refund of a payment made by method *Cash* goes out of the Cash account; every other method from the Bank account. |
| RF-BR-05 | **Idempotency:** each refund is booked once, identified by its refund id (`PaymentRefund:<refundId>`); a redelivered event creates nothing. |
| RF-BR-06 | A refund lowers the invoice's paid amount by the refund; a paid invoice no longer fully covered becomes open (Unpaid) again; a cancelled invoice keeps its status. |
| RF-BR-07 | A completed return books the **typed refund amount**, not a figure computed from lines (there are no return lines yet), once per order (`SalesReturn:<orderId>`). |
| RF-BR-08 | A return can be requested only for a delivered order and completed only from ReturnRequested. |
| RF-BR-09 | The return and the refund are independent: completing a return does **not** refund the money, and a refund does not complete a return. Both together net the order's receivable to zero. |
| RF-BR-10 | Every event carries the tenant; consumers drop events without one. |

*Worked example.* Order 1,000, invoice 1,000, payment 1,000 captured (receivable 0). Refund 400 confirmed → *Dr Receivables 400 / Cr Bank 400*, invoice paid amount 600, invoice open again. Return completed with 1,000 → *Dr Sales returns 1,000 / Cr Receivables 1,000* (receivable −600). Refund of the remaining 600 → *Dr Receivables 600 / Cr Bank 600* → receivable 0.

---

## 7. Use cases

### UC-RF-01 Refund a payment
*Actor:* finance operator (`payments:refund`). *Pre:* payment Captured / PartiallyRefunded. *Main flow:* enter amount and reason → refund created → gateway confirms → refund Completed, payment status updated, event published → ledger booked. *Alternatives:* gateway declines → refund Failed, nothing published, retry possible. *Exceptions:* amount above the payment, payment not captured → 409 with the message.

### UC-RF-02 Request a return
*Actor:* customer or agent (`sales_order:update`). *Main flow:* choose a delivered order, give a reason → order ReturnRequested. *Exception:* order not delivered → refused.

### UC-RF-03 Complete a return
*Actor:* approver (`sales_order:approve`). *Main flow:* inspect the return, type the refund amount → order Returned → event → ledger credit note. *Exceptions:* amount ≤ 0 or above the order total → refused; order not ReturnRequested → refused.

### UC-RF-04 Cancel a paid order
*Actor:* sales / customer. *Main flow:* cancel → invoice cancelled, revenue reversed → the customer's payment stays as a credit until refunded (UC-RF-01).

---

## 8. Data requirements

| Entity | Key attributes |
|---|---|
| Payment | order, amount, currency, method, status (Pending, Authorized, Captured, Failed, Cancelled, Refunded, PartiallyRefunded), refunds |
| Refund | payment, amount, reason, status (Pending, Processing, Completed, Failed), completed at, gateway refund id |
| SalesOrder | status (… Delivered, ReturnRequested, Returned), return reason, return requested / returned at, refund amount |
| Invoice (Accounting) | amount, **amount paid** (goes down on a refund), status |

**State models:** Payment Captured → PartiallyRefunded → Refunded (by confirmed refunds). Refund Pending / Processing → Completed | Failed. Order Delivered → ReturnRequested → Returned.

---

## 9. Integrations

| Direction | Event | Transport | Content / effect |
|---|---|---|---|
| Payment → Accounting | `PaymentRefunded` | Kafka `payment-topic`, key = payment id | refund id, amount, currency, method, time → *Dr Receivables / Cr Cash or Bank*; invoice paid amount down |
| Sales → Accounting (and others) | `SalesOrderReturned` | RabbitMQ `sales_order_events` | refund amount → *Dr Sales returns / Cr Receivables* |
| Sales → Accounting | `SalesOrderCancelled` | RabbitMQ | cancel invoice, reverse revenue |

The Warehouse and Shipping services do not yet react to `SalesOrderReturned` (P1-5).

---

## 10. Reports

Payment and refund lists per order; Accounting's account ledger of receivables shows the invoice, the payment, the refund and the credit note for one order, netting to zero.

---

## 11. Security, audit and non-functional requirements

| Area | Requirement |
|---|---|
| Permissions | `payments:refund` (refund), `payments:view`, `sales_order:update` (request a return), `sales_order:approve` (complete a return). |
| Segregation | The person who completes the return (`sales_order:approve`) differs from the person who refunds the money (`payments:refund`) by permission. |
| Tenant isolation | Payments, refunds and orders are tenant-owned; events carry the tenant (signature-verified gateway callbacks resolve their payment's tenant). |
| Audit | Refunds keep reason, gateway reference and time; returns keep reason and timestamps. |
| Reliability | Refusals are HTTP 409 / 400, not 500; events go through the outbox with the state change. |

---

## 12. UAT scenarios

Verified on the local Docker stack on 2026-10-24 (SYSTEM tenant) unless marked unit test.

| # | Given / When / Then | Requirement | Result |
|---|---|---|---|
| UAT-RF-01 | A 1,000 payment, refund 400 confirmed → *Dr 1100 / Cr 1000 400*; invoice paid 600 and open again; payment PartiallyRefunded. | PAY-FR-01 … 03 | Passed |
| UAT-RF-02 | The gateway declines a refund → no event, no entry, status unchanged. | PAY-FR-01 | Unit test only |
| UAT-RF-03 | Refund above the payment, or on a non-captured payment → 409 with the message. | PAY-FR-04 | Passed |
| UAT-RF-04 | An order of 1,000 returned with 1,000 → *Dr 4000 / Cr 1100 1,000*; amounts 0 / −5 / 1,200 are refused. | SO-FR-02, 03 | Passed (event) / unit test (validation) |
| UAT-RF-05 | After UAT-RF-01 and 04, the refund of the remaining 600 makes the order's receivable 0. | RF-BR-09 | Passed |
| UAT-RF-06 | Cancelling a paid order cancels the invoice and reverses revenue; cash stays as a credit. | 4.3 | Unit test only |

---

## 13. Roles and permissions matrix

| Action | Permission |
|---|---|
| List / view payments | `payments:view` |
| Refund a payment | `payments:refund` |
| Request a return | `sales_order:update` |
| Complete a return (type the refund amount) | `sales_order:approve` |

---

## 14. Assumptions and constraints

1. The refund goes back through the **same gateway and method** as the payment; the ledger account follows the method (cash vs bank).
2. The refund amount of a return is trusted from the approver (bounded by the order total) until return lines exist.
3. One currency (VND) in the ledger; the event carries the payment currency but it is not converted.
4. A return is for the whole order's value up to the typed amount; partial returns are expressed by a smaller amount, not by lines.

---

## 15. Out of scope and open issues

* **ROADMAP P1-5:** restock and cost reversal of returned goods; refund started automatically from the return; return lines; the saga that ties return, restock and refund.
* **Not built:** an **idempotency key** on the refund request (a repeated click can create a second refund), reconciliation of refunds when the gateway times out, partial-refund policy limits, tax (VAT) on the credit note (P1-3). See [DEFERRED-ISSUES.md](../../DEFERRED-ISSUES.md).

---

## 16. Traceability and change log

| Task | Date | Requirements | Technical documents | Main tests |
|---|---|---|---|---|
| P1-1e refunds and returns to the ledger | 2026-10-24 | PAY-FR-01 … 05; SO-FR-01 … 03; RF-BR-01 … 10 | AutoPosting.md, event-catalog | `RefundEventTests` (PAY 7, new project), `CompleteReturnTests` (SO), `RefundPostingTests` (ACC) |
| BA baseline (this document) | 2026-10-25 | All | — | — |
