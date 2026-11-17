import axios from 'axios'
import { useAuthStore } from '@/stores/auth.store'
import { useLocaleStore } from '@/stores/locale.store'
import { AUTH_TRANSPORT_HEADERS, refreshSession } from '@/api/session'

const BASE_URL = import.meta.env.VITE_API_URL ?? '/api'

// No tenant header: the Gateway decides the tenant from the host this storefront is served
// on (the tenant's own domain or <code>.<base-domain>) and drops any X-Tenant-* header a
// client sends — see GatewayAuthorizationMiddleware / TenantHostResolver.

// Maps app-level locale codes to the backend's supported culture codes
// (see src/services/ProductService/src/ERP.PRD.API/appsettings.json → Localization:SupportedCultures)
const ACCEPT_LANGUAGE_MAP: Record<string, string> = {
  en: 'en-US',
  vi: 'vi-VN',
}

export const apiClient = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json', ...AUTH_TRANSPORT_HEADERS },
})

apiClient.interceptors.request.use((config) => {
  const token = useAuthStore().accessToken
  if (token) config.headers.Authorization = `Bearer ${token}`

  const locale = useLocaleStore().locale
  config.headers['Accept-Language'] = ACCEPT_LANGUAGE_MAP[locale] ?? 'en-US'


  return config
})

apiClient.interceptors.response.use(
  (r) => r,
  async (error) => {
    const original = error.config as (typeof error.config & { _retry?: boolean }) | undefined
    const isAuthCall = String(original?.url ?? '').includes('/v1/auth/')
    if (error.response?.status !== 401 || !original || original._retry || isAuthCall) {
      return Promise.reject(error)
    }

    // An expired access token (it lives a few minutes, in memory only): refresh once through the HttpOnly cookie and
    // replay the call. A refused refresh means the session is over and signs the customer out (SEC-H3 / SEC-M3).
    if (!useAuthStore().signedIn) {
      useAuthStore().logout()
      return Promise.reject(error)
    }
    original._retry = true
    try {
      const token = await refreshSession()
      original.headers = { ...(original.headers ?? {}), Authorization: `Bearer ${token}` }
      return apiClient(original)
    } catch {
      return Promise.reject(error)
    }
  },
)
