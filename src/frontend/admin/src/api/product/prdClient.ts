import { gatewayClient as apiClient } from '../client'

// ProductService's Brand/Category/PriceList controllers are BARE top-level
// routes (`api/v1/brands`, `api/v1/categories`, `api/v1/pricing/...`) — NOT
// nested under `/products/` like the public Product route. Per ocelot.json
// they are only reachable via the Admin-prefixed Gateway route
// "/prd/api/v1/{everything}", not the "/api" prefix that `apiClient`'s
// baseURL points at. So, mirroring mstClient.ts's idiom, we strip a trailing
// "/api" (if present) and re-root every ProductService admin call at
// "/prd/api/v1/...".

export const PRD_BASE = `/prd/api/v1`

export { apiClient }
