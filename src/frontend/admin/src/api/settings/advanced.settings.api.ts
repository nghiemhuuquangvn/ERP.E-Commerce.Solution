import { apiClient } from '../client'

// IdentityService's AdvancedSettingsController is mounted under the Gateway's `/id/api/...` route.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const SETTINGS_BASE = `${GATEWAY_ROOT}/id/api/v1/settings/advanced`

export interface SystemAdvancedSettings {
  debugModeAllowed: boolean
}

export type UpdateSystemAdvancedSettingsRequest = SystemAdvancedSettings

export interface TenantAdvancedSettings {
  tenantId: string
  debugModeEnabled: boolean
  customCssUrl: string | null
  dataExportEnabled: boolean
}

export type UpdateTenantAdvancedSettingsRequest = Omit<TenantAdvancedSettings, 'tenantId'>

export const advancedSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemAdvancedSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemAdvancedSettingsRequest) =>
    apiClient.put<SystemAdvancedSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient
      .get<TenantAdvancedSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`)
      .then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantAdvancedSettingsRequest) =>
    apiClient
      .put<TenantAdvancedSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
