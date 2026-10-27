import { gatewayClient as apiClient } from '../client'

// ShippingService's SettingsController is mounted under the Gateway's `/shp/api/...` route.
const SETTINGS_BASE = `/shp/api/v1/settings/shipping`

export interface SystemShippingSettings {
  customCarriersAllowed: boolean
  /** Platform switches: which carrier integrations exist at all. */
  ghtkAvailable: boolean
  vtpAvailable: boolean
  ghnAvailable: boolean
  jntAvailable: boolean
}

export type UpdateSystemShippingSettingsRequest = SystemShippingSettings

export interface TenantShippingSettings {
  tenantId: string
  defaultShippingFee: number
  freeShippingThreshold: number
  /** Where the carriers collect the parcels. */
  senderName?: string | null
  senderPhone?: string | null
  senderAddress?: string | null
  senderWard?: string | null
  senderDistrict?: string | null
  senderProvince?: string | null
  /** Weight assumed for a product with no weight, in grams. */
  defaultItemWeightGrams: number
}

export type UpdateTenantShippingSettingsRequest = Omit<TenantShippingSettings, 'tenantId'>

export type CarrierCode = 'GHTK' | 'VTP' | 'GHN' | 'JNT'

/** The shop's account at one carrier. Secrets are never returned, only whether they are set. */
export interface CarrierAccount {
  code: CarrierCode
  name: string
  /** false = a placeholder (J&T): it cannot be set up. */
  integrated: boolean
  platformAvailable: boolean
  enabled: boolean
  sandbox: boolean
  publicId?: string | null
  username?: string | null
  hasSecret: boolean
  hasWebhookSecret: boolean
  complete: boolean
}

export interface UpdateCarrierAccountRequest {
  enabled: boolean
  sandbox: boolean
  publicId?: string
  username?: string
  /** Blank/undefined keeps the stored secret. */
  secret?: string
  webhookSecret?: string
}

export const shippingSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemShippingSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemShippingSettingsRequest) =>
    apiClient.put<SystemShippingSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantShippingSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantShippingSettingsRequest) =>
    apiClient
      .put<TenantShippingSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),

  getCarrierAccounts: (tenantId: string) =>
    apiClient.get<CarrierAccount[]>(`${SETTINGS_BASE}/tenant/${tenantId}/carriers`).then((r) => r.data),

  updateCarrierAccount: (tenantId: string, code: CarrierCode, body: UpdateCarrierAccountRequest) =>
    apiClient.put<CarrierAccount>(`${SETTINGS_BASE}/tenant/${tenantId}/carriers/${code}`, body).then((r) => r.data),
}
