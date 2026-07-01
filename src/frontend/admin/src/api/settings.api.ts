import { apiClient } from './client'

// IdentityService's SettingsController is mounted under the Gateway's `/id/api/...`
// route (see AuthController/UsersController also living under IdentityService),
// not under apiClient's default `/api` baseURL — mirrors how
// `src/api/masterdata/mstClient.ts` re-roots MST calls at `/mst/api/...`.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const SETTINGS_BASE = `${GATEWAY_ROOT}/id/api/settings`

export interface SystemAuthSettings {
  googleEnabled: boolean
  microsoftEnabled: boolean
  facebookEnabled: boolean
  twitterEnabled: boolean
  twoFactorEnabled: boolean
}

export type UpdateSystemAuthSettingsRequest = SystemAuthSettings

export interface TenantAuthSettings {
  tenantId: string
  googleEnabled: boolean
  microsoftEnabled: boolean
  facebookEnabled: boolean
  twitterEnabled: boolean
  twoFactorEnabled: boolean
  twoFactorRequired: boolean
}

export type UpdateTenantAuthSettingsRequest = Omit<TenantAuthSettings, 'tenantId'>

export const settingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemAuthSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemAuthSettingsRequest) =>
    apiClient.put<SystemAuthSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantAuthSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantAuthSettingsRequest) =>
    apiClient
      .put<TenantAuthSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
