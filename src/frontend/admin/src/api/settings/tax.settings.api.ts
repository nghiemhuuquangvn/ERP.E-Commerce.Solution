import { gatewayClient as apiClient } from '../client'

// AccountingService's SettingsController is mounted under the Gateway's `/acc/api/...` route.
const SETTINGS_BASE = `/acc/api/v1/settings/tax`

export interface SystemTaxSettings {
  regionalTaxConfigAllowed: boolean
}

export type UpdateSystemTaxSettingsRequest = SystemTaxSettings

export interface TenantTaxSettings {
  tenantId: string
  companyTaxCode: string | null
  defaultTaxRatePercent: number
  pricesIncludeTax: boolean
  /** Code of the e-invoice provider ("sandbox" …); empty = e-invoices are off. */
  eInvoiceProvider?: string | null
  eInvoiceSeries?: string | null
  /** Issue an e-invoice (retail customer) with every invoice created from an order. */
  autoIssueEInvoice?: boolean
  /** The currency of the books (3-letter code, default VND); ROADMAP P1-4. */
  baseCurrencyCode?: string
}

export type UpdateTenantTaxSettingsRequest = Omit<TenantTaxSettings, 'tenantId'>

export const taxSettingsApi = {
  getSystemSettings: () =>
    apiClient.get<SystemTaxSettings>(`${SETTINGS_BASE}/system`).then((r) => r.data),

  updateSystemSettings: (body: UpdateSystemTaxSettingsRequest) =>
    apiClient.put<SystemTaxSettings>(`${SETTINGS_BASE}/system`, body).then((r) => r.data),

  getTenantSettings: (tenantId: string) =>
    apiClient.get<TenantTaxSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`).then((r) => r.data),

  updateTenantSettings: (tenantId: string, body: UpdateTenantTaxSettingsRequest) =>
    apiClient.put<TenantTaxSettings>(`${SETTINGS_BASE}/tenant/${tenantId}`, body).then((r) => r.data),
}
