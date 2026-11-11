# Warehouse — Business Analysis (stock movements, costing and the link to the ledger)

| | |
|---|---|
| **Domain** | Warehouse (WareHouseService, port 5004, code WH) |
| **Covers** | ROADMAP P1-1b (stock receipt / issue / adjustment with a cost, the event Accounting posts from), plus the stock fixes found on the way. The wider module design (locations, lots, serials, transfers) is in [module-08-inventory.md](../Inventory/module-08-inventory.md). |
| **Status** | Implemented and verified on the local Docker stack, 2026-10-24 (unit tests WH 13, PO 9). Written afterwards, 2026-10-25, to the same standard as [BA-Accounting.md](../Accounting/BA-Accounting.md). |
| **Audience** | Product owner, warehouse manager, chief accountant, QA, developers |
| **Companion documents** | [BA-Accounting.md](../Accounting/BA-Accounting.md) (what Accounting does with the event: FR-05 … FR-07, UAT-05, 06), [AutoPosting.md](../Accounting/AutoPosting.md), [event-catalog](../../architecture/event-catalog.md) |

---

## 1. Business context and objectives

### 1.1 Problem
Warehouse knew how many units were on hand but the value of that stock never reached the books. Before P1-1b:
* the `StockMovementRecorded` event carried a quantity with an inconsistent sign and **no cost**, so Accounting could not tell a receipt from a loss or value either;
* a purchase-order receipt was stocked at **cost 0** (the PO event carried no price);
* a **second partial receipt** of the same product on the same PO was dropped as a "redelivery" (the movement was keyed on the PO, not on the receipt);
* the first receipt of a product that had no inventory row failed with "belongs to a different tenant", which also blocked PO-driven receipts.

### 1.2 Objectives
| # | Objective | How we know it is met |
|---|---|---|
| O1 | Every movement that changes the **value** of stock tells the ledger by how much, once. | WH-FR-01 … 03; UAT-WH-01 … 05 |
| O2 | A receipt from a purchase order is valued at the PO line price, partial receipts included. | WH-FR-04; UAT-WH-02 |
| O3 | A first-time receipt works for a product with no stock row. | WH-FR-05; UAT-WH-06 |
| O4 | Unknown cost never creates a wrong entry: it creates none. | WH-BR-05 |

### 1.3 Scope
**In scope:** the movement types and how each changes quantity and value; the cost the movement is valued at; purchase-order and manual receipts; adjustments and stocktakes; the event published to Accounting.
**Out of scope:** back-posting of stock that existed before P1-1b (an opening entry is booked by the accountant); restock and cost reversal of **returned** goods (ROADMAP P1-5); landed cost and standard cost; FX; the physical processes of locations, lots, serials and transfers (module-08).

---

## 2. Stakeholders and actors

| Actor | Who | What they do |
|---|---|---|
| Warehouse manager | Staff with `inventory:manage`, `inventory:warehouse:manage` | Receives goods, maintains warehouses and locations, reads stock and valuation. |
| Stock controller | `inventory:adjustment:create`, `inventory:stocktake:approve` | Proposes adjustments, runs and approves stocktakes. |
| Approver | `inventory:adjustment:approve` | Approves or rejects a pending adjustment (only an approved one moves stock). |
| Finance | `inventory:valuation:close`, Accounting roles | Closes valuation periods; reads the ledger entries the movements produce. |
| Purchasing (system) | PurchaseOrderService | Publishes the received lines (product, quantity, **unit price**, goods receipt id). |
| Sales / POS (system) | SaleOrderService, POSService | Reserve and release stock; POS sales and voids move stock directly. |
| Accounting (system) | AccountingService | Consumes `StockMovementRecorded` and posts it. |

---

## 3. Glossary

| Term | Meaning |
|---|---|
| Movement | One recorded change of stock: type (Received, Sold, Adjusted, Damaged, Transferred), product, quantity, reference, time. |
| Unit cost | The cost per unit the movement is valued at (receipt: what was paid; issue / loss / gain: the current cost of the stock). 0 = unknown. |
| Value delta | The **signed change of stock value** caused by the movement: positive for a receipt or a gain, negative for an issue or a loss, 0 when value does not change or the cost is unknown. Accounting posts from this and the movement type only. |
| Valuation method | FIFO, LIFO or Average, a setting of each inventory item; only Average recomputes the item's cost on a receipt. |
| Reservation | Stock set aside for an order; becomes an issue when the order ships (Fulfilled), or is Released / Expired. |
| GRNI | Goods received, not invoiced: the liability Accounting credits on a receipt and clears when the supplier bill arrives. |

