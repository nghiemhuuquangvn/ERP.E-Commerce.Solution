import { apiClient, MST_BASE, MstPagedResult } from './mstClient'

export interface PaymentTerm {
  id: string
  code: string
  name: string
  netDays: number
  depositPercent: number | null
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface ListPaymentTermsParams {
  page?: number
  pageSize?: number
}

// CreatePaymentTermCommand(Code, Name, NetDays, DepositPercent = null)
export interface CreatePaymentTermRequest {
  code: string
  name: string
  netDays: number
  depositPercent?: number | null
}

// UpdatePaymentTermRequest(Name, NetDays, DepositPercent) — no Code on update
export interface UpdatePaymentTermRequest {
  name: string
  netDays: number
  depositPercent?: number | null
}

export const paymentTermsApi = {
  list: (params: ListPaymentTermsParams) =>
    apiClient
      .get<MstPagedResult<PaymentTerm>>(`${MST_BASE}/payment-terms`, { params })
      .then((r) => r.data),

  get: (id: string) =>
    apiClient.get<PaymentTerm>(`${MST_BASE}/payment-terms/${id}`).then((r) => r.data),

  create: (body: CreatePaymentTermRequest) =>
    apiClient.post<PaymentTerm>(`${MST_BASE}/payment-terms`, body).then((r) => r.data),

  update: (id: string, body: UpdatePaymentTermRequest) =>
    apiClient.put<PaymentTerm>(`${MST_BASE}/payment-terms/${id}`, body).then((r) => r.data),

  activate: (id: string) =>
    apiClient.post<PaymentTerm>(`${MST_BASE}/payment-terms/${id}/activate`).then((r) => r.data),

  deactivate: (id: string) =>
    apiClient.post<PaymentTerm>(`${MST_BASE}/payment-terms/${id}/deactivate`).then((r) => r.data),
}
