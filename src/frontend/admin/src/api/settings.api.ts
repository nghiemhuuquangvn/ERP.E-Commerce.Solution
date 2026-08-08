import { gatewayClient as apiClient } from './client'

// IdentityService's SettingsController is mounted under the Gateway's `/id/api/...`
// route (see AuthController/UsersController also living under IdentityService),
// not under apiClient's default `/api` baseURL — mirrors how
// `src/api/masterdata/mstClient.ts` re-roots MST calls at `/mst/api/...`.
const SETTINGS_BASE = `/id/api/v1/settings`

export interface SystemAuthSettings {
  googleEnabled: boolean
  microsoftEnabled: boolean
  facebookEnabled: boolean
  twitterEnabled: boolean
  twoFactorEnabled: boolean
  minPasswordLength: number
  requireUppercase: boolean
  requireLowercase: boolean
  requireDigit: boolean
  requireSymbol: boolean
  maxLoginAttempts: number
  lockoutDurationMinutes: number
  sessionTimeoutMinutes: number
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
  minPasswordLength: number
  requireUppercase: boolean
  requireLowercase: boolean
  requireDigit: boolean
  requireSymbol: boolean
  maxLoginAttempts: number
  lockoutDurationMinutes: number
  sessionTimeoutMinutes: number
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
