# Returns screen in the Admin app (ROADMAP P2-1b, part A)

Branch `feat/p2-1b-returns-webhooks-templates` (2026-10-31, stacked on R43). Migration Shipping 011. Builds on [ParcelsAndReturns.md](ParcelsAndReturns.md) (the way back of a returned order is booked at the carrier) and [CarrierIntegration.md](CarrierIntegration.md).

User guide for staff (Vietnamese): [shipping-returns.md](../../user-guides/shipping-returns.md).

## 1. Purpose and scope
Since P1-11d every return approval books reverse parcels at the carrier, but staff could see them only inside one order's detail. A refused booking (address not served, ward missing) was therefore easy to overlook, and there was no way to book it again or to cancel a pickup at the carrier. This task adds a **Returns** screen under Shipping that lists every return parcel of the shop and lets staff **book again**, mark **goods received** and **cancel the pickup at the carrier**.

Out of scope (§8): carrier fee booking of the reverse parcel (R45), telling Sales that a cancelled return will not come back, partial returns by line (AG2), Viettel Post / J&T reverse orders against their documents.

## 2. Actors
| Actor | Does |
|---|---|
| Shipping clerk (`shipping:view`, `shipping:update`) | Watches the list, books refused returns again, receives goods by hand, cancels a pickup |
| Customer service (`shipping:view`) | Looks a return up by order id or tracking number to answer a customer |
| Carrier (system) | Collects and delivers the reverse parcel; its webhook / polling moves the return to PickedUp / Received (unchanged) |

## 3. Requirements and acceptance criteria
| ID | Requirement | Acceptance criteria |
|---|---|---|
| FR-R1 | One list of the shop's return parcels, newest first, paged. | `GET shipments/returns` answers `{items, totalCount, pageNumber, pageSize}`; page size capped at 100; a page past the end is empty. |
| FR-R2 | Filter by status and search. | `status` = Requested / PickedUp / Received / Cancelled (any other value is HTTP 400); `search` matches the tracking number, the carrier's order code, the carrier name, or an order / shipment / return id. |
| FR-R3 | Show what needs a person. | `needsAttention` = a booking the carrier refused (no tracking number yet, still Requested); the row shows the carrier's reason in red. |
| FR-R4 | Book a refused return again. | `POST returns/{id}/rebook` books the order's missing reverse parcels through the one booking path; answers the return with its tracking number, or still with the refusal. A return that already has a tracking number is left alone (a second click books nothing); a Received / Cancelled return answers HTTP 400. |
| FR-R5 | Cancel the pickup at the carrier first. | `POST returns/{id}/cancel`: a booked, not yet collected return is cancelled at the carrier, then closed here. If the carrier refuses, HTTP 400 with its reason and the return stays open. A return already PickedUp / Received answers 400 ("the carrier no longer cancels this pickup"). A return that was never booked is closed here only. |
| FR-R6 | Receive the goods by hand. | The existing `shipments/{id}/returns/{returnId}/received` action, offered on the screen; the order's refund is no longer held (P1-11d). |
| FR-R7 | Permissions. | List needs `shipping:view`; book again and cancel need `shipping:update`; the buttons are hidden without it. |
| FR-R8 | Menu and languages. | Sidebar entry *Returns* next to *Shipments*; every label in English and Vietnamese. |

## 4. Business rules
1. Booking is always the same code path as the first approval (`ReturnBookingService`): the shop's carrier rules, quotes and providers apply; nothing new is asked of a carrier.
2. A cancel is "carrier first": the shop's books never say a pickup is cancelled while the carrier will still send a courier.
3. A return can be cancelled only while it is Requested (nobody collected the goods).
4. Cancelling a return does not change the order: the order stays *return requested* and its refund still waits for the goods unless staff receive them by hand or the order is handled otherwise (R59).

## 5. Use cases
**UC-R1 Refused pickup, booked again (main).** The customer's return is approved; the carrier refuses ("Ward is required"). The clerk opens *Returns*, ticks *Booking refused*, sees the reason, fixes the customer's address (order), clicks *Book again*; the row gets a tracking number and leaves the filter.
**UC-R2 Cancel a pickup (alternative).** The customer changes their mind: the clerk clicks *Cancel*, confirms; the carrier cancels, the return becomes Cancelled.
**UC-R3 Carrier refuses to cancel (exception).** The courier already left: the carrier answers "already picked up"; the screen shows that text, the return stays Requested; staff wait for the parcel or receive it by hand.
**UC-R4 Goods arrive without a carrier message (alternative).** The clerk clicks *Goods received*; the return is Received and the order's refund can be completed.

## 6. Data and integrations
No new table. `ReturnShipmentDto` gains `CarrierId` and `LastCarrierSyncAt`. Index `(TenantId, Status, CreatedAt DESC)` on `ReturnShipments` (migration 011). New routes: `GET api/v1/shipments/returns`, `POST api/v1/shipments/returns/{id}/rebook`, `POST api/v1/shipments/returns/{id}/cancel` (attribute authorization, not on the in-action allowlist). Gateway prefix `/shp/api/v1`.

## 7. UAT (all passed live on tenant `glcheck`, fake GHTK; data removed afterwards)
| # | Scenario | Result |
|---|---|---|
| S1 | Two COD orders delivered; carrier set to refuse; both returns approved | both returns Requested without tracking, `bookingError` = the carrier's text |
| S2 | List with *needs attention* and a search by order id | 1 row with the reason, no tracking number |
| S3 | Carrier back to normal; *Book again* | 200, tracking `GHTK3`, no error; a second click 200 and the carrier got exactly 1 order |
| S4 | *Cancel* the booked return | 200, status Cancelled, the carrier received exactly 1 cancel |
| S5 | *Book again* on the cancelled return | 400 |
| S6 | Filter by status Cancelled; status "Floating" | 1 row; 400 |
| S7 | Goods received by hand on the other order's refused return | 200, list shows Received |
Not done live: the carrier refusing a cancel (unit test), a browser walk-through of the screen.

## 8. Open issues
R59 (a cancelled return does not tell Sales), R60 (no bulk actions, no export), R45 (reverse parcel fee not booked), R46 (Storefront shows no return tracking).
