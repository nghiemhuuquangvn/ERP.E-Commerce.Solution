import { apiClient } from '../client'

// IdentityService's LocalizationSettingsController is mounted under the Gateway's `/id/api/...` route.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const SETTINGS_BASE = `${GATEWAY_ROOT}/id/api/settings/localization`

export interface SystemLocalizationSettings {
  multiLanguageAllowed: boolean
  defaultLocale: string
}

export type UpdateSystemLocalizationSettingsRequest = SystemLocalizationSettings

export interface TenantLocalizationSettings {
  tenantId: string
  defaultLocale: string
  supportedLocales: string
  numberFormatLocale: string
  rtlEnabled: boolean
}

export type UpdateTenantLocalizationSettingsRequest = Omit<TenantLocalizationSettings, 'tenantId'>

export const localizationSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemLocalizationSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemLocalizationSettingsRequest) =>
    apiClient.put<SystemLocalizationSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient
      .get<TenantLocalizationSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`)
      .then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantLocalizationSettingsRequest) =>
    apiClient
      .put<TenantLocalizationSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
