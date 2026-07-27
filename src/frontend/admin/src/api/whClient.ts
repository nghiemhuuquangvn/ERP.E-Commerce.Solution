import { apiClient } from './client'

// WareHouseService's controllers that are NOT nested under /inventory (Locations,
// StockMovements, TransferOrders, Stocktakes, StockAdjustments, Warehouses, Barcode) are
// not covered by the dedicated public `/api/v1/inventory...` route — only the Admin-only
// Gateway catch-all `/wh/api/v1/{everything}` reaches them (see ocelot.json). Same
// GATEWAY_ROOT re-rooting idiom as `src/api/soClient.ts`/`src/api/poClient.ts`.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')

export const WH_BASE = `${GATEWAY_ROOT}/wh/api/v1`

export { apiClient }
