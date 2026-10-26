# Warehouse — replenishment: min / max / safety stock, purchase requests, orders (ROADMAP P1-9)

Business analysis written 2026-10-26. It describes what the system does after P1-9. The buying side after the order is created is in `docs/business/Purchasing/ProcureToPay.md`; the old warehouse module analysis is `module-07-warehouse.md`.

## 1. Purpose and scope
Before P1-9 a product had two numbers, a reorder point (default 100) and a reorder quantity (default 500), that **no screen could edit**; "low stock" meant *total* quantity at or below the point, so goods already reserved for customers still counted as stock; every movement of a low product raised another alert; and the purchase request that Purchasing made from the alert only changed status — nobody got a supplier, a price or an order from it. Now:
- each product has a **stock policy**: minimum, maximum and safety stock, edited on the inventory page;
- a product is **low** when its **available** stock (on hand − reserved − locked) is at or below the minimum;
- Warehouse tells Purchasing **once** per time a product falls below its minimum (and again when it recovers, and once a day while it is still low);
- Purchasing turns the alert into a **proposal**: how much to buy to reach the maximum, counting the goods **already on order**, and from whom (the product's **preferred supplier**);
- one click creates **draft purchase orders** (one per preferred supplier) and one **RFQ** for the products that have no preferred supplier. A person still approves everything.

Out of scope (DEFERRED §4t): demand forecasting and average-usage based minimums, safety stock computed from lead time, a policy per warehouse / location, minimum order quantity and pack sizes, automatic approval or sending of the order, e-mail to suppliers, supplier price breaks, a unit of measure other than "pcs" on the generated order lines.

## 2. Actors and permissions
| Actor | Does | Permission |
|---|---|---|
| Warehouse manager | sets min / max / safety of a product; runs the scan now | `inventory:manage` (view: `inventory:view`) |
| Buyer | reviews proposals, dismisses, creates the orders; marks the preferred supplier of a product | `purchase_request:view/manage`, `supplier:update` |
| System | sends the alerts (RabbitMQ `warehouse_exchange`), the daily scan at the configured hour, creates / refreshes / closes proposals | service |

No new permission: *Create orders* uses `purchase_request:manage`, the preferred flag `supplier:update`.

## 3. Process
1. **Policy.** Open the product in *Inventory*, card *Stock policy*: minimum, safety stock, maximum. Rules: 0 ≤ safety ≤ minimum < maximum. Saving re-sends the alert if the product is below the new minimum (so an open proposal gets the new figures) or reports that it is no longer low (so the proposal is closed). Products that existed before P1-9 start with minimum = old reorder point, maximum = reorder point + reorder quantity, safety 0.
2. **Alert.** After any stock change (receipt, sale, adjustment, stocktake, POS, reservation fulfilment): if available ≤ minimum and no alert is outstanding → one alert. When stock is back above the minimum → one closing message and the mark is cleared.
3. **Daily scan.** `ReplenishmentScanJob` runs once per UTC day from `Replenishment:ScanHour` (default 2) for every tenant and re-sends the alert of products still low whose last alert is older than 24 h (an alert lost on the way, or a proposal that was dismissed, comes back). *Scan stock now* on the Purchase requests page does the same for the user's tenant.
4. **Proposal.** Purchasing receives the alert and works out the need: `covered = available + on order`; if `covered ≤ minimum`, the **suggested quantity = maximum − covered**. *On order* = Σ (ordered − accepted received) of the product's lines on orders that are Draft, Submitted, Pending finance approval, Approved, Ordered or Partially received (a draft counts, so the same need is not ordered twice). An open proposal for the product is updated, never duplicated; if the need has disappeared (stock recovered, or orders now cover it) the open proposal is closed as *Dismissed* with the note "Covered by stock or orders". The proposal records available, on order, min / max / safety, and the preferred supplier with the price and lead time of its supplier item.
5. **Create orders** (button, all open proposals or the ticked ones). Proposals whose product has a **preferred supplier** are grouped by supplier into one **draft purchase order** each: one line per product at the supplier item's price, quantity = suggested, expected delivery = today + the longest lead time, the supplier's currency. Proposals **without** a preferred supplier go into **one RFQ** (draft) to the suppliers that have a supplier item for any of those products, or to every active supplier when none has. Each proposal is linked to its order / RFQ and becomes *Ordered* / *Quoting*; the result panel lists them with links. A proposal that is not open any more is skipped and reported, so running it twice orders nothing twice. If no supplier exists to ask, the proposal stays open and is reported.
6. **After the order.** The buyer submits and approves the draft as usual (or sends the RFQ, compares and awards — P1-8). Goods received raise the stock above the minimum; the closing message closes anything still open.

**Worked example.** Shirt: minimum 10, safety 3, maximum 50. Stock 9, nothing on order → alert → proposal 41. The buyer creates the order for 41 (draft). Before it is approved a sale takes available to 4: the new alert finds `4 + 41 = 45 > 10` → no new proposal. If 30 are on order and 9 in stock, `39 > 10`: no proposal either. A product oversold to −2 with nothing on order needs `50 − (−2) = 52`.

## 4. Data and states
| Item | Content |
|---|---|
| `Inventory` (Warehouse, migration 009) | MinStock, MaxStock, SafetyStock, LowStockAlertedAt (set when an alert is sent, cleared when stock is back above the minimum); ReorderPoint / ReorderQuantity follow min and max − min |
| `StockAlertEvent` (RabbitMQ `warehouse_exchange`, key `warehouse.StockAlertEvent`) | old fields + ProductSku, AvailableQuantity, MinStock, MaxStock, SafetyStock. When AvailableQuantity > MinStock the message means "no longer low". An older sender (no MaxStock) is read as min = reorder point, max = point + quantity, available = total |
| `PurchaseRequest` (Purchasing, migration 013) | ProductId, ProductCode (= warehouse SKU), figures above, PreferredSupplierId, UnitPrice, LeadTimeDays, PurchaseOrderId, RfqId, Note |
| `SupplierItem.IsPreferred` | one preferred supplier per product and tenant; marking one clears the others |
| Proposal status | Suggested (open) → Ordered (draft PO created) / Quoting (RFQ created) / Dismissed (by a person, or covered) / Converted (marked done by hand, as before) |

## 5. Requirements
| ID | Requirement | Acceptance |
|---|---|---|
| FR-R1 | A product has a minimum, maximum and safety stock that a warehouse manager can edit. | Saving 10 / 50 / 3 shows them on the page and in the list; safety 11 against minimum 10, or maximum equal to the minimum, is refused with a message (400). |
| FR-R2 | Low stock is judged on available stock. | A product with 15 on hand and 8 reserved against minimum 10 is low. |
| FR-R3 | An alert is sent once per time below the minimum, and a closing message when stock recovers. | Two receipts that keep the product low send one alert; receiving above the minimum sends one message with available > minimum. |
| FR-R4 | A daily per-tenant scan and a manual scan repeat the alert for products still low after 24 h. | Scan within 24 h of an alert sends 0; the result shows checked / below minimum / alerts sent. |
| FR-R5 | Purchasing proposes the quantity to reach the maximum, counting goods on order, and never keeps two open proposals for a product. | Stock 1, min 10, max 50 → 49; with 30 on order → no proposal; a second alert updates the same row. |
| FR-R6 | A proposal that is no longer needed is closed automatically with a reason. | Stock raised above the minimum → the open proposal is Dismissed "Covered by stock or orders". |
| FR-R7 | A supplier item can be marked preferred; one per product. | Marking B's item clears A's flag. |
| FR-R8 | Create orders groups proposals with a preferred supplier into one draft order per supplier at the supplier item's price. | Shirt 49 @ 90 and pants 49 @ 210 at supplier A → one draft PO, total 14,700. |
| FR-R9 | Proposals without a preferred supplier go into one RFQ. | Hat and sock → one draft RFQ with two lines and the suppliers that can supply them (all active suppliers if none can). |
| FR-R10 | Creating orders twice orders nothing twice; a proposal that cannot be ordered stays open and is reported. | Second run with the same ids: 0 orders, 4 skipped. |
| FR-R11 | The Purchase requests page shows available, on order, min–max, suggested quantity, preferred supplier, links to the order / RFQ created, and offers *Create orders* and *Scan stock now*. | See section 8. |

## 6. Business rules
- BR-R1 Policy: 0 ≤ safety ≤ minimum < maximum; the minimum is the point at which one orders, the safety stock is the part of it that is kept back, it is never ordered separately.
- BR-R2 A product is low when available ≤ minimum (available = on hand − reserved − locked); negative stock (POS sale of an uncounted item) is low.
- BR-R3 The need is `maximum − (available + on order)` and exists only when `available + on order ≤ minimum`.
- BR-R4 On order counts open quantity (ordered − accepted received) of draft and live orders, not finished, cancelled or rejected ones; rejected goods still owed are included because the supplier owes a replacement.
- BR-R5 The product code of an order line is the warehouse SKU. A product without a SKU gets a code made of its id and its on-order quantity is not known (0).
- BR-R6 Orders created from proposals are always **drafts**; nothing is submitted or approved automatically.
- BR-R7 The preferred supplier is looked up again when orders are created, so a change since the alert is respected; only active suppliers count.
- BR-R8 The alert consumer retries 5 times, then logs and drops the message (the daily scan sends it again).

## 7. Use cases
**UC-R1 Set the policy.** Main: manager opens the product, edits the three numbers, saves; the page shows "Below minimum" if applicable. Alternative: the product is already below the new minimum → the open proposal is refreshed. Exception: invalid combination → message, nothing saved; unknown product → 404.
**UC-R2 Review and order.** Main: buyer opens *Purchase requests*, ticks (or not) the proposals, clicks *Create orders*, reads the result (orders, RFQ, skipped), opens the draft order or RFQ. Alternative: dismiss a proposal, or mark it converted. Exception: no preferred supplier and no active supplier → the proposal stays open and is listed as skipped.
**UC-R3 Recover.** Main: goods received raise stock above the minimum → closing message → the open proposal is dismissed. Alternative: the order exists → the proposal is Ordered and stays so.
**UC-R4 Scan.** Main: at the configured hour each tenant is scanned; a manager can run it for their own tenant at any time. Exception: one tenant failing does not stop the others (logged).

## 8. Screens (admin)
- **Inventory → product**: card *Stock policy* with three number fields (Minimum, Safety stock, Maximum), an amber "Below minimum" badge, a message when the combination is invalid, *Save* (only with `inventory:manage`).
- **Inventory list**: columns Minimum and Maximum replace Reorder point; the "Low stock only" filter now means available ≤ minimum.
- **Purchasing → Purchase requests**: tabs Suggested / Ordered / Quoting / Dismissed / Converted / All; table with tick box, product (name and code), Available, On order, Min – max (safety), Suggested qty, Preferred supplier (name, price, lead time, or "None — goes to an RFQ"), Status (with the note and a link *Open purchase order* / *Open RFQ*), Dismiss / Convert; buttons *Scan stock now* and *Create orders from all open (n)* / *from n selected*; a result panel after creating.
- **Purchasing → Suppliers → supplier → Items tab**: a *Preferred* tick box per item.

## 9. Integrations
Warehouse → Purchasing: `StockAlertEvent` over RabbitMQ (fields in section 4), tenant stamped, consumed by `WarehouseEventRabbitMqConsumer`. No call from Purchasing to Warehouse: the on-order quantity is Purchasing's own data. The orders and RFQs created go through the existing P1-8 flows.

## 10. Assumptions and open issues
- The warehouse SKU equals the product code on purchase order lines (DEFERRED risk R2).
- An RFQ made from proposals asks only the suppliers that have a supplier item for one of its products; a product nobody supplies is therefore not covered when others are (§4t AL3).
- Open issues: DEFERRED §4t.

## 11. UAT scenarios
| ID | Scenario | Expected |
|---|---|---|
| UAT-R1 | Save policy 10 / 50 / 3; then 10 / 50 / 11 and 10 / 10 / 3 | first saved, the others 400 |
| UAT-R2 | Product with 1 on hand, policy 10 / 50 / 3 | one open proposal for 49 with the preferred supplier's name and price |
| UAT-R3 | Receive 1 more (still low) | no new alert; same single proposal |
| UAT-R4 | *Scan stock now* inside 24 h | 0 alerts sent |
| UAT-R5 | *Create orders* for shirt + pants (supplier A) and hat + sock (no preferred) | one draft PO (49 @ 90, 49 @ 210, 14,700 VND), one draft RFQ with 2 lines; proposals Ordered / Quoting |
| UAT-R6 | *Create orders* again with the same ids | no orders, 4 skipped |
| UAT-R7 | Save the shirt policy again while its order is a draft | no new proposal (available + on order above the minimum) |
| UAT-R8 | Receive 100 of a product with an open proposal | proposal Dismissed "Covered by stock or orders"; the product is no longer low |
| UAT-R9 | Mark B's supplier item preferred while A's is | only B's stays preferred |
| UAT-R10 | Policy of an unknown product | 404 |
| UAT-R11 | User without `inventory:manage` calls the policy or scan endpoints | 403 (attribute; checked by the architecture test) |

Verified live on tenant `glcheck` 2026-10-26: UAT-R1…R8 and R10; UAT-R9 (one preferred supplier per product) by unit test; UAT-R11 by the architecture test.