---

## 4. End-to-end business processes

### 4.1 Procure to stock
1. A purchase order is approved; goods arrive and the purchasing officer records the **goods receipt** (one or more partial receipts, each with its own id).
2. Purchasing publishes the received lines with their **PO unit price**; Warehouse receives each line: quantity up, item cost updated (Average: weighted; otherwise the receipt price), a `Received` movement is written with `UnitCost` and `ValueDelta = quantity × unit cost`.
3. Accounting books *Dr Inventory / Cr GRNI*; when the supplier bill is approved it books *Dr GRNI / Cr Payables* (BA-Accounting 4.2).
A manual receipt (`POST inventory/receive`) follows the same path with the cost typed in.

### 4.2 Stock to customer
Order created → stock **reserved** (available = on hand − reserved − locked). Order shipped → the reservation is **fulfilled**: quantity falls, a `Sold` movement is written at the item's cost, the consumption of each lot is logged. Accounting books *Dr Cost of goods sold / Cr Inventory*.
Reservation released (order cancelled) or expired → nothing is booked: the stock never left.

### 4.3 POS sale and void
A POS sale writes a `Sold` movement per line at the item's cost; a void writes the reverse (a gain at cost). The revenue side is the POS flow (BA-POS 4.1).

### 4.4 Adjustments and stocktakes
* **Adjustment:** proposed with a reason and a signed quantity → Pending → **approved** (stock changes, an `Adjusted` / `Damaged` movement is written at the current cost) or rejected (nothing changes).
* **Stocktake:** counts entered → completed (PendingApproval) → **approved**: each difference becomes a movement at the current cost (gain or loss).
Accounting books a loss as *Dr Inventory adjustment / Cr Inventory* and a gain the other way round.

---

## 5. Functional requirements

Priority: M = must, S = should. Source: ROADMAP item.

| ID | Requirement | Pri | Source |
|---|---|---|---|
| WH-FR-01 | Every movement event carries the **unit cost** and a **signed value delta**; the quantity's sign is not relied upon. | M | P1-1b |
| WH-FR-02 | Receipts, shipments (cost of goods sold), POS sales and voids, approved adjustments and stocktake differences each produce a value delta. | M | P1-1b |
| WH-FR-03 | Transfers between warehouses and movements with an unknown cost (0) produce a value delta of 0, so no entry is posted for them. | M | P1-1b |
| WH-FR-04 | A purchase-order receipt is valued at the PO line price; each partial receipt of the same product on the same PO is a separate movement identified by its goods receipt. | M | P1-1b |
| WH-FR-05 | Receiving a product that has no inventory row creates it (no tenant error) so a PO receipt can proceed. | M | P1-1b |
| WH-FR-06 | The event is published through the outbox in the same transaction as the stock change, stamped with the tenant. | M | P1-1b |
| WH-FR-07 | Under the Average method a receipt recomputes the item's cost as the weighted average; under FIFO / LIFO the cost is the latest receipt price. | S | existing |

### 5.1 Acceptance criteria
| ID | Acceptance criteria | UAT |
|---|---|---|
| WH-FR-01, 02 | Given a manual receipt of 10 units at 40, then the event has `UnitCost` 40 and `ValueDelta` +400; given 3 units sold at cost 50, `ValueDelta` −150; given 2 damaged at cost 50, −100; given +4 adjusted at cost 50, +200. | UAT-WH-01, 03 |
| WH-FR-03 | Given a transfer, or a receipt typed with cost 0, then `ValueDelta` is 0 and Accounting posts nothing. | UAT-WH-05 |
| WH-FR-04 | Given a PO line of 20 units at 50 received as 12 then 8, then two movements (cost 50) with `ValueDelta` 600 and 400 exist, and both are posted. | UAT-WH-02 |
| WH-FR-05 | Given a product never stocked, when it is received for the first time, then the call succeeds and the inventory row exists. | UAT-WH-06 |
| WH-FR-06 | Given a receipt, then the movement, the stock change and the outbox row commit together; the event JSON has `TenantId`. | UAT-WH-04 |
| WH-FR-07 | Given Average, stock 10 at 40 and a receipt of 10 at 60, then the cost becomes 50. | unit test |

---

## 6. Business rules

