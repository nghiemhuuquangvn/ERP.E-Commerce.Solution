# Order processing across services — saga timeouts, shipment failures, delivery, returns (ROADMAP P1-5)

Business analysis of what happens to an order after it is placed: how the system keeps it moving, what it does when a step does not answer or fails, how the carrier's delivery confirmation reaches the order, and what a completed return sets in motion in Warehouse, Payment and Accounting. Companion of [SalesWorkflow.md](SalesWorkflow.md) (the happy path) and [BA-Accounting.md](../Accounting/BA-Accounting.md) (the ledger side, FR-158 … 159).

## 1. Context and objectives
Before P1-5 the order saga (`OrderFulfillmentSaga`) compensated only a failed payment and insufficient stock. A step that never answered left the order in "processing" for ever (the recovery job only wrote a log line), a shipment that could not be created was dropped silently, the carrier's `DeliveryConfirmed` was ignored, and a completed return only reached Accounting: the goods stayed out of stock and the customer was never refunded. Objectives:
1. No order waits for ever: every automatic step has a time limit, is pushed again, and then compensated.
2. A shipment that cannot be created ends the order cleanly (refund, cancellation, stock released).
3. The carrier's confirmation marks the order Delivered without a person.
4. A completed return puts the goods back on the shelf at the cost they left at and refunds the customer; the ledger follows.
5. A person can see where an order is stuck and push it.

**In scope:** the four flows above, the *Order processing* card on the order page. **Out of scope:** partial returns by line (a return is of the whole order), per-tenant timeout settings, moving the saga onto the outbox, an inbox for dead-lettered messages (see §13).

## 2. Actors
| Actor | Role |
|---|---|
| Order handler (`sales_order:update`) | Sees the card, pushes a stuck step |
| Order approver (`sales_order:approve`) | Approves the order (the one step the system never retries); completes a return |
| Carrier / Shipping staff | Confirms delivery (`shipping:update`) |
| Warehouse, Payment, Accounting | React to events; no screen changes |
| System (`SagaRecoveryHostedService`) | Detects stuck steps, pushes them, compensates |

## 3. Process
**3.1 The saga.** Order placed → *Payment* (Payment service) → *Stock reserved* (Warehouse) → *Approval* (a person; may also happen earlier) → *Shipment* (Shipping) → Done. Each step publishes a command and waits for the answering event.

**3.2 Timeouts and retries.** A Running saga that stays in Payment, Stock or Shipment longer than the step's limit (defaults 15 / 10 / 15 minutes, configuration `Saga:*`) has the step's command published again; every receiver is idempotent (payment per order, reservation per order, shipment per order), so a late answer to the first command does no harm. After `MaxRetries` (default 3) pushes the saga gives up: it is marked Failed, the payment (if any) is refunded, the order is cancelled and the reservation released. Approval has no limit. A person can push the current step at once (*Retry this step now*); a manual push counts as a retry and restarts the clock.

**3.3 Shipment cannot be created.** Shipping answers the create-shipment command with `ShipmentCreationFailed` when the order has no delivery address, the tenant has no active carrier, or a technical failure survived 5 attempts. Sales compensates exactly as in 3.2. Shipping never creates a second shipment for an order: a repeated command re-announces the existing one (which also repairs a lost `ShipmentCreated`).

**3.4 Delivery.** Shipping publishes `DeliveryConfirmed` when the carrier confirms. A Shipped order becomes Delivered with the status-history reason "Delivered (carrier confirmation)"; any other status is left alone (already Delivered, cancelled, partly shipped — the other shipments are still on the way). A failed delivery attempt (`ShipmentFailed`) is only recorded on the order card; the order is not cancelled, a person decides.

**3.5 Return.** When the approver completes a return, Sales publishes `SalesOrderReturned` with the amount and the shipped lines. Warehouse puts each product back (movement *Returned*, a new lot, the weighted cost the order's goods left at, from the cost log; the product's current cost when the order has none). Accounting books the movement as Dr Inventory / Cr Cost of goods sold (the credit note Dr Sales returns / Cr Receivable was already posted). Payment refunds `min(refund amount, refundable)` on the order's payment through the gateway; Accounting books the confirmed refund as before.

