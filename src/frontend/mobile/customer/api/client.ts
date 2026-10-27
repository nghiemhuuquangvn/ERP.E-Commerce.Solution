import axios from 'axios'
import { useAuthStore } from '@/stores/auth.store'
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

apiClient.interceptors.response.use(
  (r) => r,
  (error) => {
    if (error.response?.status === 401) useAuthStore.getState().logout()
    return Promise.reject(error)
  },
)