| ID | Rule |
|---|---|
| WH-BR-01 | **Value is signed, not the quantity.** Accounting uses `ValueDelta` and the movement type. Positive = stock value up (receipt, gain), negative = down (issue, loss). |
| WH-BR-02 | `ValueDelta = round(quantity × unit cost, 2)` with the sign above. A receipt is valued at the price paid; an issue, a loss or a gain at the item's current cost. |
| WH-BR-03 | A reservation changes **no value** — only the shipment (fulfilment) does. |
| WH-BR-04 | A stock adjustment moves stock only when **approved**; a rejected one changes nothing and publishes nothing. A stocktake moves stock only when approved. |
| WH-BR-05 | **Unknown cost (0) creates no value change** (`ValueDelta` 0). The accountant books an opening or correcting entry by hand; nothing is guessed. |
| WH-BR-06 | A movement is identified by its own id (a goods receipt for PO receipts), so redelivery of an event cannot duplicate it and a second partial receipt is never mistaken for one. |
| WH-BR-07 | Available quantity = on hand − reserved − locked; a reservation above it is refused. |
| WH-BR-08 | Every record and event belongs to one tenant; the event carries it and consumers drop events without one. |
| WH-BR-09 | Stock that existed before P1-1b is **not back-posted**: ledger inventory starts below the Warehouse valuation until an opening entry is booked. |

---

## 7. Use cases

### UC-WH-01 Receive goods against a purchase order
*Actor:* purchasing officer (Purchasing) → Warehouse (system). *Pre:* PO approved, supplier prices on the lines.
*Main flow:* record the goods receipt (quantity per line) → Warehouse updates stock and cost → writes a `Received` movement at the PO price → publishes the event → Accounting books *Dr Inventory / Cr GRNI*.
*Alternative:* partial receipt — repeat for the rest; each receipt is its own movement. *Exception:* no price on the line → cost 0 → no entry (WH-BR-05); event without tenant → dropped by consumers.

### UC-WH-02 Receive stock by hand
*Actor:* warehouse manager (`inventory:manage`). *Main flow:* `POST inventory/receive` with product, quantity, unit cost, reference → stock and cost update → movement and event as above. *Exception:* new product — the inventory row is created; negative quantity or cost → refused.

### UC-WH-03 Ship an order
*Actor:* sales / shipping (system). *Main flow:* shipment event → reservation fulfilled → quantity down, lot consumption logged → `Sold` movement at cost → Accounting books *Dr COGS / Cr Inventory*. *Alternative:* order cancelled before shipping → reservation released, nothing booked.

### UC-WH-04 Adjust stock
*Actor:* stock controller creates (`inventory:adjustment:create`), approver decides (`inventory:adjustment:approve`). *Main flow:* create (reason, signed quantity) → approve → stock changes → movement at current cost → Accounting books a gain or loss. *Alternative:* reject → nothing. *Exception:* approving a non-pending adjustment → refused.

### UC-WH-05 Count stock
*Actor:* stock controller; approver (`inventory:stocktake:approve`). *Main flow:* open stocktake → record counts → complete → approve → differences become movements (gain or loss) → Accounting books them. *Alternative:* cancel → nothing.

---

## 8. Data requirements

| Entity | Key attributes | Notes |
|---|---|---|
| Inventory | product, total / reserved / locked quantity, cost, valuation method, last movement | One row per product and tenant. |
| StockMovement | movement id, product, quantity, type, reference, reason, source order, recorded at, **unit cost**, **value delta** | The value fields travel in the event. |
| StockAdjustment | product, quantity delta, reason, status (Pending, Approved, Rejected) | |
| Stocktake | number, counts, status (InProgress, PendingApproval, Approved, Cancelled) | |
| Reservation | items per lot, status (Reserved, Fulfilled, Released, Expired) | |
| CostConsumptionLog | per-lot cost taken at shipment | Audit trail of cost of sales. |

**State models:** Reservation Reserved → Fulfilled | Released | Expired. Adjustment Pending → Approved | Rejected. Stocktake InProgress → PendingApproval → Approved | Cancelled.

---

## 9. Integrations

