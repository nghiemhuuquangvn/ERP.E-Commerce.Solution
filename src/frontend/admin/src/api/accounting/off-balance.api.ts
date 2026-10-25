import { apiClient, ACC_BASE } from './accClient'

// OffBalanceEntryDto — a movement of a class 0 account (001 – 008), kept in its own register outside the double entry.
export type OffBalanceDirection = 'In' | 'Out'

export interface OffBalanceEntry {
  id: string
  accountId: string
  accountNumber: string
  accountName: string
  entryDate: string
  direction: OffBalanceDirection
  amount: number
  description: string | null
  reference: string | null
  status: 'Posted' | 'Voided'
  voidReason: string | null
}

export interface PostOffBalanceEntryRequest {
  accountId: string
  entryDate?: string
  direction: OffBalanceDirection
  amount: number
  description?: string
  reference?: string
}

export interface OffBalanceLine {
  accountNumber: string
  accountName: string
  opening: number
  in: number
  out: number
  closing: number
  isNegative: boolean
}

export interface OffBalanceReport {
  fromDate: string
  toDate: string
  lines: OffBalanceLine[]
  notes: string[]
}

export const offBalanceApi = {
  list: (params: { accountId?: string; fromDate?: string; toDate?: string }) =>
    apiClient.get<OffBalanceEntry[]>(`${ACC_BASE}/accounting/off-balance`, { params }).then((r) => r.data),

  post: (body: PostOffBalanceEntryRequest) =>
    apiClient.post<OffBalanceEntry>(`${ACC_BASE}/accounting/off-balance`, body).then((r) => r.data),

  void: (id: string, reason: string) =>
    apiClient.post<OffBalanceEntry>(`${ACC_BASE}/accounting/off-balance/${id}/void`, { reason }).then((r) => r.data),

  report: (params: { fromDate: string; toDate: string }) =>
    apiClient.get<OffBalanceReport>(`${ACC_BASE}/accounting/reports/off-balance`, { params }).then((r) => r.data),
}
