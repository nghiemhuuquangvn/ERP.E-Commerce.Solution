import axios, { AxiosError } from 'axios'
import { useAuthStore } from '@/stores/auth.store'
import { useUiStore } from '@/stores/ui.store'
import { logger } from '@/utils/logger'

const BASE_URL = import.meta.env.VITE_API_URL ?? '/api'

// Maps this app's short locale codes to the backend's actual supported culture
// codes (see e.g. ProductService/appsettings.json's Localization:SupportedCultures).
const ACCEPT_LANGUAGE_MAP: Record<string, string> = { en: 'en-US', vi: 'vi-VN' }

export const apiClient = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' },
})

// Gateway routes (tenants/users/roles/permissions/settings/masterdata/hrm/crm/acc/wh/po/so/
// prd/pos/prm/ntf/rpt/logs/license — anything Ocelot exposes under its own `/{prefix}/...`
// route rather than the shared `/api/v1/...` one) build already gateway-rooted, root-relative
// paths (e.g. `/so/api/v1/quotations`). Reusing `apiClient` for those would make axios prepend
// `baseURL` on top (axios only treats scheme:// or protocol-relative // URLs as absolute), so
// every such request actually hit `/api/so/api/v1/quotations` — the "redundant /api/" that
// forced Ocelot/nginx to carry a whole duplicate set of double-prefixed mirror routes just to
// paper over it. `gatewayClient` has no baseURL, so these paths resolve exactly as written.
export const gatewayClient = axios.create({
  headers: { 'Content-Type': 'application/json' },
})

function attachAuthInterceptor(instance: typeof apiClient) {
  instance.interceptors.request.use((config) => {
    const token = useAuthStore.getState().accessToken
    if (token) config.headers.Authorization = `Bearer ${token}`
    const locale = useUiStore.getState().locale
    config.headers['Accept-Language'] = ACCEPT_LANGUAGE_MAP[locale] ?? 'en-US'
    return config
  })
}

attachAuthInterceptor(apiClient)
attachAuthInterceptor(gatewayClient)

let isRefreshing = false
let refreshQueue: Array<(token: string) => void> = []

function attachRefreshInterceptor(instance: typeof apiClient) {
  instance.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as typeof error.config & { _retry?: boolean }

    // Don't log failures of the log-shipping call itself — would just be noise
    // (and the logger already swallows its own request errors).
    const isLogsEndpoint = original?.url?.includes('/logs/api/v1/logs')
    if (!isLogsEndpoint) {
      const status = error.response?.status
      if (!error.response) {
        logger.trace('Warning', `Network error calling ${original?.method?.toUpperCase()} ${original?.url}`, {
          url: original?.url ?? '',
        })
      } else if (status !== undefined && status >= 500) {
        logger.exception(error, {
          url: original?.url ?? '',
          method: original?.method ?? '',
          statusCode: String(status),
        })
      }
    }

    if (error.response?.status !== 401 || original._retry) {
      return Promise.reject(error)
    }

    if (isRefreshing) {
      return new Promise((resolve) => {
        refreshQueue.push((token) => {
          original.headers!.Authorization = `Bearer ${token}`
          resolve(instance(original))
        })
      })
    }

    original._retry = true
    isRefreshing = true

    const refreshToken = useAuthStore.getState().refreshToken
    if (!refreshToken) {
      isRefreshing = false
      useAuthStore.getState().logout()
      return Promise.reject(error)
    }

    try {
      const { data } = await axios.post(`${BASE_URL}/v1/auth/refresh`, { refreshToken })
      useAuthStore.getState().setTokens(data.accessToken, data.refreshToken)
      refreshQueue.forEach((cb) => cb(data.accessToken))
      refreshQueue = []
      original.headers!.Authorization = `Bearer ${data.accessToken}`
      return instance(original)
    } catch {
      useAuthStore.getState().logout()
      return Promise.reject(error)
    } finally {
      isRefreshing = false
    }
  },
  )
}

attachRefreshInterceptor(apiClient)
attachRefreshInterceptor(gatewayClient)

export interface PaginatedResponse<T> {
  items: T[]
  totalCount: number
  pageNumber: number
  pageSize: number
}

export interface ApiError {
  message: string
  errors?: Record<string, string[]>
}
