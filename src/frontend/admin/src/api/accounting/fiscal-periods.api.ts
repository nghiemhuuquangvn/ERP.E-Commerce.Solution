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

export const fiscalPeriodsApi = {
  // ListFiscalPeriodsQuery takes no filters; controller returns a plain List<FiscalPeriodDto> (no pagination wrapper).
  list: () => apiClient.get<FiscalPeriod[]>(`${ACC_BASE}/fiscal-periods`).then((r) => r.data),

  get: (id: string) =>
    apiClient.get<FiscalPeriod>(`${ACC_BASE}/fiscal-periods/${id}`).then((r) => r.data),

  open: (body: OpenFiscalPeriodRequest) =>
    apiClient.post<FiscalPeriod>(`${ACC_BASE}/fiscal-periods`, body).then((r) => r.data),

  close: (id: string) =>
    apiClient.post<FiscalPeriod>(`${ACC_BASE}/fiscal-periods/${id}/close`).then((r) => r.data),
}
