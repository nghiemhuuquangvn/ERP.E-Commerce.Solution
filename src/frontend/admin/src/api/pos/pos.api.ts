import { gatewayClient as apiClient } from '../client'

// POSService is mounted under the Gateway's `/pos/api/...` route.
const BASE = '/pos/api/v1'

export interface PosPaged<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export interface Terminal {
  id: string
  name: string
  locationId?: string | null
  isActive: boolean
  lastSyncAt?: string | null
  createdAt: string
}

export interface Denomination {
  denomination: number
  count: number
  lineTotal: number
}

export interface CashShift {
  id: string
  terminalId: string
  cashierId: string
  openingBalance: number
  closingBalance?: number | null
  totalSales: number
  transactionCount: number
  status: 'Open' | 'Closed'
  openedAt: string
  closedAt?: string | null
  openingDenominations: Denomination[]
  closingDenominations: Denomination[]
}

export interface ShiftSummary {
  shiftId: string
  completedTransactionCount: number
  voidedTransactionCount: number
  totalSales: number
  totalDiscount: number
  totalTax: number
  salesByPaymentMethod: Record<string, number>
}

export type TransactionStatus = 'Open' | 'Completed' | 'Voided' | 'Suspended'

export interface PosTransaction {
  id: string
  terminalId: string
  cashierId: string
  promotionId?: string | null
  subTotal: number
  discountAmount: number
  taxAmount: number
  totalAmount: number
  paymentMethod?: string | null
  status: TransactionStatus
  voidReason?: string | null
  completedAt?: string | null
  voidedAt?: string | null
  createdAt: string
  items: { productId: string; productName: string; quantity: number; unitPrice: number; taxRate: number; discountAmount: number; lineTotal: number; taxAmount: number }[]
  tenders: { paymentMethod: string; amount: number }[]
}

export interface ZReport {
  date: string
  terminalId?: string | null
  shiftCount: number
  completedTransactionCount: number
  voidedTransactionCount: number
  totalSales: number
  totalDiscount: number
  totalTax: number
  salesByPaymentMethod: Record<string, number>
  shifts: ShiftSummary[]
}

export interface ShiftFilter { terminalId?: string; status?: string; from?: string; to?: string; page?: number; pageSize?: number }
export interface TransactionFilter extends ShiftFilter { cashierId?: string }

export const posApi = {
  terminals: (isActive?: boolean, signal?: AbortSignal) => apiClient.get<Terminal[]>(`${BASE}/pos/terminals`, { params: { isActive }, signal }).then((r) => r.data),
  createTerminal: (body: { name: string; locationId?: string | null }) => apiClient.post<Terminal>(`${BASE}/pos/terminals`, body).then((r) => r.data),
  renameTerminal: (id: string, name: string) => apiClient.put<Terminal>(`${BASE}/pos/terminals/${id}`, { name }).then((r) => r.data),
  setTerminalActive: (id: string, active: boolean) =>
    apiClient.post<Terminal>(`${BASE}/pos/terminals/${id}/${active ? 'activate' : 'deactivate'}`).then((r) => r.data),

  shifts: (filter: ShiftFilter, signal?: AbortSignal) => apiClient.get<PosPaged<CashShift>>(`${BASE}/pos/shifts`, { params: filter, signal }).then((r) => r.data),
  shiftSummary: (id: string) => apiClient.get<ShiftSummary>(`${BASE}/pos/shifts/${id}/summary`).then((r) => r.data),

  transactions: (filter: TransactionFilter, signal?: AbortSignal) =>
    apiClient.get<PosPaged<PosTransaction>>(`${BASE}/pos/transactions`, { params: filter, signal }).then((r) => r.data),
  receipt: (id: string) => apiClient.get<string>(`${BASE}/pos/transactions/${id}/receipt`, { responseType: 'text' }).then((r) => r.data),
  voidTransaction: (id: string, reason: string) => apiClient.post<PosTransaction>(`${BASE}/pos/transactions/${id}/void`, { reason }).then((r) => r.data),

  zReport: (date: string, terminalId?: string) =>
    apiClient.get<ZReport>(`${BASE}/pos/reports/z-report`, { params: { date, terminalId } }).then((r) => r.data),
}
