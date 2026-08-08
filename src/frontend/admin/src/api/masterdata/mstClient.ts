import { gatewayClient as apiClient } from '../client'

// The MST (SharedMasterDataService) gateway route is mounted at `/mst/api/...`,
// not under the `/api` prefix that `apiClient`'s baseURL points at (see
// ocelot.json: UpstreamPathTemplate "/mst/api/{everything}" vs. other services'
// "/api/v1/{...}"). Uses `gatewayClient` (no baseURL, aliased to `apiClient` here)
// so this already-rooted path isn't double-prefixed with `/api`.

export const MST_BASE = `/mst/api/v1/masterdata`

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
