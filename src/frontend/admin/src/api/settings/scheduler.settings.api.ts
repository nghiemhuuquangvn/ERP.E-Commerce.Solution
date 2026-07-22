import { apiClient } from '../client'

// IdentityService's SchedulerSettingsController is mounted under the Gateway's `/id/api/...` route.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const SETTINGS_BASE = `${GATEWAY_ROOT}/id/api/v1/settings/scheduler`

export interface SystemSchedulerSettings {
  backgroundJobsEnabled: boolean
}

export type UpdateSystemSchedulerSettingsRequest = SystemSchedulerSettings

export interface TenantSchedulerSettings {
  tenantId: string
  outboxPollingIntervalSeconds: number
  maxRetryAttempts: number
}

export type UpdateTenantSchedulerSettingsRequest = Omit<TenantSchedulerSettings, 'tenantId'>

export const schedulerSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemSchedulerSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemSchedulerSettingsRequest) =>
    apiClient.put<SystemSchedulerSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantSchedulerSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantSchedulerSettingsRequest) =>
    apiClient
      .put<TenantSchedulerSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
