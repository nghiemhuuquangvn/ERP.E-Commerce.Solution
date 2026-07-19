import { apiClient } from '../client'

// The CRM (CRMService) gateway route is mounted at `/crm/api/...`, not under the
// `/api` prefix that `apiClient`'s baseURL points at (see ocelot.json:
// UpstreamPathTemplate "/crm/api/{everything}"). `apiClient`'s baseURL is typically
// "http://localhost:5000/api", so we strip a trailing "/api" (if present) and
// re-root every CRM call at "/crm/api/v1/crm/...".
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')

export const CRM_BASE = `${GATEWAY_ROOT}/crm/api/v1/crm`

/** PagedResult<T> shape returned by CRMService controllers (camelCased by ASP.NET). */
export interface CrmPagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export { apiClient }
