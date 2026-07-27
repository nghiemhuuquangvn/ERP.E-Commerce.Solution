import { apiClient, WH_BASE } from './whClient'

export type StocktakeStatus = 'InProgress' | 'Completed' | 'Cancelled' | 'PendingApproval'

export interface StocktakeLine {
  productId: string
  systemQuantity: number
  countedQuantity: number | null
  variance: number
  varianceReason: string | null
}

export interface Stocktake {
  stocktakeId: string
  stocktakeNumber: string
  locationId: string | null
  status: StocktakeStatus
  lines: StocktakeLine[]
  notes: string | null
  startedBy: string
  startedAt: string
  completedAt: string | null
}

export const stocktakesApi = {
  list: () => apiClient.get<Stocktake[]>(`${WH_BASE}/stocktakes`).then((r) => r.data),

  get: (id: string) => apiClient.get<Stocktake>(`${WH_BASE}/stocktakes/${id}`).then((r) => r.data),

  start: (body: { locationId?: string; productIds?: string[]; startedBy: string; notes?: string }) =>
    apiClient.post<Stocktake>(`${WH_BASE}/stocktakes`, body).then((r) => r.data),

  recordCount: (id: string, productId: string, countedQuantity: number, varianceReason?: string) =>
    apiClient
      .post<Stocktake>(`${WH_BASE}/stocktakes/${id}/counts`, { productId, countedQuantity, varianceReason })
      .then((r) => r.data),

  // Locks counting and computes variance — does NOT post to Inventory yet.
  complete: (id: string) => apiClient.post<Stocktake>(`${WH_BASE}/stocktakes/${id}/complete`).then((r) => r.data),

  // The only step that actually posts variance to Inventory.
  approve: (id: string) => apiClient.post<Stocktake>(`${WH_BASE}/stocktakes/${id}/approve`).then((r) => r.data),

  cancel: (id: string) => apiClient.post<Stocktake>(`${WH_BASE}/stocktakes/${id}/cancel`).then((r) => r.data),
}
