import { gatewayClient as apiClient } from '../client'

// NotificationService's SettingsController is mounted under the Gateway's `/ntf/api/...` route.
const SETTINGS_BASE = `/ntf/api/v1/settings/sms`

export interface SystemSmsSettings {
  customSmsGatewayAllowed: boolean
}

export type UpdateSystemSmsSettingsRequest = SystemSmsSettings

// hasApiKey reports whether an API key is stored — the encrypted value itself
// is never returned by the backend.
export interface TenantSmsSettings {
  tenantId: string
  providerName: string | null
  hasApiKey: boolean
  fromNumber: string | null
}

// apiKey is plaintext, sent only when the admin wants to change it —
// leave empty/undefined to keep the currently stored key.
export interface UpdateTenantSmsSettingsRequest {
  providerName: string | null
  apiKey?: string
  fromNumber: string | null
}

export const smsSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemSmsSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemSmsSettingsRequest) =>
    apiClient.put<SystemSmsSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantSmsSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantSmsSettingsRequest) =>
    apiClient.put<TenantSmsSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body).then((r) => r.data),
}
