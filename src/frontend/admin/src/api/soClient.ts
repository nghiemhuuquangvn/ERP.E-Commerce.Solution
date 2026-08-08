import { gatewayClient as apiClient } from './client'

// SaleOrderService's newer controllers (QuotationsController, CustomerSalesProfilesController,
// CommissionPlansController/CommissionsController) are NOT covered by the dedicated public
// `/api/v1/orders...` route that orders.api.ts uses — only the Admin-only Gateway catch-all
// `/so/api/v1/{everything}` reaches them (see ocelot.json). Uses `gatewayClient` (no baseURL)
// instead of `apiClient` so this already-rooted path isn't double-prefixed with `/api` — same
// as `src/api/poClient.ts`/`src/api/masterdata/mstClient.ts`.

export const SO_BASE = `/so/api/v1`

export { apiClient }
