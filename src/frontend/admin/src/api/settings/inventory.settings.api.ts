import { apiClient } from '../client'

// WarehouseService's SettingsController is mounted under the Gateway's `/wh/api/...` route.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const SETTINGS_BASE = `${GATEWAY_ROOT}/wh/api/v1/settings/inventory`

export interface SystemInventorySettings {
  negativeStockAllowed: boolean
}

export type UpdateSystemInventorySettingsRequest = SystemInventorySettings

export interface TenantInventorySettings {
  tenantId: string
  lowStockThreshold: number
  backorderAllowed: boolean
  defaultHoldDays: number
}

export type UpdateTenantInventorySettingsRequest = Omit<TenantInventorySettings, 'tenantId'>

export const inventorySettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemInventorySettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemInventorySettingsRequest) =>
    apiClient.put<SystemInventorySettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantInventorySettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantInventorySettingsRequest) =>
    apiClient
      .put<TenantInventorySettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
