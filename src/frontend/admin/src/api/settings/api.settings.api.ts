import { apiClient } from '../client'

// IdentityService's ApiSettingsController is mounted under the Gateway's `/id/api/...` route.
// Config only — distinct from the existing ApiKey/ApiKeysController feature.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const SETTINGS_BASE = `${GATEWAY_ROOT}/id/api/v1/settings/api`

export interface SystemApiSettings {
  apiKeysAllowed: boolean
  defaultRateLimitPerMinute: number
}

export type UpdateSystemApiSettingsRequest = SystemApiSettings

export interface TenantApiSettings {
  tenantId: string
  rateLimitPerMinute: number
  maxActiveApiKeys: number
}

export type UpdateTenantApiSettingsRequest = Omit<TenantApiSettings, 'tenantId'>

export const apiSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemApiSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemApiSettingsRequest) =>
    apiClient.put<SystemApiSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantApiSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantApiSettingsRequest) =>
    apiClient.put<TenantApiSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body).then((r) => r.data),
}
