# Payment — Admin screen: Payments (list and refund)

Business analysis of the **Payments** screen of the Admin as shipped on 2026-11-02 (ROADMAP P2-9, U3 batch 8). The payment rules (gateways, IPN, COD capture per parcel, fraud flag) are in [OnlinePayments.md](OnlinePayments.md); this document describes what the screen does for the finance clerk. Vietnamese user guide: [docs/user-guides/payments-shipping-sales-logs.md](../../user-guides/payments-shipping-sales-logs.md).

## 1. Purpose and scope
Let finance and support see every payment of the shop, find the ones that need a person (flagged for review, money for a cancelled order) and **send money back to a customer**. Out of scope today: refund history per payment, partial-refund balance on the screen, manual capture, export.

## 2. Actors and permissions
Finance clerk: `payments:refund` (the **Refund** button). Anyone who can open the page can read the list.

## 3. State model and rules
`Pending → Captured | PartiallyCaptured → PartiallyRefunded → Refunded`; `Failed`. COD captures per delivered parcel (`PartiallyCaptured` until the last parcel). **Refund** is offered for Captured, PartiallyCaptured and PartiallyRefunded payments.
- Amount ≥ 1 and ≤ the payment amount (the screen proposes the captured amount); a written reason is mandatory.
- The refund is sent to the provider that took the money with the shop's own keys; **the payment changes only when the gateway confirms** (`PaymentRefunded` → Accounting). A **COD** refund is recorded and nothing is sent to a gateway.
- **New in this batch:** pressing Refund does not send it. A second dialog states the amount and the provider ("…returned to the customer through VNPay; this cannot be undone" / for COD "nothing is sent to a gateway") and only **Send refund** posts. A refusal by the server is shown inside the refund dialog.

## 4. Requirements with acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| PA-1 | Money never goes back with one click | Refund opens a confirmation naming amount and provider; no request before it is confirmed; Esc on the confirmation keeps the refund dialog, a second Esc closes it |
| PA-2 | The form validates first | Empty reason → "Bắt buộc" (linked); amount below 1 or above the payment → "Enter an amount from 1 to …"; nothing asked |
| PA-3 | Refusals are explained | The server's reason appears in the dialog |
| PA-4 | Lists are usable | Status filters are a pressed-state group; provider and "needs attention" are labelled; filter with no result → "Không có kết quả" + Xóa lọc; flagged payments carry a text badge, not only a tooltip |
| PA-5 | Accessible | Dialogs trap focus; axe finds no violation |

## 5. Use cases
**UC-1 Refund a captured online payment:** find the payment → Refund → amount, reason → Refund → read the confirmation → Send refund → the payment changes when the gateway answers. *Exception:* the server refuses (for example more than was captured) → reason in the dialog.
**UC-2 Clear a flagged payment:** filter "needs attention" → money that arrived for a cancelled order → refund the customer.
**UC-3 Refund cash on delivery:** same dialog; the text says it is only recorded.

## 6. Data, integrations, reports
Payment(Id, Order, Provider, Method, Amount, CapturedAmount, Currency, Status, FailureReason, IsFlaggedForReview, FraudReviewReason). Events `PaymentRefunded` (Accounting, Reporting, Notification). No reports on the page.

## 7. Open issues (DEFERRED R95)
The ceiling uses the payment amount, not "captured − already refunded"; the server decides; there is no refund history; the walk tenant had no refundable payment, so the dialog was tested automatically only.

## 8. UAT scenarios
1. Payments → a Captured VNPay payment → **Refund** → Save with an empty reason → "Bắt buộc".
2. Amount 900 000 on a 500 000 payment → "Enter an amount from 1 to …".
3. Valid amount and reason → confirmation says "through VNPay … cannot be undone" → Esc → refund dialog still open → Esc → closed, nothing sent.
4. Confirm **Send refund** → a gateway refusal shows its reason in the dialog.
5. COD payment → the confirmation says nothing is sent to a gateway.
6. Filter Failed with no result → "Không có kết quả" + Xóa lọc.
