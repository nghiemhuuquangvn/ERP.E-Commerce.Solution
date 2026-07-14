import { apiClient } from '../client'

// ProductService's SettingsController is mounted under the Gateway's `/prd/api/...`
// route, mirroring settings.api.ts's re-rooting for IdentityService's `/id/api/...`.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const SETTINGS_BASE = `${GATEWAY_ROOT}/prd/api/settings/store-product`

export interface SystemStoreProductSettings {
  priceDisplayIncludesTaxByDefault: boolean
  productReviewsEnabled: boolean
}

export type UpdateSystemStoreProductSettingsRequest = SystemStoreProductSettings

export interface TenantStoreProductSettings {
  tenantId: string
  storeName: string
  storeDescription: string | null
  logoUrl: string | null
  showOutOfStockProducts: boolean
}

export type UpdateTenantStoreProductSettingsRequest = Omit<TenantStoreProductSettings, 'tenantId'>

export const storeProductSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemStoreProductSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemStoreProductSettingsRequest) =>
    apiClient.put<SystemStoreProductSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantStoreProductSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantStoreProductSettingsRequest) =>
    apiClient
      .put<TenantStoreProductSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
