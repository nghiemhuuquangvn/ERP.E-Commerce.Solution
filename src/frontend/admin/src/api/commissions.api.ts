import { apiClient, SO_BASE } from './soClient'

export interface CommissionPlan {
  id: string
  name: string
  rateType: 'Percent'
  rateValue: number
  isActive: boolean
}

export interface CommissionRecord {
  id: string
  planId: string
  salesRepId: string
  orderId: string
  amount: number
  status: 'Pending' | 'Approved' | 'Paid'
  calculatedAt: string
}

export const commissionPlansApi = {
  list: () => apiClient.get<CommissionPlan[]>(`${SO_BASE}/commission-plans`).then((r) => r.data),

  create: (body: { name: string; rateValue: number }) =>
    apiClient.post<CommissionPlan>(`${SO_BASE}/commission-plans`, body).then((r) => r.data),

  deactivate: (id: string) => apiClient.post(`${SO_BASE}/commission-plans/${id}/deactivate`),
}

export const commissionsApi = {
  list: (params: { salesRepId?: string; from?: string; to?: string }) =>
    apiClient.get<CommissionRecord[]>(`${SO_BASE}/commissions`, { params }).then((r) => r.data),
}
