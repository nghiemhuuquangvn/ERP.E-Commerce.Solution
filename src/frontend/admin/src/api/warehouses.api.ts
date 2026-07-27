import { apiClient, WH_BASE } from './whClient'

export interface Warehouse {
  id: string
  code: string
  name: string
  address: string | null
  isActive: boolean
}

export const warehousesApi = {
  list: () => apiClient.get<Warehouse[]>(`${WH_BASE}/warehouses`).then((r) => r.data),

  get: (id: string) => apiClient.get<Warehouse>(`${WH_BASE}/warehouses/${id}`).then((r) => r.data),

  create: (body: { code: string; name: string; address?: string }) =>
    apiClient.post<Warehouse>(`${WH_BASE}/warehouses`, body).then((r) => r.data),

  update: (id: string, body: { name: string; address?: string }) =>
    apiClient.put<Warehouse>(`${WH_BASE}/warehouses/${id}`, body).then((r) => r.data),

  activate: (id: string) => apiClient.post(`${WH_BASE}/warehouses/${id}/activate`),

  deactivate: (id: string) => apiClient.post(`${WH_BASE}/warehouses/${id}/deactivate`),
}
