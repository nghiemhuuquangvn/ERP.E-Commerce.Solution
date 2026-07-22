import { apiClient } from '../client'

// SaleOrderService's SettingsController is mounted under the Gateway's `/so/api/...` route.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const SETTINGS_BASE = `${GATEWAY_ROOT}/so/api/v1/settings/order`

export interface SystemOrderSettings {
  customOrderNumberFormatAllowed: boolean
}

export type UpdateSystemOrderSettingsRequest = SystemOrderSettings

export interface TenantOrderSettings {
  tenantId: string
  orderNumberPrefix: string
  autoCancelUnpaidAfterHours: number
  allowEditAfterSubmit: boolean
}

export type UpdateTenantOrderSettingsRequest = Omit<TenantOrderSettings, 'tenantId'>

export const orderSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemOrderSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemOrderSettingsRequest) =>
    apiClient.put<SystemOrderSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantOrderSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantOrderSettingsRequest) =>
    apiClient.put<TenantOrderSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body).then((r) => r.data),
}
