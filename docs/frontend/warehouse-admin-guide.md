# Admin Warehouse (Inventory) CRUD pattern

The Admin app's `/inventory/*` pages are the UI for **WareHouseService** (port
5004, Gateway prefix `/wh`) — Inventory, Locations, Warehouses (sites), Stock
Adjustments, Stocktakes, Transfer Orders, Barcode resolve, and (added
2026-07-27) Cost History / Inventory Valuation Snapshots. This guide covers
the pattern used after the 2026-07 reconciliation, which added bin-level
stock tracking, an approval workflow for adjustments/stocktakes, a parent
`Warehouse` site entity, FEFO lot picking, and barcode resolve to the
backend — see `docs/business/Warehouse/module-07-warehouse.md` §12/the
service's own `README.md` for the business rationale — and the follow-up
Module 8 reconciliation (`docs/business/Inventory/module-08-inventory.md`
§12), which fixed the reservation soft-hold/ship-time bugs and added
`CostConsumptionLog`/`InventoryValuationSnapshot`.

## Backend quirk: two different Gateway paths for the same service

Unlike every other domain guide in this folder, WareHouseService's endpoints
are **not** uniformly reachable through one client. `InventoryController`,
`LotsController` (nested at `/inventory/lots`), and `SerialsController`
(nested at `/inventory/serials`) sit behind a dedicated public Gateway route
(`/api/v1/inventory` + wildcard) and use the plain `apiClient` +
`/v1/inventory/...` paths. Every other controller — `LocationsController`
(`/locations`), `StockMovementController` (`/movements`),
`TransferOrdersController` (`/transfer-orders`), `StocktakesController`
(`/stocktakes`), `StockAdjustmentsController` (`/stock-adjustments`),
`WarehousesController` (`/warehouses`), `BarcodeMappingsController`
(`/barcode`) — is **not** nested under `/inventory` and is only reachable via
the Admin-only Gateway catch-all `/wh/api/v1/{everything}`. Before adding a
new WareHouseService endpoint to the frontend, check the controller's actual
`[Route(...)]` attribute to know which client to use — don't assume.

- `src/api/inventory.api.ts` — bare `apiClient`, paths like
  `/v1/inventory/{productId}`, `/v1/inventory/lots/...`. Also exports a
  separate `movementsApi` that uses `whClient` instead, since
  `StockMovementController` is not nested despite living in the same file
  historically.
- `src/api/whClient.ts` — `WH_BASE = \`${GATEWAY_ROOT}/wh/api/v1\``, re-export
  of `apiClient`. Every other `.api.ts` in this domain
  (`locations.api.ts`, `warehouses.api.ts`, `stockAdjustments.api.ts`,
  `stocktakes.api.ts`, `transferOrders.api.ts`, `barcodeMappings.api.ts`)
  imports `WH_BASE` from here — don't hardcode `/wh/api/v1` again.

## Backend quirk: Stock Adjustment is a request/approve workflow, not a direct write

The old `POST /inventory/{productId}/adjust` endpoint was **removed**. A
Warehouse Manager can no longer write to `Inventory` directly; every
adjustment must be requested (`stockAdjustmentsApi.request`, Status=Pending,
no Inventory mutation yet) and then approved or rejected by someone holding
`inventory:adjustment:approve` before it takes effect. Same shape for
Stocktakes: `complete` (locks counts, computes variance, no Inventory writes)
is a distinct, separately-gated step from `approve` (posts the variance to
Inventory) — see `StocktakeDetailPage.tsx`'s status-gated action buttons.
Don't reintroduce a one-step adjust/complete action if a future endpoint
looks like it could support it; the approval gate is a deliberate business
rule, not a missing feature.

## Backend quirk: bin-level stock is additive, not a replacement

`Inventory` (keyed by `ProductId`) is still the cross-location rollup
(`totalQuantity`/`availableQuantity`/`reservedQuantity`/`lockedQuantity`).
`InventoryLocationStock` (keyed by `ProductId`+`LocationId`) is a newer,
additive breakdown — a product with no bin-level data yet still returns one
synthetic row from `GET /v1/inventory/{productId}` (`locations: [...]`) as a
fallback. `InventoryDetailPage.tsx` shows a "no bin-level data" hint
(`inventory.noBinLevelData`) when it detects this fallback row rather than
presenting it as real data — check for that same signal before building any
new page that consumes `locations`.

## File layout

1. One `.api.ts` per entity under `src/api/` (`inventory.api.ts`,
   `locations.api.ts`, `warehouses.api.ts`, `stockAdjustments.api.ts`,
   `stocktakes.api.ts`, `transferOrders.api.ts`, `barcodeMappings.api.ts`,
   `valuationSnapshots.api.ts`), mirroring the real controller's
   request/response shape exactly — read
   `src/services/WareHouseService/src/ERP.WH.API/Controllers/*.cs` first, per
   the Gateway-path quirk above.
