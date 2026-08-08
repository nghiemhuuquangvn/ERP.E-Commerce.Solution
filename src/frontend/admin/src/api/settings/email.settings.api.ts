import { gatewayClient as apiClient } from '../client'

// NotificationService's SettingsController is mounted under the Gateway's `/ntf/api/...` route.
const SETTINGS_BASE = `/ntf/api/v1/settings/email`

export interface SystemEmailSettings {
  customSmtpAllowed: boolean
}

export type UpdateSystemEmailSettingsRequest = SystemEmailSettings

// hasSmtpPassword reports whether a password is stored — the encrypted value itself
// is never returned by the backend.
export interface TenantEmailSettings {
  tenantId: string
  smtpHost: string | null
  smtpPort: number
  smtpUsername: string | null
  hasSmtpPassword: boolean
  fromAddress: string | null
  fromName: string | null
}

// smtpPassword is plaintext, sent only when the admin wants to change it —
// leave empty/undefined to keep the currently stored password.
export interface UpdateTenantEmailSettingsRequest {
  smtpHost: string | null
  smtpPort: number
  smtpUsername: string | null
  smtpPassword?: string
  fromAddress: string | null
  fromName: string | null
}

export const emailSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemEmailSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemEmailSettingsRequest) =>
    apiClient.put<SystemEmailSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantEmailSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantEmailSettingsRequest) =>
    apiClient
      .put<TenantEmailSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
