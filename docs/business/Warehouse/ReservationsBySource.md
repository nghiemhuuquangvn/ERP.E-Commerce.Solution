# Warehouse - reservations by source, partial consume, lot suggestion (W4 of the Warehouse upgrade)

Added 2026-11-10. Decision record: `docs/architecture/adr-warehouse-upgrade.md`. Follows `ProductionMovements.md` (W3). The internal write API for Manufacturing with its idempotency key and the lot / serial checks come in W5.

## 1. Purpose and actors
Until now only a sales order could reserve stock, always over the item total and over every warehouse, and a shipment took the whole reservation at once. A production order needs to hold components **in the warehouse it will draw from**, take them out **in pieces** as the shop floor issues them, and be told **which lots to use** (earliest expiry first).

| Actor | What changes for them |
|---|---|
| Production planner (via Manufacturing, from W5) | Reserves the components of a production order in a warehouse; sees what is held, consumed and left. |
| Storekeeper | Issues material against the reservation, partly and several times; is told which lots to pick. |
| Warehouse manager | Sees reservations by source; knows production reservations do not expire. |
| Sales / POS | Nothing changes in how they reserve and ship. A manual sale can no longer take stock that is reserved. |

## 2. Requirements and acceptance criteria
| ID | Requirement | Acceptance |
|---|---|---|
| RS-1 | A reservation belongs to a source: SalesOrder (default) or ProductionOrder, with the source id and a reference (e.g. the order number). | Reserve for a production order: the reservation reads source ProductionOrder and the reference. |
| RS-2 | A production reservation is held in one named warehouse; it uses only that warehouse's usable lots (first expiry first), then its lot-less stock. | 8 kg reserved in A: lot LB 4 then LA 4, the lot in warehouse B holds nothing. |
| RS-3 | If the warehouse cannot cover a line, nothing is reserved (all lines are checked first), even when another warehouse has the stock. | 20 asked in A (18 in total, 13 in A): 400 and the reserved figures are unchanged. |
| RS-4 | One open reservation per source: asking again returns it. | Second identical call answers `replayed: true` with the same reservation id and stock is held once. |
| RS-5 | Stock can be taken from a reservation in parts; the item total, the lot, the balance and the reservation move together; a reservation is Fulfilled when every line is fully taken. | Issue 3 of 8: consumed 3, remaining 5, still Reserved; then 5 more: Fulfilled. |
| RS-6 | More than what is left cannot be taken. | Issue 6 when 5 are left: 400, nothing changes. |
| RS-7 | Releasing a partly consumed reservation frees only what is left. | Reserve 3, issue 1, release: 2 freed, no reservation remains. |
| RS-8 | The lot suggestion follows the same first-expiry-first rule as the reservation: Available lots only, not expired, not fully held, optionally one warehouse. | `suggest-lots` for 8 in A lists LB 4, LA 4; for 7 anywhere it starts with the lot of B that expires first; shortfall and lot-less stock are shown. |
| RS-9 | A manual sale, a production issue without a reservation and a vendor return take only **available** stock and never lower anybody's reservation. | 8 of 10 reserved: a sale of 5 is refused, a sale of 2 is accepted and the reservation stays 8. |
| RS-10 | A production issue can name the reservation (and the line) it is taken from; the movement carries the value of the lots actually taken. | Issue 3 against a reservation at lot cost 4: value -12, `reservationId` consumed. |

## 3. Business rules
| ID | Rule |
|---|---|
| RS-R1 | Production reservations never expire (`HoldUntil` is the end of time); Manufacturing must release or consume them. Sales reservations keep their hold time and the expiry job. |
| RS-R2 | Availability of a production line = usable lots of the warehouse not held + lot-less stock of the warehouse not held / locked; the item-level check (`total - reserved - locked`) applies as well. Sales reservations still check the item total only. |
| RS-R3 | A line may pin one lot; the lot must be Available, not expired, in that warehouse and free for the quantity, otherwise the call is refused. |
| RS-R4 | A lot that is on hold, blocked, expired or fully held is never suggested or reserved. (Expired lots were allocated to sales before W4; none existed on the live data.) |
| RS-R5 | A partial consume takes from the named lot, else from the recorded lot split in order, then from the lot-less stock of the line's warehouse. A shipment of a sales reservation takes the whole remaining quantity, as before. |
| RS-R6 | `Inventory.SellStock` (manual sale, production issue without reservation) needs `available >= quantity`; shipment and consume use `ConsumeReserved`, which lowers the total and the reservation together. |
| RS-R7 | One reference per document line stays the idempotency key of the issue (W3): the same `DocNo#line` twice is one movement. |
| RS-R8 | Production reservations raise no integration event (nobody consumes it); the movement and stock-changed events of W3 still go out. |