## 4. Functional requirements
| ID | Requirement | Acceptance |
|---|---|---|
| FR-S1 | A Running saga whose Payment / Stock / Shipment step exceeded its timeout is pushed again (that step's command) and its retry count rises. | UAT-S1 |
| FR-S2 | After the retries are used up the saga is compensated once: refund when paid, order cancelled, saga Failed with "Timed out in step X after N retries". | UAT-S2 |
| FR-S3 | The Approval step is never retried or timed out, not even by hand. | UAT-S3 |
| FR-S4 | `GET orders/{id}/saga` returns the steps (Done / Current / Waiting / Failed), status, retries, timeout, stuck flag, message and whether a push is possible. `POST orders/{id}/saga/retry` pushes the current step now (refused for a finished saga or for approval). | UAT-S4 |
| FR-S5 | Shipping raises `ShipmentCreationFailed(OrderId, Reason)` instead of dropping a command with no address / no carrier or after 5 failed attempts; Sales compensates on it once. | UAT-S5 |
| FR-S6 | Shipping creates at most one non-cancelled shipment per order; a repeated command announces the existing shipment again. | UAT-S6 |
| FR-S7 | `DeliveryConfirmed` turns a Shipped order into Delivered with the reason "Delivered (carrier confirmation)"; other statuses are left alone. | UAT-S7 |
| FR-S8 | `ShipmentFailed` is stored on the saga's message and shown on the order; it does not cancel. | UAT-S8 |
| FR-S9 | `SalesOrderReturned` lists the order's shipped lines. | UAT-S9 |
| FR-S10 | Warehouse restocks every line once per order and product at the order's issue cost, with a lot and a *Returned* movement of positive value; a product it does not track is skipped. | UAT-S9, UAT-S10 |
| FR-S11 | Payment refunds the return once per order, capped at the refundable amount; a refund the gateway declined is tried again on redelivery; no payment / not refundable → nothing. | UAT-S11 |
| FR-S12 | Warehouse and Payment consumers retry a failing message 5 times (republish with a counter) and then drop it with an error log; they no longer requeue for ever. | UAT-S12 |
| FR-S13 | The Warehouse reads the cancelled / shipped events as Sales publishes them (the optional `CorrelationId`), so the reservation of a cancelled order is released and a shipped order's stock is issued. | UAT-S13 |
| FR-S14 | The order page shows the *Order processing* card (§9). | UAT-S14 |

## 5. Business rules
| ID | Rule |
|---|---|
| BR-S1 | Timeouts: Payment 15, Stock 10, Shipment 15 minutes; MaxRetries 3; check every 60 s (`Saga:PaymentPendingMinutes`, `InventoryPendingMinutes`, `ShippingPendingMinutes`, `MaxRetries`, `CheckIntervalSeconds`). The clock is the saga's `LastUpdatedAt` (set at every step change and every push). |
| BR-S2 | Compensation = refund command (when `PaymentId` is set) + `CancelOrderCommand`; the cancellation event releases the reservation. A saga is compensated once (Failed is final). |
| BR-S3 | The recovery job handles each saga in its own tenant scope and DI scope; one failing saga never stops the others. |
| BR-S4 | Retried commands are idempotent at the receiver: Payment per order id, Warehouse per active reservation of the order, Shipping per non-cancelled shipment of the order. |
| BR-S5 | Only a Shipped order becomes Delivered on `DeliveryConfirmed`. |
| BR-S6 | Restock cost = Σ total cost ÷ Σ quantity of the order's cost-consumption rows for the product, rounded to 4 decimals; none → the product's current cost (a warning is logged). The movement reference is `return:<order id>`; the lot is `RET-<8 hex>`. |
| BR-S7 | A return is of the whole order: every line with a shipped quantity comes back with that quantity. |
| BR-S8 | The refund's reason is "Return of order <id>"; an existing non-failed refund with that reason means the return was refunded. |
| BR-S9 | Failure to publish or process never loses the order: a saga that cannot reach a receiver is retried, then compensated, and the log names the order. |

## 6. Use cases
**UC-S1 Follow an order.** *Actor:* order handler. *Main flow:* Orders → open the order → read **Order processing** (§9). *Alternative:* A1 — the card shows "waiting for 'Payment' for 18 min (the system pushes it again after 15 min) — it is taking longer than expected" → press **Retry this step now**. A2 — the card shows a red banner "The order was cancelled automatically; any payment is being refunded" with the reason → no action. *Exceptions:* approval step → no retry button; a finished saga → 400.

**UC-S2 Approve while the system waits.** The card shows *Approval* as the current step with "Waiting for a person to approve the order."; approving moves it on (the saga never times out here).

**UC-S3 Customer receives the parcel.** The carrier confirms delivery in Shipping → within seconds the order shows **Delivered** and the history row "Delivered (carrier confirmation)".

**UC-S4 Return a delivered order.** Request return → complete return with the refund amount → stock returns, the customer is refunded, the journal gets the credit note and the cost reversal. *Exceptions:* a product removed from the warehouse is skipped (logged); no payment (cash / POS) → no gateway refund.

## 7. Data and state
- `OrderFulfillmentSagaState` (existing table, **no schema change**): `CurrentStep`, `Status`, `RetryCount`, `LastUpdatedAt` (clock), `ErrorMessage` (reason / last failed delivery).
- Events: new `ShipmentCreationFailed` (Shipping, outbox → `shipping-topic`); `SalesOrderReturned` gains `Lines[{ProductId, Quantity}]`; Warehouse movement type `Returned = 5` (INT column, no migration).
- States of the card: step `Done / Current / Waiting / Failed`; saga `Running / Completed / Compensating / Failed`.

## 8. Integrations
| From → To | Message | Transport | Effect |
|---|---|---|---|
| Sales → Payment / Warehouse / Shipping | `InitiatePayment`, `ReserveInventory`, `CreateShipment` commands (also re-sent by the job) | RabbitMQ `sales_order_events` | idempotent receivers |
| Shipping → Sales | `ShipmentCreated`, `ShipmentCreationFailed`, `ShipmentFailed`, `DeliveryConfirmed` | Kafka `shipping-topic` | saga steps / compensation / Delivered |
| Sales → Warehouse, Payment, Accounting | `SalesOrderReturned` (+ lines) | RabbitMQ | restock / refund / credit note |
| Warehouse → Accounting | `StockMovementRecorded` (`Returned`, ValueDelta > 0) | RabbitMQ `warehouse_exchange` | Dr Inventory / Cr COGS |
| Payment → Accounting | `PaymentRefunded` (gateway-confirmed) | Kafka `payment-topic` | refund entry (P1-1e) |

## 9. User interface — the *Order processing* card
Where: **Orders → (an order) → order page**, between the *Status Actions* block and the *Line items* table. Shown only for orders that have a saga (older orders show nothing). English / Vietnamese labels in `orders.saga.*`.
- **Header:** "Order processing" / "Tiến trình xử lý đơn", and on the right the saga status: In progress / Completed / Cancelling / Stopped (Đang xử lý / Hoàn tất / Đang hủy / Đã dừng).
- **Step chips**, left to right with arrows: Payment → Stock reserved → Approval → Shipment → Done (Thanh toán → Giữ hàng → Duyệt đơn → Vận đơn → Hoàn tất). Done = green with ✓; Current = blue with ● and a ring (`aria-current="step"`); Waiting = grey ○; Failed = red ✕.
- **Running line:** "Waiting for 'Payment' for 18 min (the system pushes it again after 15 min)." In amber with "— it is taking longer than expected" once past the limit. At Approval: "Waiting for a person to approve the order." Below: "Pushed again 1 of 3 times." once a push happened.
- **Button** *Retry this step now* (Thử lại bước này ngay): only for users with `sales_order:update`, only while Running and in a step that can be retried; shows "Saving…" while it runs; an error text under it when refused.
- **Red banner** (role alert) for Failed / Compensating: "The order was cancelled automatically; any payment is being refunded." plus the reason ("Timed out in step PaymentPending after 2 retries", "Shipment could not be created: there is no active carrier", …).
- **Completed:** "Processing finished on 26 Oct 2026 03:36"; an amber note when a delivery attempt failed ("Delivery failed (attempt 2): customer not at home").
- The card refreshes every 15 s while the saga is Running. The status history table below shows "Delivered (carrier confirmation)" for carrier-confirmed deliveries.
- No other screen changes: restocked goods appear in *Inventory → movements* as movement type 5, the refund in *Payments*, the entries in *Accounting → Journal entries*.

## 10. Permissions
| Action | Permission |
|---|---|
| A customer's own orders (`GET orders/my`, `GET orders/{id}` of their own order, create their own order) | none beyond being signed in (own-resource check, 2026-10-27; another customer's order → 404) — see `docs/business/Identity/CustomerSignup.md` |
| Any order (`GET orders/{id}`) | `sales_order:read` |
| See the card (`GET orders/{id}/saga`) | `sales_order:read` |
| Retry a step (`POST orders/{id}/saga/retry`) | `sales_order:update` |
| Complete a return | `sales_order:approve` (unchanged) |
| Confirm delivery in Shipping | `shipping:update` (unchanged) |

