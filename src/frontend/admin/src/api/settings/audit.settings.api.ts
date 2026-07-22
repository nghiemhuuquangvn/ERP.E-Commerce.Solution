import { apiClient } from '../client'

// IdentityService's AuditSettingsController is mounted under the Gateway's `/id/api/...` route.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const SETTINGS_BASE = `${GATEWAY_ROOT}/id/api/v1/settings/audit`

export interface SystemAuditSettings {
  auditLoggingEnabled: boolean
}

export type UpdateSystemAuditSettingsRequest = SystemAuditSettings

export interface TenantAuditSettings {
  tenantId: string
  retentionDays: number
  logSuccessfulLogins: boolean
  logFailedLogins: boolean
  logPermissionChanges: boolean
}

export type UpdateTenantAuditSettingsRequest = Omit<TenantAuditSettings, 'tenantId'>

export const auditSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemAuditSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemAuditSettingsRequest) =>
    apiClient.put<SystemAuditSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantAuditSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantAuditSettingsRequest) =>
    apiClient
      .put<TenantAuditSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
