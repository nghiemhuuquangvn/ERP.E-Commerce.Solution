import { gatewayClient as apiClient } from '../client'

// ShippingService's SettingsController is mounted under the Gateway's `/shp/api/...` route.
const SETTINGS_BASE = `/shp/api/v1/settings/shipping`

export interface SystemShippingSettings {
  customCarriersAllowed: boolean
}

export type UpdateSystemShippingSettingsRequest = SystemShippingSettings

export interface TenantShippingSettings {
  tenantId: string
  defaultShippingFee: number
  freeShippingThreshold: number
}

export type UpdateTenantShippingSettingsRequest = Omit<TenantShippingSettings, 'tenantId'>

export const shippingSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemShippingSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemShippingSettingsRequest) =>
    apiClient.put<SystemShippingSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantShippingSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantShippingSettingsRequest) =>
    apiClient
      .put<TenantShippingSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
