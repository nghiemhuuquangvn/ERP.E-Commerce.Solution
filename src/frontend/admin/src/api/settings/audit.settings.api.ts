import { gatewayClient as apiClient } from '../client'

// IdentityService's AuditSettingsController is mounted under the Gateway's `/id/api/...` route.
const SETTINGS_BASE = `/id/api/v1/settings/audit`

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
