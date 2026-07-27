import { apiClient, WH_BASE } from './whClient'

export interface WarehouseLocation {
  locationId: string
  warehouseId: string | null
  locationCode: string
  name: string | null
  zone: string | null
  aisle: string
  bin: string
  shelf: string
  capacity: number
  currentOccupancy: number
  isActive: boolean
  createdAt: string
}

export interface CreateLocationRequest {
  warehouseId?: string | null
  locationCode: string
  name?: string
  zone?: string
  aisle: string
  bin: string
  shelf: string
  capacity: number
}

export const locationsApi = {
  list: (isActive?: boolean) =>
    apiClient.get<WarehouseLocation[]>(`${WH_BASE}/locations`, { params: { isActive } }).then((r) => r.data),

  get: (locationId: string) =>
    apiClient.get<WarehouseLocation>(`${WH_BASE}/locations/${locationId}`).then((r) => r.data),

  create: (body: CreateLocationRequest) =>
    apiClient.post<WarehouseLocation>(`${WH_BASE}/locations`, body).then((r) => r.data),

  update: (locationId: string, body: Omit<CreateLocationRequest, 'locationCode'>) =>
    apiClient.put<WarehouseLocation>(`${WH_BASE}/locations/${locationId}`, body).then((r) => r.data),

  activate: (locationId: string) => apiClient.post(`${WH_BASE}/locations/${locationId}/activate`),

  deactivate: (locationId: string) => apiClient.post(`${WH_BASE}/locations/${locationId}/deactivate`),
}