## 11. Reports
No new report. The saga status is on the order page only.

## 12. UAT scenarios (live results on `glcheck`, 2026-10-26)
| ID | Scenario | Result |
|---|---|---|
| UAT-S1 | Order placed, nobody pays, timeouts set to 1 min / 1 retry | after ~1 min `retries 1/1`, step unchanged |
| UAT-S2 | Same, wait for the next limit | saga Failed "Timed out in step PaymentPending after 2 retries" (one was a manual push), order Cancelled, a push on the finished saga → 400 |
| UAT-S3 | Paid order waiting for approval; press retry | 400 "…done by a person"; `canRetry` false |
| UAT-S4 | Read the saga of an order mid-way | steps Payment=Done, Inventory=Current, … as in the card |
| UAT-S5 | No active carrier, order paid and approved | `ShipmentCreationFailed` in the outbox; saga Failed "Shipment could not be created: there is no active carrier"; order Cancelled; payment Refunded; reservation released; stock reserved 0 |
| UAT-S6 | Command for an order that has a shipment | no second shipment (unit test); live: exactly one shipment per order |
| UAT-S7 | Carrier confirms delivery of a Shipped order | order status 5 (Delivered) with "Delivered (carrier confirmation)" |
| UAT-S8 | Failed delivery recorded | unit test: message "Delivery failed (attempt 2): …", status unchanged |
| UAT-S9 | Complete the return of a 2-piece order issued at cost 50 | stock 8 → 10; movement type 5 qty 2 `return:<id>`; lot `RET-…` 2 × 50 |
| UAT-S10 | Accounting follows the movement | Dr 1561 100 / Cr 632 100 (October reopened for the check, closed again) |
| UAT-S11 | Refund 200,000 on completing the return; replay | one refund row "Return of order …", completed; replay → nothing (unit test) |
| UAT-S12 | A poison message | republished 5 times with a log, then dropped (seen live for the Warehouse event before FR-S13) |
| UAT-S13 | Cancel / ship events | reservation released (status 2); shipped stock issued |
| UAT-S14 | The admin build | `tsc -b` clean; the card is described from code and the live API data, not clicked in a browser in this task |

## 13. Assumptions and open issues
- A return is of the whole order and of the shipped quantities; the refund amount is what the approver types (capped by the payment).
- The saga publishes its commands after saving its own state, not through the outbox (at-least-once, receivers idempotent). A crash between the two is repaired by the timeout retry.
- Timeouts are service-wide defaults. Dead-lettered messages are only in the log (no screen).
- Restocking at the FIFO valuation overwrites the product's current cost with the returned cost, like any receipt.
- A shipment for a part of the order (split shipments) is not marked Delivered by one confirmation (only a Shipped order is).
- Foreign-currency payments from the Payment service are still booked unconverted (DEFERRED §4o).

## 14. Traceability
| Task | Date | Requirements | Main tests |
|---|---|---|---|
| P1-5 saga completion | 2026-10-26 | FR-S1 … S14, BR-S1 … S9, UC-S1 … S4, UAT-S1 … S14 | `SagaTimeoutTests`, `DeliveryConfirmedTests`, `CompleteReturnTests` (SO); `SagaShipmentRequestProcessorTests` (SHP); `RestockReturnedOrderTests`, `SalesOrderEventContractTests` (WH); `RefundReturnedOrderTests` (PAY); `StockMovementPostingTests` (ACC) |
