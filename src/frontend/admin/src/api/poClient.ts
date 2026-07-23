import { apiClient } from './client'

// PurchaseOrderService's controllers (PurchaseOrdersController, SuppliersController, ...)
// are mounted under the Gateway's `/po/api/...` route (see ocelot.json:
// UpstreamPathTemplate "/po/api/{everything}" -> purchaseorderservice), not under
// apiClient's default `/api` baseURL — same GATEWAY_ROOT re-rooting idiom as
// `src/api/masterdata/mstClient.ts` and the other per-service API clients.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')

export const PO_BASE = `${GATEWAY_ROOT}/po/api/v1`

export { apiClient }
