# Several parcels per order and returns through the carrier (ROADMAP P1-11d)

Branch `feat/p1-11d-returns-and-parcels` (2026-10-28). Migrations: Shipping 010, Sales 017. Builds on [CarrierIntegration.md](CarrierIntegration.md) (booking, tracking) and [ShippingFee.md](ShippingFee.md) (the refund of the delivery fee).

## 1. Purpose and scope
Before this task an order travelled in exactly one parcel and a returned order's goods came back "outside the system": the approver clicked *Complete return*, the refund went out and nobody knew whether the goods were home.
Now (a) a person can **split an order's parcel into several parcels**, each booked at a carrier on its own, and the order is *Delivered* with its **last** parcel; (b) approving a **return books the way back at the carrier**, and the refund is held until the **warehouse has received the goods**.

Out of scope (kept in DEFERRED-ISSUES): automatic splitting by weight or warehouse, splitting after the carrier collected the parcel, a partial return by line (AG2), tracking / parcels on the storefront, Viettel Post and J&T reverse orders against their real documents (P1-11e), booking the carrier's fee for the way back.

## 2. Actors
| Actor | Does |
|---|---|
| Shop staff (`shipping:update`) | splits a parcel, marks a reverse parcel received by hand |
| Approver (`sales_order:approve`) | approves a return (books the pickup), completes the return (refund) |
| Carrier | collects / delivers parcels, reports status (webhook, polling) |
| Warehouse | receives the returned goods (the carrier's "delivered" on the reverse parcel is the signal) |
| Customer | receives the parcels; hands the goods back to the carrier |

## 3. Requirements and acceptance criteria
### 3.1 Several parcels
| ID | Requirement | Acceptance criteria |
|---|---|---|
| FR-30 | A parcel the carrier has not collected (Pending / ReadyForPickup) can be split into 2–10 parcels. | Admin → Shipments → parcel → *Split into parcels*; a collected parcel is refused ("cannot be split"). |
| FR-31 | Every item goes to exactly one parcel. | 3 tees + 1 hat split as [2 tees] [1 tee + 1 hat] works; a part that loses or invents an item, an empty part or fewer than two parts is refused (400). |
| FR-32 | The cash on delivery of the parcels adds up to the original. | Proposed by units (200 000 → 100 000 + 100 000, the last parcel takes the remainder); typed amounts must add up exactly, all or none typed. |
| FR-33 | Each new parcel is booked at a carrier on its own; the original booking is cancelled; **all or nothing**. | Live: carrier received `…-P1` (pick_money 100 000) and `…-P2` (100 000) and one cancel of the original; if a carrier refuses a new parcel, or refuses to cancel the original, the new bookings are undone and nothing is saved. |
| FR-34 | A parcel knows its number and the order's parcel count. | `Shipments.ParcelNo / ParcelCount`; "Parcel 2 of 2" on the shipment card; reference to the carrier is `SO-<order>` for an unsplit order and `SO-<order>-P<n>` otherwise (carriers dedupe on it). |
| FR-35 | The order is *Delivered* only when **all** parcels are delivered. | `DeliveryConfirmed` carries `OtherParcelsPending`; Sales ignores it while > 0. Live: after parcel 1 the order stays Shipped, after parcel 2 it is Delivered. |
| FR-36 | The cash on delivery is booked **parcel by parcel** (changed by ROADMAP R43; P1-11d first booked it once with the last parcel). | Each delivered parcel captures its own `CodAmount`: payment `PartiallyCaptured` after parcel 1, `Captured` after the last; see [OnlinePayments.md](../Payment/OnlinePayments.md) §13. The *order* is still Delivered with the last parcel (FR-35). |

### 3.2 Returns through the carrier
| ID | Requirement | Acceptance criteria |
|---|---|---|
| FR-37 | *Approve return* asks Shipping to book the goods' way back. | `POST orders/{id}/return/approve` → `internal/v1/returns`; one reverse order per delivered parcel, customer → the shop's pickup address, **no cash to collect**, reference `RT-<order>-P<n>`. Live: 2 reverse orders, `pick_money 0`, from "2 Le Loi" to "1 Nguyen Hue". |
| FR-38 | The reverse booking is the forward booking turned round — the shop's carrier rules, quotes and every provider work as they are. | `ReturnBookingService` builds an unsaved shipment to the shop's address and books it with the two addresses swapped. |
| FR-39 | Approving again is safe. | A second approve books nothing new for a parcel that has a tracking code; a refused one is retried (live: carrier orders stayed 2). |
| FR-40 | A refusal does not block the return. | Carrier refusal → the reason is kept on the return parcel and on the order (`ReturnPickupNote`); the return goes on by hand as before. Shipping **unreachable** → the approval is refused (nothing changed), try again. |
| FR-41 | The refund waits for the goods when a pickup is booked. | `CompleteReturn` is refused (409 "still on their way back") while `ReturnPickupBooked` and `ReturnGoodsReceivedAt` empty. A return with no booked pickup completes as before. |
| FR-42 | The carrier tells the goods are home. | The reverse parcel goes through the same webhook / polling path as forward parcels; "delivered" = `Received` → `ReturnReceivedEvent(OtherReturnsPending)`; Sales sets `ReturnGoodsReceivedAt` when no other reverse parcel is pending. Live: nothing after 1 of 2, set after 2 of 2, then *Complete* = 200. |
| FR-43 | A person can receive the goods by hand. | Order card → *Goods received* per reverse parcel (existing `…/returns/{id}/received`). |

## 4. Business rules
1. Only a parcel in Pending / ReadyForPickup can be split; a split never loses an item or cash.
2. Split numbering continues after live parcels of the order; the cancelled original keeps its number but the unique index (`TenantId, OrderId, ParcelNo`) covers live parcels only.
3. Cancelling the original is done at the carrier first; a carrier that refuses stops the split.
4. An order is Delivered / its COD captured with the last parcel; a parcel returned to sender or stuck keeps the order Shipped (visible, never silently delivered).
5. A reverse parcel is never COD; its carrier fee is paid by the shop and is not booked (see risks).
6. A return is refunded after the goods are home **only when** a carrier pickup was booked; orders delivered outside the system keep the manual flow.

## 5. Use cases
**UC-7 Split (main).** Staff opens a Pending parcel with 4 items → *Split into parcels* → chooses 2 parcels, moves 1 tee + the hat to parcel 2, adjusts the cash → *Split and book*. Alternative: a carrier refuses → message with the reason, nothing changed. Exception: the carrier already collected the original → refused.
**UC-8 Deliver a split order.** Carrier notifies parcel 1 delivered → order stays Shipped; parcel 2 delivered → order Delivered, cash captured.
**UC-9 Return through the carrier (main).** Customer asks for a return → approver *Approve return (book pickup)* → panel lists reverse parcels with tracking codes → carrier delivers them to the shop → *Complete return* enabled → refund + restock as before. Alternative: booking refused → panel shows the reason and *Book pickup again*; or *Goods received* by hand.
**UC-10 Return of an order delivered by hand.** No parcel in Shipping → approve answers "no delivered parcel", the return goes on by hand.

## 6. Data and integrations
- Shipping 010: `Shipments.ParcelNo/ParcelCount` + `UX_Shipments_Order_Parcel`; `ReturnShipments.CarrierId/CarrierName/CarrierOrderCode/CarrierFee/BookingError/LastCarrierSyncAt`. Existing orders with two live shipments (demo data) were renumbered so the index could be built.
- Sales 017: `SalesOrders.ReturnApprovedAt/ReturnPickupBooked/ReturnGoodsReceivedAt/ReturnPickupNote`.
- Events (additive fields, old consumers unaffected): `DeliveryConfirmed(ParcelNo, ParcelCount, CodAmount, OtherParcelsPending)`; `ReturnReceived(OtherReturnsPending)` now consumed by Sales.
- APIs: `POST api/v1/shipments/{id}/split` (`shipping:update`), `GET api/v1/shipments/order-returns/{orderId}` (`shipping:view`), `POST internal/v1/returns` (`salesorder` service), `POST api/v1/orders/{id}/return/approve` (`sales_order:approve`).
- The Sales order read is no longer cached (a 5-minute copy was never evicted by event-driven changes and its key had no tenant).

## 7. UAT (all passed live on tenant `glcheck`, fake GHTK)
| # | Scenario | Result |
|---|---|---|
| S12 | COD order 3 tees + 1 hat (200 000) | 1 parcel, COD 200 000 |
| S13 | Split [2 tees] [1 tee + 1 hat] | carrier got `-P1` 100 000 and `-P2` 100 000, 1 cancel; 2 live + 1 cancelled; a part that loses an item → 400 |
| S14 | Deliver parcel 1, then parcel 2 | Shipped / payment Pending; then Delivered / payment Captured |
| S15 | Request return, approve | 2 reverse orders (`pick_money 0`, customer → shop), note "2 of 2 booked"; approve again books nothing |
| S16 | Complete before / after 1 of 2 reverse parcels delivered | 409, 409 |
| S17 | After 2 of 2 delivered, complete | `ReturnGoodsReceivedAt` set; 200, order Returned |
Not run live: a carrier refusing a new parcel (covered by unit tests), Viettel Post / GHN reverse orders, the admin screens in a browser.

## 8. Open issues
See DEFERRED-ISSUES: AN5 / AN6 / AG5 closed; R43–R47 new.
