# Warehouse - stock per warehouse and lot (W2 of the Warehouse upgrade)

Added 2026-11-10. Decision record: `docs/architecture/adr-warehouse-upgrade.md`. Follows `DecimalQuantities.md` (W1). Production movements, reservations by source and the internal write API for Manufacturing come in W3-W5.

## 1. Purpose and actors
Until now the Warehouse knew how much of an item it had in total (and, loosely, per bin and per lot) but not **which warehouse holds it**. Manufacturing needs "how much of the paint is in the raw-material warehouse, in which lot, is it released or on quality hold".

| Actor | What changes for them |
|---|---|
| Warehouse manager | Sees stock per warehouse and lot, can name the receiving warehouse, sets the tenant's default warehouse, sees a lot's status. |
| Administrator | Chooses the **default warehouse** (Inventory settings) and marks a product as lot / serial controlled. |
| Planner / production (later) | Reads balances per warehouse and lot; a lot on quality hold is never allocated. |
| Sales / POS / Replenishment | Nothing changes: they keep reading the per-item total. |
| Auditor | `GET inventory/reconciliation` proves the figures agree. |

## 2. Requirements and acceptance criteria
| ID | Requirement | Acceptance |
|---|---|---|
| WB-1 | Stock is kept per (item, warehouse, lot); the per-item total stays and equals the sum of the balances. | Receive 5 kg into warehouse A (lot LA) and 3 kg into B (lot LB): balances A/LA 5, B/LB 3, item total 8. |
| WB-2 | One write path: every stock change goes through the ledger so the item total, lots, bins and balances move together. | Architecture test `NoStockMutationOutsideTheLedgerTests`; reconciliation shows no quantity or lot drift after the full regression. |
| WB-3 | A receipt names its warehouse, else the bin's warehouse, else the tenant's default warehouse. | Lot-less receipt without a warehouse lands in the default warehouse; changing the default moves where the next one lands. |
| WB-4 | The default warehouse is a tenant setting: the configured one, else the active warehouse coded MAIN, else the first active one by code; a tenant with no warehouse uses the "unassigned" bucket. | Unit tests of the resolver; setting an unknown or inactive warehouse is refused (HTTP 400). |
| WB-5 | A lot has a status (Available, QcHold, Blocked), a warehouse, an optional manufacture date and a source; only an Available lot is allocated. | A lot set to QcHold gets no reservation while other lots cover the quantity. |
| WB-6 | Concurrent writes to the same stock row never overwrite each other. | Two parallel sales of the last unit: one succeeds (201), the other answers 409; stock is 0, never negative. |
| WB-7 | Legacy stock without a warehouse is assigned to the tenant's default warehouse by the migration, with a report. | `011_StockBalance.sql` prints the default per tenant and the cases to review; second run changes nothing. |
| WB-8 | A product can be marked lot-controlled and serial-controlled. | Product create / `PUT products/{id}/tracking`; the flags appear in the product, in the product lookup, and in the Admin product form. |

## 3. Business rules
| ID | Rule |
|---|---|
| WB-R1 | `Inventory` remains the figure Sales, POS and Replenishment use (availability is checked on the item total). Per-warehouse availability is enforced from W4 (reservations by source). |
| WB-R2 | A lot-less issue takes from the named warehouse first, then from the other warehouses (largest first); only the part that is still missing makes the named warehouse negative (stock that was never received there). |
| WB-R3 | A reservation is held on the lots first-expiry-first (Available lots only); what no lot covers is held on the lot-less balance. Release and shipping undo / consume exactly that. |
| WB-R4 | A bin-to-bin transfer moves the balance between warehouses only when the two bins belong to different warehouses; the item total never changes. |
| WB-R5 | A manual sale under Average costing does not deplete lots; FIFO / LIFO deplete them oldest / newest first (Available lots only). |
| WB-R6 | A point-of-sale sale may take stock below zero (the goods already left); a manual sale may not. |
| WB-R7 | A lot number is unique per (tenant, item) when the existing data allows it (the migration reports duplicates and then creates only a plain index). |
| WB-R8 | No lot / serial enforcement yet: Warehouse has no Product client. A production receipt without a lot for a lot-controlled item is refused from W5, where Manufacturing passes the flags. |

