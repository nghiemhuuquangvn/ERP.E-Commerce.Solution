import { gatewayClient as apiClient } from '../client'

// SaleOrderService's SettingsController is mounted under the Gateway's `/so/api/...` route.
const SETTINGS_BASE = `/so/api/v1/settings/cart-checkout`

export interface SystemCartCheckoutSettings {
  guestCheckoutAllowed: boolean
}

export type UpdateSystemCartCheckoutSettingsRequest = SystemCartCheckoutSettings

export interface TenantCartCheckoutSettings {
  tenantId: string
  cartHoldMinutes: number
  requireRegistrationAtCheckout: boolean
  checkoutSteps: number
}

export type UpdateTenantCartCheckoutSettingsRequest = Omit<TenantCartCheckoutSettings, 'tenantId'>

export const cartCheckoutSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemCartCheckoutSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemCartCheckoutSettingsRequest) =>
    apiClient.put<SystemCartCheckoutSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantCartCheckoutSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantCartCheckoutSettingsRequest) =>
    apiClient
      .put<TenantCartCheckoutSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body)
      .then((r) => r.data),
}
