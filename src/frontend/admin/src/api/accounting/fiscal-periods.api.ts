import { apiClient, ACC_BASE } from './accClient'

// Mirrors ERP.ACC.Domain.Enums.FiscalPeriodStatus (Open = 0, Closed = 1) — FiscalPeriodDto
// serializes this as the enum's string member name, not its numeric value.
export type FiscalPeriodStatus = 'Open' | 'Closed'

export interface FiscalPeriod {
  id: string
  periodName: string
  startDate: string
  endDate: string
  status: FiscalPeriodStatus
  createdAt: string
}

// OpenFiscalPeriodCommand(PeriodName, StartDate, EndDate) — this is the "create" action;
// there is no separate "create" vs "open" — POST / literally opens a new period.
export interface OpenFiscalPeriodRequest {
  periodName: string
  startDate: string
  endDate: string
}

// ClosingPolicyDto — Monthly: every period writes its closing entry; YearEnd: only the last period of the fiscal year does.
export type ClosingMode = 'Monthly' | 'YearEnd'

export interface ClosingPolicy {
  closingMode: ClosingMode
  fiscalYearEndMonth: number
  /** The month-end job accrues the loan interest of the tenant to the end of the previous month. Omitted on save = unchanged. */
  autoAccrueLoanInterest?: boolean
}

export const fiscalPeriodsApi = {
  closingPolicy: () => apiClient.get<ClosingPolicy>(`${ACC_BASE}/fiscal-periods/closing-policy`).then((r) => r.data),

  updateClosingPolicy: (body: ClosingPolicy) =>
    apiClient.put<ClosingPolicy>(`${ACC_BASE}/fiscal-periods/closing-policy`, body).then((r) => r.data),

  // ListFiscalPeriodsQuery takes no filters; controller returns a plain List<FiscalPeriodDto> (no pagination wrapper).
  list: () => apiClient.get<FiscalPeriod[]>(`${ACC_BASE}/fiscal-periods`).then((r) => r.data),

  get: (id: string) =>
    apiClient.get<FiscalPeriod>(`${ACC_BASE}/fiscal-periods/${id}`).then((r) => r.data),

  open: (body: OpenFiscalPeriodRequest) =>
    apiClient.post<FiscalPeriod>(`${ACC_BASE}/fiscal-periods`, body).then((r) => r.data),

  // Checks the books, writes the period-end transfer (revenue / expenses -> income summary -> retained earnings) and locks the period.
  close: (id: string) =>
    apiClient.post<FiscalPeriod>(`${ACC_BASE}/fiscal-periods/${id}/close`).then((r) => r.data),

  // Only the latest closed period; its closing entry is voided.
  reopen: (id: string, reason: string) =>
    apiClient.post<FiscalPeriod>(`${ACC_BASE}/fiscal-periods/${id}/reopen`, { reason }).then((r) => r.data),
}
