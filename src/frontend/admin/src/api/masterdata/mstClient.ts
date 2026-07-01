import { apiClient } from '../client'

// The MST (SharedMasterDataService) gateway route is mounted at `/mst/api/...`,
// not under the `/api` prefix that `apiClient`'s baseURL points at (see
// ocelot.json: UpstreamPathTemplate "/mst/api/{everything}" vs. other services'
// "/api/v1/{...}"). `apiClient`'s baseURL is typically
// "http://localhost:5000/api", so we strip a trailing "/api" (if present) and
// re-root every masterdata call at "/mst/api/v1/masterdata/...".
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')

export const MST_BASE = `${GATEWAY_ROOT}/mst/api/v1/masterdata`

/** PagedResult<T> shape returned by SharedMasterDataService controllers (camelCased by ASP.NET). */
export interface MstPagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
}

export { apiClient }
