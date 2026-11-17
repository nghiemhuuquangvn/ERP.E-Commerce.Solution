import axios from 'axios'
import { useAuthStore } from '@/stores/auth.store'
import { loadRefreshToken, saveRefreshToken } from '@/lib/secureStorage'
import { useLocaleStore } from '@/stores/locale.store'

const LOCALE_TO_CULTURE: Record<string, string> = {
  en: 'en-US',
  vi: 'vi-VN',
}

const DEFAULT_API_URL = 'http://localhost:5000/api'
const API_URL = process.env.EXPO_PUBLIC_API_URL ?? DEFAULT_API_URL

// The Gateway takes the tenant from the request HOST; localhost belongs to no tenant (risk R15).
if (API_URL === DEFAULT_API_URL) {
  console.warn('EXPO_PUBLIC_API_URL is not set: calls go to localhost, which is no tenant, so sign-in and ordering will be refused. See docs/frontend/mobile-apps.md.')
}

// No answer at all (offline, wrong address, name not resolved, timeout): not the same as a refused sign-in.
export const isNetworkError = (e: unknown) => axios.isAxiosError(e) && !e.response

export const apiClient = axios.create({
  baseURL: API_URL,
  headers: { 'Content-Type': 'application/json' },
  timeout: 15_000,
})

apiClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken
  if (token) config.headers.Authorization = `Bearer ${token}`
  const locale = useLocaleStore.getState().locale
  config.headers['Accept-Language'] = LOCALE_TO_CULTURE[locale] ?? 'en-US'
  return config
})

let refreshing: Promise<string> | null = null

/**
 * One refresh at a time with the refresh token from the device keystore; every caller that met a 401 meanwhile waits
 * for the same answer. The access token lives a few minutes (a third of the shop's idle timeout, Sessions.md), so a
 * working shift refreshes regularly; a refusal (idle timeout, session limit, revoked device) signs the person out.
 */
export function refreshAccessToken(): Promise<string> {
  if (refreshing) return refreshing
  refreshing = loadRefreshToken()
    .then(async (stored) => {
      if (!stored) throw new Error('No refresh token.')
      const { data } = await axios.post(`${API_URL}/v1/auth/refresh`, { refreshToken: stored }, { timeout: 15_000 })
      if (data.refreshToken) await saveRefreshToken(data.refreshToken)
      useAuthStore.getState().setAccessToken(data.accessToken)
      return data.accessToken as string
    })
    .catch((error) => {
      // Offline is not a refused session: keep the person signed in and let the call fail as a network error.
      if (!isNetworkError(error)) useAuthStore.getState().logout()
      throw error
    })
    .finally(() => {
      refreshing = null
    })
  return refreshing
}

apiClient.interceptors.response.use(
  (r) => r,
  async (error) => {
    const original = error.config as (typeof error.config & { _retry?: boolean }) | undefined
    const isAuthCall = String(original?.url ?? '').includes('/v1/auth/')
    if (error.response?.status !== 401 || !original || original._retry || isAuthCall) return Promise.reject(error)
    if (!useAuthStore.getState().isAuthenticated) return Promise.reject(error)

    original._retry = true
    try {
      const token = await refreshAccessToken()
      original.headers = { ...(original.headers ?? {}), Authorization: `Bearer ${token}` }
      return apiClient(original)
    } catch {
      return Promise.reject(error)
    }
  },
)
