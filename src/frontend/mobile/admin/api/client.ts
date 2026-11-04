import axios, { type AxiosError, type AxiosInstance } from 'axios'
import { authApi } from '@/api/auth'
import { useAuthStore } from '@/stores/auth.store'
import { useLocaleStore } from '@/stores/locale.store'
import { tenantConfig } from '@/lib/tenant'

// Maps the app's short locale codes to the backend's culture codes.
const ACCEPT_LANGUAGE: Record<string, string> = { en: 'en-US', vi: 'vi-VN' }

// No answer at all (offline, wrong address, name not resolved, timeout): not the same as a refused sign-in.
export const isNetworkError = (e: unknown) => axios.isAxiosError(e) && !e.response

/** Calls under /api (auth, orders, reports ...). */
export const apiClient = axios.create({ baseURL: tenantConfig.apiUrl, headers: { 'Content-Type': 'application/json' }, timeout: 15_000 })

/**
 * Calls that are rooted at the Gateway's per-service prefix (/po/api/v1/..., /hrm/api/v1/...). It has no baseURL of its own,
 * so the tenant host is added here from the same build setting (the host IS the tenant).
 */
export const gatewayClient = axios.create({
  baseURL: tenantConfig.apiUrl.replace(/\/api$/, ''),
  headers: { 'Content-Type': 'application/json' },
  timeout: 15_000,
})

let refreshing: Promise<string> | null = null

/** One refresh at a time; every caller that met a 401 meanwhile waits for the same answer. */
export function refreshAccessToken(): Promise<string> {
  if (refreshing) return refreshing
  const token = useAuthStore.getState().refreshToken
  if (!token) {
    void useAuthStore.getState().logout()
    return Promise.reject(new Error('No refresh token.'))
  }
  refreshing = authApi
    .refresh(token)
    .then(async (t) => {
      await useAuthStore.getState().setTokens(t.accessToken, t.refreshToken)
      return t.accessToken
    })
    .catch(async (e) => {
      await useAuthStore.getState().logout()
      throw e
    })
    .finally(() => {
      refreshing = null
    })
  return refreshing
}

function attach(instance: AxiosInstance) {
  instance.interceptors.request.use((config) => {
    const token = useAuthStore.getState().accessToken
    if (token) config.headers.Authorization = `Bearer ${token}`
    config.headers['Accept-Language'] = ACCEPT_LANGUAGE[useLocaleStore.getState().locale] ?? 'en-US'
    return config
  })
  instance.interceptors.response.use(
    (r) => r,
    async (error: AxiosError) => {
      const original = error.config as (typeof error.config & { _retry?: boolean }) | undefined
      if (error.response?.status !== 401 || !original || original._retry) return Promise.reject(error)
      original._retry = true
      try {
        const token = await refreshAccessToken()
        original.headers.Authorization = `Bearer ${token}`
        return instance(original)
      } catch {
        return Promise.reject(error)
      }
    },
  )
}

attach(apiClient)
attach(gatewayClient)

export interface Paged<T> {
  items: T[]
  total: number
}
