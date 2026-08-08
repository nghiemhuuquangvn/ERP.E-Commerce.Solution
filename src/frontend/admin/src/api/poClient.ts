import { gatewayClient as apiClient } from './client'

// PurchaseOrderService's controllers (PurchaseOrdersController, SuppliersController, ...)
// are mounted under the Gateway's `/po/api/...` route (see ocelot.json:
// UpstreamPathTemplate "/po/api/{everything}" -> purchaseorderservice), not under
// apiClient's default `/api` baseURL. Uses `gatewayClient` (no baseURL) so this already-rooted
// path isn't double-prefixed with `/api` — same as `src/api/masterdata/mstClient.ts` and the
// other per-service API clients.

export const PO_BASE = `/po/api/v1`

export { apiClient }
