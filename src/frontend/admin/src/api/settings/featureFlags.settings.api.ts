import { apiClient } from '../client'

// IdentityService's FeatureFlagsController is mounted under the Gateway's `/id/api/...` route.
// Dynamic key-value shape — deliberately different from every other Settings category.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const SETTINGS_BASE = `${GATEWAY_ROOT}/id/api/settings/feature-flags`

export interface SystemFeatureFlag {
  key: string
  isEnabled: boolean
  description: string | null
}

export interface UpsertSystemFeatureFlagRequest {
  isEnabled: boolean
  description: string | null
}

// isOverridden reports whether the tenant has its own override for this key, vs.
// inheriting the System value.
export interface TenantFeatureFlag {
  key: string
  isEnabled: boolean
  isOverridden: boolean
  description: string | null
}

export interface SetTenantFeatureFlagOverrideRequest {
  isEnabled: boolean
}

export const featureFlagsApi = {
  getSystemFlags: () =>
    apiClient.get<SystemFeatureFlag[]>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  upsertSystemFlag: (key: string, body: UpsertSystemFeatureFlagRequest) =>
    apiClient
      .put<SystemFeatureFlag>(`${SETTINGS_BASE}/system/${encodeURIComponent(key)}`, body)
      .then((r) => r.data),

  getTenantFlags: (tenantId: string) =>
    apiClient.get<TenantFeatureFlag[]>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  setTenantFlagOverride: (tenantId: string, key: string, body: SetTenantFeatureFlagOverrideRequest) =>
    apiClient
      .put<TenantFeatureFlag>(`${SETTINGS_BASE}/tenant/${tenantId}/${encodeURIComponent(key)}`, body)
      .then((r) => r.data),
}
