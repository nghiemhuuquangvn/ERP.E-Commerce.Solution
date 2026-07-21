import { apiClient } from '../client'

// The ACC (AccountingService) gateway route is mounted at `/acc/api/...`, not
// under the `/api` prefix that `apiClient`'s baseURL points at (see
// ocelot.json: UpstreamPathTemplate "/acc/api/{everything}"). `apiClient`'s
// baseURL is typically "http://localhost:5000/api", so we strip a trailing
// "/api" (if present) and re-root every accounting call at "/acc/api/v1/...".
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')

export const ACC_BASE = `${GATEWAY_ROOT}/acc/api/v1`

// NOTE: unlike SharedMasterDataService, AccountingService's "paginated" list
// endpoints (Invoices/Bills/JournalEntries — pageNumber/pageSize query params)
// return a plain `List<TDto>` — there is no wrapper object and no total
// count/page count in the response (confirmed via
// AccountingQueryHandlers.cs's ListInvoicesQueryHandler et al., which return
// `List<InvoiceDto>` directly). Each entity's `.api.ts` should type its list
// function's return as `TDto[]`, not a paged-result wrapper. Pagination UI
// for these 3 entities can only do Previous/Next (no total-pages display) —
// see masterdata-admin-guide.md's Pagination component usage for the shape
// this constrains.

export { apiClient }
