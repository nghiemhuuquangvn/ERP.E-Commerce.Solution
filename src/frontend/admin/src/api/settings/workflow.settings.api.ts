import { apiClient } from '../client'

// IdentityService's WorkflowSettingsController is mounted under the Gateway's `/id/api/...` route.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const SETTINGS_BASE = `${GATEWAY_ROOT}/id/api/v1/settings/workflow`

export interface SystemWorkflowSettings {
  multiLevelApprovalAvailable: boolean
}

export type UpdateSystemWorkflowSettingsRequest = SystemWorkflowSettings

export interface TenantWorkflowSettings {
  tenantId: string
  multiLevelApprovalEnabled: boolean
  defaultApprovalLevels: number
  autoApproveThresholdAmount: number | null
}

export type UpdateTenantWorkflowSettingsRequest = Omit<TenantWorkflowSettings, 'tenantId'>

export const workflowSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemWorkflowSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemWorkflowSettingsRequest) =>
    apiClient.put<SystemWorkflowSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantWorkflowSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantWorkflowSettingsRequest) =>
    apiClient
      .put<TenantWorkflowSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
