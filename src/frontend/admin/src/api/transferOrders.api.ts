import { apiClient, WH_BASE } from './whClient'

export type TransferOrderStatus = 'Draft' | 'InTransit' | 'Completed' | 'Cancelled'

export interface TransferOrderLine {
  productId: string
  quantity: number
}

export interface TransferOrder {
  transferOrderId: string
  transferNumber: string
  fromLocationId: string
  toLocationId: string
  status: TransferOrderStatus
  lines: TransferOrderLine[]
  notes: string | null
  createdAt: string
  shippedAt: string | null
  completedAt: string | null
}

export interface CreateTransferOrderRequest {
  fromLocationId: string
  toLocationId: string
  lines: TransferOrderLine[]
  notes?: string
}

export const transferOrdersApi = {
  list: () => apiClient.get<TransferOrder[]>(`${WH_BASE}/transfer-orders`).then((r) => r.data),

  get: (id: string) => apiClient.get<TransferOrder>(`${WH_BASE}/transfer-orders/${id}`).then((r) => r.data),

  create: (body: CreateTransferOrderRequest) =>
    apiClient.post<TransferOrder>(`${WH_BASE}/transfer-orders`, body).then((r) => r.data),

  ship: (id: string) => apiClient.post<TransferOrder>(`${WH_BASE}/transfer-orders/${id}/ship`).then((r) => r.data),

  complete: (id: string) =>
    apiClient.post<TransferOrder>(`${WH_BASE}/transfer-orders/${id}/complete`).then((r) => r.data),

  cancel: (id: string) =>
    apiClient.post<TransferOrder>(`${WH_BASE}/transfer-orders/${id}/cancel`).then((r) => r.data),
}
