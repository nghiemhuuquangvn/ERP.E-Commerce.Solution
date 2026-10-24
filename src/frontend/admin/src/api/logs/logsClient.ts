import { gatewayClient as apiClient } from '../client'

// LoggingService's Ocelot route is mounted at `/logs/api/...` (see ocelot.json:
// UpstreamPathTemplate "/logs/api/{everything}" and "/logs/api/v1/logs/client"),
// not under the `/api` prefix that `apiClient`'s baseURL points at. Same
// `gatewayClient` approach as api/masterdata/mstClient.ts.

export const LOGS_BASE = `/logs/api/v1`

export interface LogsPagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export { apiClient }
