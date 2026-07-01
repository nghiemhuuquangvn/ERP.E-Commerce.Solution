import axios from 'axios'
import { useAuthStore } from '@/stores/auth.store'
import { useLocaleStore } from '@/stores/locale.store'

const BASE_URL = import.meta.env.VITE_API_URL ?? '/api'

// Maps app-level locale codes to the backend's supported culture codes
// (see src/services/ProductService/src/ERP.PRD.API/appsettings.json → Localization:SupportedCultures)
const ACCEPT_LANGUAGE_MAP: Record<string, string> = {
  en: 'en-US',
  vi: 'vi-VN',
}

export const apiClient = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' },
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
    if (error.response?.status === 401) {
      useAuthStore().logout()
    }
    return Promise.reject(error)
  },
)
