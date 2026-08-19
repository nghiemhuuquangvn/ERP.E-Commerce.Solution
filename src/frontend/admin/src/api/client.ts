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
let refreshQueue: Array<{ onSuccess: (token: string) => void; onError: (error: unknown) => void }> = []

function settleQueue(outcome: { token: string } | { error: unknown }) {
  const queued = refreshQueue
  refreshQueue = []
  for (const entry of queued) {
    if ('token' in outcome) entry.onSuccess(outcome.token)
    else entry.onError(outcome.error)
  }
}

// Extracted so callers other than the 401-retry interceptor below (currently
// notificationSignalR/chatSignalR, which need a confirmed-fresh token *before*
// attempting a SignalR connection — a reactive 401-retry doesn't help there, since
// SignalR's own negotiate call has no retry-after-refresh path of its own) can force
// a refresh proactively instead of waiting to react to a 401 that already happened.
// Shares the same in-flight dedup (isRefreshing/refreshQueue) as the interceptor, so
// concurrent callers (e.g. two hubs connecting at once) don't fire duplicate refresh
// requests against the same refresh token.
export function refreshAccessToken(): Promise<string> {
  if (isRefreshing) {
    return new Promise((resolve, reject) => {
      refreshQueue.push({ onSuccess: resolve, onError: reject })
    })
  }

  const refreshToken = useAuthStore.getState().refreshToken
  if (!refreshToken) {
    useAuthStore.getState().logout()
    return Promise.reject(new Error('No refresh token available.'))
  }

  isRefreshing = true
  return axios
    .post(`${BASE_URL}/v1/auth/refresh`, { refreshToken })
    .then(({ data }) => {
      useAuthStore.getState().setTokens(data.accessToken, data.refreshToken)
      settleQueue({ token: data.accessToken })
      return data.accessToken as string
    })
    .catch((refreshError) => {
      useAuthStore.getState().logout()
      settleQueue({ error: refreshError })
      throw refreshError
    })
    .finally(() => {
      isRefreshing = false
    })
}

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

    original._retry = true
    try {
      const token = await refreshAccessToken()
      original.headers!.Authorization = `Bearer ${token}`
      return instance(original)
    } catch {
      return Promise.reject(error)
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
