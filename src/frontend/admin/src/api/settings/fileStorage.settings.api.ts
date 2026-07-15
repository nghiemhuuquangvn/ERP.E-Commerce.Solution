import { apiClient } from '../client'

// IdentityService's FileStorageSettingsController is mounted under the Gateway's `/id/api/...` route.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const SETTINGS_BASE = `${GATEWAY_ROOT}/id/api/settings/file-storage`

export interface SystemFileStorageSettings {
  customStorageProviderAllowed: boolean
  defaultMaxUploadSizeMb: number
}

export type UpdateSystemFileStorageSettingsRequest = SystemFileStorageSettings

export interface TenantFileStorageSettings {
  tenantId: string
  provider: string
  maxUploadSizeMb: number
  cdnBaseUrl: string | null
}

export type UpdateTenantFileStorageSettingsRequest = Omit<TenantFileStorageSettings, 'tenantId'>

export const fileStorageSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemFileStorageSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemFileStorageSettingsRequest) =>
    apiClient.put<SystemFileStorageSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient
      .get<TenantFileStorageSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`)
      .then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantFileStorageSettingsRequest) =>
    apiClient
      .put<TenantFileStorageSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