2. Two page shapes are used, matching entity complexity:
   - **Flat CRUD** (`LocationsPage.tsx`, `WarehousesPage.tsx`) — list + modal,
     same convention as `masterdata-admin-guide.md` (one file, modal function
     above the page component).
   - **Dedicated-route detail/form pages** for anything with line items or a
     multi-step lifecycle: `InventoryDetailPage.tsx` (tab strip:
     locations/lots/serials/**costHistory**, plus a "Request Adjustment"
     modal), `StocktakeDetailPage.tsx` (line table with inline count-input,
     gated Complete/Approve/Cancel buttons), `TransferOrderFormPage.tsx`
     (dedicated create route with from/to location pickers and a product
     line-item table, mirrors `OrderFormPage.tsx` from the Sales module).
   - **Lookup + form panel** (`BarcodeResolvePage.tsx`,
     `InventoryValuationSnapshotsPage.tsx` — added 2026-07-27): a "close/add"
     form (permission-gated) alongside a separate "look up by key" panel that
     handles a 404/not-found response as a normal empty state, not an error
     — see `valuationSnapshotsApi.getByPeriod`/`barcodeMappingsApi.resolve`'s
     shared `axios.isAxiosError(err) && err.response?.status === 404 → null`
     idiom, and copy it for any future "does this key exist yet" lookup
     instead of letting React Query's `isError` path handle a plain
     not-found.
3. `DataTable` requires `T extends {id: string}`. Several backend DTOs use a
   different PK field name (`productId`, `locationId`, `adjustmentId`, etc.)
   — map to `{...x, id: x.realIdField}` before passing rows in
   (`InventoryListPage`, `LocationsPage`, `StockAdjustmentsPage`,
   `StocktakeListPage`, `TransferOrderListPage` all do this;
   `WarehousesPage` doesn't need it since `Warehouse.id` already matches).
4. Wire into `src/router/index.tsx` under `inventory/*` — static segments
   (`inventory/locations`, `inventory/warehouses`, etc.) must be declared
   before the dynamic `inventory/:productId` route, per react-router v6
   matching order.
5. Wire into `src/components/layout/Sidebar.tsx`'s `INVENTORY_CHILDREN`
   array (collapsible nav group, same mechanism as `ACCOUNTING_CHILDREN`/
   `HRM_CHILDREN`), each child gated on its own permission — see below.
6. Add i18n keys under `inventory.*` in both `en.json`/`vi.json` — see
   [i18n-guide.md](i18n-guide.md).

## Permissions

`inventory:view` / `inventory:manage` remain the coarse pair for endpoints
unchanged by the reconciliation (Locations CRUD, movement history, lots/
serials read). New granular permissions gate only the new workflow steps —
don't gate an unrelated existing endpoint with one of these by mistake:

| Permission | Gates |
|---|---|
| `inventory:adjustment:create` | `StockAdjustmentsPage` request action |
| `inventory:adjustment:approve` | `StockAdjustmentsPage` approve/reject actions |
| `inventory:stocktake:approve` | `StocktakeDetailPage` Approve action (`complete` itself stays under `inventory:manage`) |
| `inventory:barcode:view` | `BarcodeResolvePage` (add-mapping form still needs `inventory:manage`) |
| `inventory:warehouse:manage` | `WarehousesPage` create/update/activate/deactivate |
| `inventory:valuation:close` (added 2026-07-27) | `InventoryValuationSnapshotsPage`'s "Close Period" form — the lookup-by-period panel next to it only needs `inventory:view` (read) |

## Conventions already established

- `StatusBadge` colors for this domain's statuses (`InProgress`,
  `PendingApproval`, plus the pre-existing `Pending`/`Approved`/`Rejected`/
  `Completed`/`Cancelled`) live in the same shared `STATUS_COLORS` map in
  `src/components/shared/StatusBadge.tsx` as every other domain.
  `InventoryListPage` maps the read-only `OK`/`Low Stock` state onto the
  existing `Active`/`Pending` colors rather than adding new ones.
- Modal styling: same fixed-overlay `<div className="fixed inset-0 z-50 ...">`
  wrapping a `.card`, copied from the masterdata pages.
- FEFO lot picking and barcode resolve are backend-only concerns exposed
  read-only in the UI (`InventoryDetailPage`'s lots tab, `BarcodeResolvePage`'s
  lookup panel) — there is no manual lot-selection UI, the backend always
  auto-allocates earliest-expiry-first.
- (Added 2026-07-27) Reservation is now a non-destructive soft hold on the
  backend — the lots tab shows `Reserved`/`Available` columns per lot
  (`InventoryLot.ReservedQuantity`/`AvailableQuantity`) alongside the existing
  `Remaining`/`Received`, so a reader can see held-but-not-yet-shipped stock
  distinctly from what's actually still on hand.
- (Added 2026-07-27) `InventoryDetailPage`'s Cost History tab and the new
  Valuation Snapshots page are both read-heavy audit views (`CostConsumptionLog`,
  `InventoryValuationSnapshot`) — no create/edit UI beyond the single "close a
  period" action, since both are meant to be immutable/append-only on the
  backend. Don't add an edit affordance for either.

## Cross-service: Purchase Requests (PurchaseOrderService, not this service)

`StockAlertEvent` (published from this domain when a product hits its
reorder point) is now consumed by PurchaseOrderService to create a
suggestion-only `PurchaseRequest` — surfaced in the Admin app at
`/purchasing/requests` (`PurchaseRequestsPage.tsx`,
`src/api/purchaseRequests.api.ts`, gated on `purchase_request:view`/`:manage`).
This page lives in the Purchasing nav group, not Inventory, since the data
and permissions belong to PurchaseOrderService — but it's worth knowing about
here since the trigger originates from this domain's event. There is no
dedicated `purchasing-admin-guide.md` yet; the Purchasing module's pages
(`SupplierListPage`, `PurchaseOrderListPage`, `PurchaseRequestsPage`) each
follow whichever pattern from this folder fits their shape (flat CRUD vs.
status-filtered list) rather than a documented convention of their own.

## When WareHouseService gains a new sub-domain or endpoint

Read the real controller first (checking its `[Route(...)]` attribute to
pick the correct client per the Gateway-path quirk above), add a `.api.ts`
mirroring it exactly, and verify any permission string it checks is actually
defined in `src/Shared/Authorization/PermissionConstants.cs` and granted to
some role in `RoleDefinitions.cs` before assuming the UI will work.