| Direction | Event | Transport | Content / effect |
|---|---|---|---|
| Purchasing → Warehouse | Purchase order received (domain event → integration) | RabbitMQ | Lines with product, quantity, **unit price**, currency, goods receipt id |
| Sales → Warehouse | SalesOrderCreated / Cancelled / Shipped | RabbitMQ | Reserve, release, fulfil |
| POS → Warehouse | POS sale / void stock record | internal call | `Sold` movements and reversals |
| Warehouse → Accounting | `StockMovementRecordedEvent` | RabbitMQ `warehouse_exchange` | Movement type, quantity, **UnitCost, ValueDelta**, reference, tenant |

Reliability: outbox with the stock change; consumers retry up to 5 times, then log and drop. Plain System.Text.Json (not the Newtonsoft `$type` format).

---

## 10. Reports and queries

Stock by product and location, low stock, movement history, valuation per product and per period (snapshots), cost consumption per product, lots expiring. The ledger side (inventory account, GRNI, COGS) is in the Accounting reports.

---

## 11. Security, audit and non-functional requirements

| Area | Requirement |
|---|---|
| Permissions | `inventory:view`, `inventory:manage`, `inventory:adjustment:create|approve`, `inventory:stocktake:approve`, `inventory:valuation:close`, `inventory:warehouse:manage`, `inventory:barcode:view`; settings `settings:inventory:*`. |
| Tenant isolation | Every aggregate is tenant-owned; the first receipt of a product no longer fails on the tenant guard (a just-added row is not "updated"). |
| Audit | Every change is a movement with reference and reason; approvals are separate steps. |
| Consistency | Stock change, movement and outbox row in one transaction. |
| Money | The cost and value are in the tenant's currency (VND); no conversion. |

---

## 12. UAT scenarios

Verified on the local Docker stack on 2026-10-24 (SYSTEM tenant) unless marked unit test.

| # | Given / When / Then | Requirement | Result |
|---|---|---|---|
| UAT-WH-01 | A manual receipt of 10 × 40 books *Dr Inventory / Cr GRNI 400*. | WH-FR-01 | Passed |
| UAT-WH-02 | A PO line 20 × 50 received as 12 then 8: both stocked at cost 50; entries 600 and 400. | WH-FR-04 | Passed |
| UAT-WH-03 | Sold 3 → *Dr COGS / Cr Inventory 150*; damaged 2 → loss 100; adjusted +4 → gain 200. | WH-FR-02 | Passed |
| UAT-WH-04 | The event is written to the outbox with the stock change and carries the tenant. | WH-FR-06 | Passed |
| UAT-WH-05 | A transfer or a cost-0 receipt posts nothing. | WH-FR-03, WH-BR-05 | Unit test |
| UAT-WH-06 | The first receipt of a never-stocked product succeeds. | WH-FR-05 | Passed |
| UAT-WH-07 | A shipment through a sales order books the cost of goods sold; a POS sale; redelivery of the event. | WH-FR-02, WH-BR-06 | Unit test only (not run live) |

---

## 13. Roles and permissions matrix

| Action | Permission |
|---|---|
| View stock, movements, valuation | `inventory:view` |
| Receive stock, reserve, release | `inventory:manage` |
| Create an adjustment | `inventory:adjustment:create` |
| Approve / reject an adjustment | `inventory:adjustment:approve` |
| Approve a stocktake | `inventory:stocktake:approve` |
| Close a valuation period | `inventory:valuation:close` |
| Manage warehouses | `inventory:warehouse:manage` |

---

## 14. Assumptions and constraints

1. One currency (VND) per tenant; the PO currency is carried but not converted.
2. The item's cost is the cost at the moment of the movement; Warehouse owns costing, Accounting never computes a cost.
3. Value of a shipment uses the item cost, not the per-lot cost (the lot log is an audit trail).
4. A receipt priced at 0 is treated as "price unknown", not as a free item.

---

## 15. Out of scope and open issues

Back-posting of pre-P1-1b stock; restock and cost reversal of returns (P1-5); landed cost, standard cost; FIFO / LIFO layer costing of issues (the item cost is used); reconciliation report between Warehouse valuation and the ledger inventory account. See [DEFERRED-ISSUES.md](../../DEFERRED-ISSUES.md).

---

## 16. Traceability and change log

| Task | Date | Requirements | Technical documents | Main tests |
|---|---|---|---|---|
| P1-1b stock and cost of goods | 2026-10-24 | WH-FR-01 … 06; WH-BR-01 … 09 | AutoPosting.md, event-catalog | `StockMovementCostTests` (WH), `PurchaseOrder*` (PO), `StockMovementPostingTests` (ACC) |
| BA baseline (this document) | 2026-10-25 | All | — | — |
