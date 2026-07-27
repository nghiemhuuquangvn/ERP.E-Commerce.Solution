import { apiClient, PO_BASE } from './poClient'

export type PurchaseRequestStatus = 'Suggested' | 'Dismissed' | 'Converted'

export interface PurchaseRequest {
  id: string
  productId: string
  productName: string
  currentQuantity: number
  reorderPoint: number
  suggestedQuantity: number
  status: PurchaseRequestStatus
  createdAt: string
  resolvedAt: string | null
}

const BASE = `${PO_BASE}/purchase-requests`

export const purchaseRequestsApi = {
  list: (status?: PurchaseRequestStatus) =>
    apiClient.get<PurchaseRequest[]>(BASE, { params: status ? { status } : undefined }).then((r) => r.data),

  dismiss: (id: string) => apiClient.post<PurchaseRequest>(`${BASE}/${id}/dismiss`).then((r) => r.data),

  convert: (id: string) => apiClient.post<PurchaseRequest>(`${BASE}/${id}/convert`).then((r) => r.data),
}
