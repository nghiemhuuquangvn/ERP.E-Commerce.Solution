# Shipping — Admin screen: Shipments (detail, re-book, cancel at carrier, split)

Business analysis of the **Shipments** screen as shipped on 2026-11-02 (ROADMAP P2-9, U3 batch 8). Carrier booking, tracking and COD are in [CarrierIntegration.md](CarrierIntegration.md) and [ParcelsAndReturns.md](ParcelsAndReturns.md); the Returns screen is [ReturnsBackOffice.md](ReturnsBackOffice.md). Vietnamese user guide: [docs/user-guides/payments-shipping-sales-logs.md](../../user-guides/payments-shipping-sales-logs.md).

## 1. Purpose and scope
Let a shipping clerk follow every parcel, understand why a carrier was chosen, fix a parcel that was not booked, cancel one that the carrier has not collected, and **split** one parcel into several. Out of scope: creating a shipment by hand, label printing, bulk actions.

## 2. Actors and permissions
Shipping clerk: `shipping:update` (Re-book, Cancel at the carrier, Split). Reading needs only access to the page.

## 3. State model and rules
`Pending → ReadyForPickup → PickedUp → InTransit → OutForDelivery → Delivered | DeliveryFailed → Returned`; `Cancelled`. Status is driven by the carrier webhook / polling, never by this screen.
- **Re-book:** offered when the parcel has no tracking code and is not cancelled; books through the one booking path (shop rules, then cheapest quote); a fee may apply. A refusal is shown as a toast with the carriers' reasons.
- **Cancel at the carrier:** only while Pending / ReadyForPickup; cancels the booking at the carrier, **not the order**; if the carrier refuses, the parcel stays and a review note is kept.
- **Split:** only while Pending / ReadyForPickup and with more than one unit; 2–10 parcels; every item quantity is assigned exactly once; every parcel gets at least one unit; COD per parcel must add up to the order's COD (proposed by units, the last parcel takes the remainder); all parcels are booked or none (the original booking is cancelled); the order is delivered and its COD captured with the **last** parcel.
- **New in this batch:** Re-book, Cancel at the carrier and Split each ask for confirmation stating the consequence; the grid says in words what is wrong ("n unit(s) are not assigned…", "assigned more than once", "every parcel needs at least one unit", "cash on delivery must add up to …") instead of red numbers only; every grid input has a label ("Shirt: parcel 2").

## 4. Requirements with acceptance criteria
| # | Requirement | Acceptance criteria |
|---|---|---|
| SH-1 | A step that books or cancels at a carrier is never one click | The three buttons open a confirmation; no request before it; Esc closes only the top dialog |
| SH-2 | The split grid cannot be confirmed in a wrong state | While any unit is unassigned or over-assigned, a parcel is empty or the COD does not add up, **Split and book** is disabled and the reason is written under the grid |
| SH-3 | Refusals are explained | Re-book / cancel failures are toasts with the server's reason; a split refusal is shown in the dialog |
| SH-4 | Lists are usable | Labelled search, status and "needs attention" filters; no-results + Clear; View button names the tracking code |
| SH-5 | Accessible | Stacked dialogs trap focus, Esc closes the top one, axe finds no violation |

## 5. Use cases
**UC-1 Cancel an unwanted parcel:** View → Cancel at the carrier → confirm → parcel cancelled at the carrier; the order is handled separately.
**UC-2 Fix a failed booking:** View (booking error shown) → Re-book → confirm.
**UC-3 Ship in two boxes:** View → Split into parcels → 2 parcels, move units, adjust COD → Split and book → confirm.

## 6. Data, integrations
Shipment(Carrier, Tracking, Status, Recipient, Address, COD, DeclaredValue, CarrierFee, SelectionNote, ReviewNote, BookingError, Items, ParcelNo/ParcelCount, TrackingEvents). Events `ShipmentCreated`, `TrackingUpdated`, `DeliveryConfirmed` (`OtherParcelsPending`).

## 7. Open issues (DEFERRED R95)
A split cannot be undone; the walk used platform-tenant parcels and never executed Re-book / Cancel / Split (only the confirmations were opened); dates use `dd MMM yyyy` regardless of language.

## 8. UAT scenarios
1. Shipments → filter that matches nothing → "Không có kết quả" + Xóa lọc.
2. View a Pending parcel → **Cancel at the carrier** → text names the carrier → Esc → the detail stays open.
3. **Split into parcels** → the grid starts with everything in parcel 1 and says "Every parcel needs at least one unit" → move units → "Every item is assigned" → **Split and book** → confirmation → Cancel → nothing sent.
4. A parcel without tracking code → **Book with a carrier again** → confirmation → a refusal is shown as a toast.