## 4. Use cases
**UC-1 Receive into a warehouse and lot (main).** The clerk receives 5 kg with lot `LA`, warehouse A and expiry; the item total, the lot and the balance (A, LA) rise by 5. *Alternative:* no warehouse - the default warehouse. *Exception:* a tenant without any warehouse - the stock is "unassigned" and the Admin tab says so.
**UC-2 See where stock is.** Admin > Inventory > product > *By warehouse / lot*: one row per warehouse and lot with on-hand, reserved, available, lot status, expiry.
**UC-3 Set the default warehouse.** Admin > Settings > Inventory > tenant tab > *Default warehouse*.
**UC-4 Hold a lot.** A lot with status QcHold stays in the counts but is skipped by allocation (status is set by the production receipt flow from W5; today by SQL or a future screen).
**UC-5 Reconcile.** `GET inventory/reconciliation` lists items whose total differs from the sum of their balances, and lots that differ from their balance. A *Reserved* line can come from the older sale code (see 9).
**UC-6 Mark an item lot / serial controlled.** Admin > Products > edit > two checkboxes.

## 5. Data model
`StockBalances` (`BalanceId`, `TenantId`, `ProductId`, `WarehouseId` null = unassigned, `LotId` null = not in a lot, `Quantity`, `ReservedQuantity`, `LockedQuantity` decimal(18,6), `RowVersion`); unique `(TenantId, ProductId, WarehouseId, LotId)`. `InventoryLots` + `WarehouseId`, `Status` (0/1/2), `ManufactureDate`, `Source` (Receipt / Production / Return), `RowVersion`. `Inventories` + `RowVersion`. `TenantInventorySettings` + `DefaultWarehouseId`. Products + `IsLotControlled`, `IsSerialControlled`. SQL: `Warehouse/011_StockBalance.sql`, `Product/020_LotSerialFlags.sql`.

## 6. Integrations
No new event: the payloads of W1 are unchanged. `internal/v1/product-lookup` returns the two flags; Manufacturing's `ProductInfo` reads them (optional fields). Admin: new tab, lots column, settings select, product checkboxes (en / vi).

## 7. Permissions
`inventory:view` for balances and reconciliation; `settings:inventory:tenant:manage` for the default warehouse; `products:edit` for the flags. No new permission.

## 8. Migration and rollout
1. `Product/020`, deploy Product and Manufacturing (optional record fields). 2. `Warehouse/011` (rehearsed on a restored copy first, run twice), deploy Warehouse, then Admin. The backfill assigns **every legacy unit and lot to the tenant's default warehouse**: on the live glcheck database 1 045 balance rows were created, the SYSTEM tenant's default is MAIN, the glcheck tenant's first active warehouse. A tenant with several real warehouses should set the default before running the script, then move stock with transfers.

## 9. Assumptions and open issues
- The reserved figure of the older code can disagree: a manual sale lowers the item's reservation (`SellStock`) without releasing a lot, so a *Reserved* line may show in the reconciliation. Quantities never drift.
- The legacy reserve still checks availability on the item total; a reservation can sit in a warehouse that does not hold the stock. W4 changes that for production reservations.
- `Inventories.Cost` is still one cost per item (W3 persists cost per movement).
- The reconciliation reads all items of the tenant into memory (two group-by queries): fine for thousands, to be paged for millions.
- A concurrent write answers 409 and the caller retries; the Sales consumer already retries five times, other consumers rely on redelivery.
- The lot status has no screen yet.

## 10. UAT
1. Receive 5 kg with a lot into warehouse A and 3 kg into B: *By warehouse / lot* shows two rows.
2. Set the default warehouse to A, receive 2 kg without a warehouse and lot: a row "A / not in a lot" with 2.
3. Put the earliest-expiry lot on hold (SQL) and reserve 10 kg: the held lot is not reserved.
4. Release the reservation: no reserved quantity remains anywhere.
5. Open `inventory/reconciliation`: empty for quantities and lots.
6. Edit a product, tick *Tracked by lot*, save, reopen: still ticked.
