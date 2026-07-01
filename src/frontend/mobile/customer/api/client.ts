import axios from 'axios'
import { useAuthStore } from '@/stores/auth.store'
import { useLocaleStore } from '@/stores/locale.store'

const LOCALE_TO_CULTURE: Record<string, string> = {
  en: 'en-US',
  vi: 'vi-VN',
}

export const apiClient = axios.create({
  baseURL: process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5000/api',
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
