import axios, { AxiosError } from 'axios'
import { useAuthStore } from '@/stores/auth.store'
import { useUiStore } from '@/stores/ui.store'
import { logger } from '@/utils/logger'
import { isUserIdle } from '@/lib/sessionActivity'

const BASE_URL = import.meta.env.VITE_API_URL ?? '/api'

// Maps this app's short locale codes to the backend's actual supported culture
// codes (see e.g. ProductService/appsettings.json's Localization:SupportedCultures).
const ACCEPT_LANGUAGE_MAP: Record<string, string> = { en: 'en-US', vi: 'vi-VN' }

// SEC-H3 (docs/business/Identity/Sessions.md): Identity keeps this page's refresh token in an HttpOnly cookie
// (path /api/v1/auth) and only reads it when this header is present - a header another site cannot add.
export const AUTH_TRANSPORT_HEADERS = { 'X-Auth-Transport': 'cookie' }

export const apiClient = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json', ...AUTH_TRANSPORT_HEADERS },
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

  // Nobody signed in on this page: nothing to refresh (the cookie, if any, is left to expire).
  if (!useAuthStore.getState().isAuthenticated) {
    return Promise.reject(new Error('Not signed in.'))
  }

  // Background calls (notification polling, hubs) must not keep an abandoned session alive: when the person did
  // nothing for the whole idle window, the session ends here exactly as the server would end it (BR-SES-01).
  if (isUserIdle(useAuthStore.getState().accessToken)) {
    useAuthStore.getState().expire()
    return Promise.reject(new Error('Session ended after inactivity.'))
  }

  isRefreshing = true
  return axios
    // The refresh token travels as the HttpOnly cookie (same origin); the body stays empty.
    .post(`${BASE_URL}/v1/auth/refresh`, {}, { headers: AUTH_TRANSPORT_HEADERS })
    .then(({ data }) => {
      useAuthStore.getState().setAccessToken(data.accessToken)
      settleQueue({ token: data.accessToken })
      return data.accessToken as string
    })
    .catch((refreshError) => {
      // Idle timeout, absolute limit, revoked device or account change: the session is over.
      useAuthStore.getState().expire()
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
