import { gatewayClient as apiClient } from './client'

// WareHouseService's controllers that are NOT nested under /inventory (Locations,
// StockMovements, TransferOrders, Stocktakes, StockAdjustments, Warehouses, Barcode) are
// not covered by the dedicated public `/api/v1/inventory...` route — only the Admin-only
// Gateway catch-all `/wh/api/v1/{everything}` reaches them (see ocelot.json). Uses
// `gatewayClient` (no baseURL) so this already-rooted path isn't double-prefixed with `/api` —
// same as `src/api/soClient.ts`/`src/api/poClient.ts`.

export const WH_BASE = `/wh/api/v1`

export { apiClient }
