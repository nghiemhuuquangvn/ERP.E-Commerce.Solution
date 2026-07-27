import { apiClient } from './client'

// SaleOrderService's newer controllers (QuotationsController, CustomerSalesProfilesController,
// CommissionPlansController/CommissionsController) are NOT covered by the dedicated public
// `/api/v1/orders...` route that orders.api.ts uses — only the Admin-only Gateway catch-all
// `/so/api/v1/{everything}` reaches them (see ocelot.json). Same GATEWAY_ROOT re-rooting idiom
// as `src/api/poClient.ts`/`src/api/masterdata/mstClient.ts`.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')

export const SO_BASE = `${GATEWAY_ROOT}/so/api/v1`

export { apiClient }
