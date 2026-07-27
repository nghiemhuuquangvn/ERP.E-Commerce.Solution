import { apiClient, WH_BASE } from './whClient'

export type AdjustmentReason = 'Damaged' | 'Lost' | 'Correction' | 'Other'
export type StockAdjustmentStatus = 'Pending' | 'Approved' | 'Rejected'

export interface StockAdjustment {
  id: string
  productId: string
  locationId: string | null
  quantityDelta: number
  reason: AdjustmentReason
  notes: string | null
  status: StockAdjustmentStatus
  requestedBy: string
  requestedAt: string
  approvedBy: string | null
  approvedAt: string | null
  rejectionReason: string | null
}

export interface RequestAdjustmentRequest {
  productId: string
  locationId?: string
  quantityDelta: number
  reason: AdjustmentReason
  notes?: string
}

// Replaces the removed direct-write POST /inventory/{productId}/adjust — an adjustment
// must always be approved before it affects Inventory.
export const stockAdjustmentsApi = {
  list: (status?: StockAdjustmentStatus) =>
    apiClient.get<StockAdjustment[]>(`${WH_BASE}/stock-adjustments`, { params: { status } }).then((r) => r.data),

  request: (body: RequestAdjustmentRequest) =>
    apiClient.post<StockAdjustment>(`${WH_BASE}/stock-adjustments`, body).then((r) => r.data),

  approve: (id: string) => apiClient.post(`${WH_BASE}/stock-adjustments/${id}/approve`).then((r) => r.data),

  reject: (id: string, reason: string) =>
    apiClient.post(`${WH_BASE}/stock-adjustments/${id}/reject`, { reason }).then((r) => r.data),
}
