import { apiClient, PaginatedResponse } from './client'

export interface InventoryItem {
  id: string
  productId: string
  productName: string
  sku: string
  quantityOnHand: number
  quantityReserved: number
  quantityAvailable: number
  warehouseLocation: string
  lastMovementAt: string
}

export interface StockMovement {
  id: string
  productId: string
  productName: string
  type: 'Receipt' | 'Issue' | 'Transfer' | 'Adjustment'
  quantity: number
  referenceId: string
  occurredAt: string
}

export interface ListInventoryParams {
  page?: number
  pageSize?: number
  search?: string
  lowStock?: boolean
}

export const inventoryApi = {
  list: (params: ListInventoryParams) =>
    apiClient.get<PaginatedResponse<InventoryItem>>('/v1/inventory', { params }).then((r) => r.data),

  movements: (productId: string) =>
    apiClient.get<StockMovement[]>(`/v1/inventory/${productId}/movements`).then((r) => r.data),

  adjust: (productId: string, quantity: number, reason: string) =>
    apiClient.post('/v1/inventory/adjust', { productId, quantity, reason }),
}
