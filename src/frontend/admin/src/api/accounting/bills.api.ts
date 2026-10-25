import { apiClient, ACC_BASE } from './accClient'

// BillDto — Status is a string on the wire (backend does
// `((BillStatus)b.Status).ToString()` when mapping), even though the list
// filter query param below is the underlying int enum value.
export interface BillDto {
  id: string
  billNumber: string
  supplierId: string
  poId: string
  /** The gross owed to the supplier; netAmount + taxAmount (input VAT). */
  amount: number
  netAmount: number
  taxRatePercent: number
  taxAmount: number
  dueDate: string
  status: string
  createdAt: string
  postedToLedger?: boolean
  postingWarning?: string | null
  foreignCurrencyCode?: string | null
  exchangeRate?: number | null
  foreignAmount?: number | null
}

// BillStatus enum (ERP.ACC.Domain.Enums.BillStatus): Pending=0, Approved=1, Paid=2
export const BILL_STATUS = {
  Pending: 0,
  Approved: 1,
  Paid: 2,
} as const

export interface ListBillsParams {
  supplierId?: string
  status?: number
  pageNumber?: number
  pageSize?: number
}

// CreateBillCommand(BillNumber, SupplierId, POId, Amount, DueDate) — no update endpoint exists.
export interface CreateBillRequest {
  billNumber: string
  supplierId: string
  poId: string
  amount: number
  dueDate: string
  /** VAT lines (rates, goods not subject to VAT); when given they replace the amount. */
  lines?: { category: number; ratePercent: number; amount: number; description?: string }[]
  /** A bill in a foreign currency: the currency, the total in it and the rate (base per 1 foreign). */
  currencyCode?: string
  foreignAmount?: number
  exchangeRate?: number
}

export interface APAgingDto {
  supplierId: string
  current: number
  days30: number
  days60: number
  days90Plus: number
  totalOutstanding: number
}

export const billsApi = {
  // Plain array response — NOT a PagedResult wrapper (see accClient.ts note).
  list: (params: ListBillsParams) =>
    apiClient.get<BillDto[]>(`${ACC_BASE}/bills`, { params }).then((r) => r.data),

  get: (id: string) => apiClient.get<BillDto>(`${ACC_BASE}/bills/${id}`).then((r) => r.data),

  create: (body: CreateBillRequest) =>
    apiClient.post<BillDto>(`${ACC_BASE}/bills`, body).then((r) => r.data),

  approve: (id: string, taxRatePercent?: number) =>
    apiClient.post<BillDto>(`${ACC_BASE}/bills/${id}/approve`, { taxRatePercent }).then((r) => r.data),

  markPaid: (id: string, paymentRate?: number) =>
    apiClient.post<BillDto>(`${ACC_BASE}/bills/${id}/mark-paid`, { paymentRate }).then((r) => r.data),

  apAging: (asOfDate?: string) =>
    apiClient
      .get<APAgingDto[]>(`${ACC_BASE}/bills/ap-aging`, { params: { asOfDate } })
      .then((r) => r.data),
}
