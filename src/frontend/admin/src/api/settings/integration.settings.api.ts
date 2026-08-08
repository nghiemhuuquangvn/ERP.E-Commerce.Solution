import { gatewayClient as apiClient } from '../client'

// IdentityService's IntegrationSettingsController is mounted under the Gateway's `/id/api/...` route.
const SETTINGS_BASE = `/id/api/v1/settings/integration`

export interface SystemIntegrationSettings {
  externalIntegrationsAllowed: boolean
}

export type UpdateSystemIntegrationSettingsRequest = SystemIntegrationSettings

// hasExternalApiKey reports whether a key is stored — the encrypted value itself
// is never returned by the backend.
export interface TenantIntegrationSettings {
  tenantId: string
  externalApiBaseUrl: string | null
  hasExternalApiKey: boolean
  syncEnabled: boolean
}

// externalApiKey is plaintext, sent only when the admin wants to change it —
// leave empty/undefined to keep the currently stored key.
export interface UpdateTenantIntegrationSettingsRequest {
  externalApiBaseUrl: string | null
  externalApiKey?: string
  syncEnabled: boolean
}

export const integrationSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemIntegrationSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemIntegrationSettingsRequest) =>
    apiClient.put<SystemIntegrationSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient
      .get<TenantIntegrationSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`)
      .then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantIntegrationSettingsRequest) =>
    apiClient
      .put<TenantIntegrationSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
