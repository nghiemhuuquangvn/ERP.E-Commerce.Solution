import { apiClient } from '../client'

// IdentityService's GeneralSettingsController is mounted under the Gateway's `/id/api/...` route.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const SETTINGS_BASE = `${GATEWAY_ROOT}/id/api/v1/settings/general`

export interface SystemGeneralSettings {
  maintenanceModeEnabled: boolean
  defaultLanguageCode: string
}

export type UpdateSystemGeneralSettingsRequest = SystemGeneralSettings

export interface TenantGeneralSettings {
  tenantId: string
  siteName: string
  defaultLanguageCode: string
  timeZone: string
  dateFormat: string
  maintenanceModeEnabled: boolean
}

export type UpdateTenantGeneralSettingsRequest = Omit<TenantGeneralSettings, 'tenantId'>

export const generalSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemGeneralSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemGeneralSettingsRequest) =>
    apiClient.put<SystemGeneralSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantGeneralSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantGeneralSettingsRequest) =>
    apiClient
      .put<TenantGeneralSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