## 4. Use cases
**UC-1 Reserve the components of a production order (main).** Manufacturing reserves 8 kg of paint for `MO-2611-00001` in the raw-material warehouse. *Alternative:* a line pins a lot. *Exception:* the warehouse holds less - the whole call is refused with the shortfall and nothing is held.
**UC-2 Ask which lots to pick.** `GET inventory/{id}/suggest-lots?quantity=8&warehouseId=...` lists lots and quantities, the shortfall and the lot-less stock that could cover it.
**UC-3 Issue in pieces.** The first shift issues 3 against the reservation, the second shift 5; the order is Fulfilled with the last piece. *Exception:* asking for more than is left is refused.
**UC-4 Change of plan.** The order is cancelled after a partial issue: releasing the reservation frees only the unissued part.
**UC-5 Sell free stock.** A manual sale can only use what nobody has reserved.

## 5. Data model
`Reservations` + `SourceType` (0 SalesOrder, 1 ProductionOrder), `SourceReference`; `Reservation_ReservedItems` + `SourceLineId`, `WarehouseId`, `ConsumedQuantity`; the lot split (`LotAllocationsJson`) records `Consumed` per lot. Index `IX_Reservations_Source (TenantId, SourceType, OrderId)`. SQL `src/Database/Warehouse/013_ReservationSource.sql` (idempotent; changes no data). The mapping compares the lot split by content so an in-place change is saved.

## 6. Integrations
- API (existing permissions): `POST inventory/reservations/source` (`inventory:manage`), `GET inventory/reservations/source/{type}/{sourceId}` and `GET inventory/{id}/suggest-lots` (`inventory:view`), release by `POST inventory/release`, issue by `POST movements` with `reservationId` / `sourceLineId`. The internal `internal/v1/stock/*` wrapper arrives in W5.
- Sales reserve / ship / release use the same ledger operations (earliest expiry over all warehouses, whole-quantity consume), tested and verified live.
- Accounting, Reporting, Shipping: unchanged.

## 7. Permissions
No new permission: `inventory:manage` to reserve, `inventory:view` to read and to suggest.

## 8. Assumptions and open issues
- The reserved figure of older stock: the seed data of the live database carries about a thousand `ReservedQuantity` values no active reservation explains. They were **not** repaired (the repair would erase demo reservations); only the cause was fixed, so no new drift appears. The reconciliation keeps listing them as *Reserved*.
- A forgotten production reservation holds stock for ever; Manufacturing must release it.
- Sales reservations still check availability on the item total, so a sale can be reserved in a warehouse that does not hold it.
- Behaviour changes: manual sales and vendor returns need available stock; expired lots are no longer allocated to sales.
- Concurrent writers get HTTP 409 and retry (W2 rule); a busy item may conflict more.

## 9. UAT
1. Receive lot LA (6, expires 2030) and LB (4, expires 2028) in warehouse A and lot LC (5) in warehouse B. Ask `suggest-lots` for 8 in A: LB 4, LA 4.
2. Reserve 8 for a production order in A; the lot of B holds nothing; ask again: same reservation.
3. Reserve 20 for another order in A: refused, nothing held.
4. Issue 3 with the reservation: the reservation shows consumed 3 / remaining 5; issue 6: refused; issue 5: Fulfilled.
5. Reserve 3 for a second order, issue 1, release: nothing stays reserved.
6. Reserve 8 of 10 lot-less units; sell 5 manually: refused; sell 2: accepted, the reservation stays 8.
7. `inventory/reconciliation` lists no line for these items.
