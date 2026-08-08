import { gatewayClient as apiClient } from '../client'

// The HRM (HRMService) gateway route is mounted at `/hrm/api/...`, not under the
// `/api` prefix that `apiClient`'s baseURL points at (see ocelot.json:
// UpstreamPathTemplate "/hrm/api/{everything}" vs. other services'
// "/api/v1/{...}"). Same pattern as masterdata/mstClient.ts — strip a trailing
// "/api" (if present) and re-root every HRM call at "/hrm/api/v1/hrm/...".

export const HRM_BASE = `/hrm/api/v1/hrm`

/** PagedResult<T> shape returned by HRMService controllers (camelCased by ASP.NET). */
export interface HrmPagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export { apiClient }
