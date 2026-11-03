import axios from 'axios'
import { useAuthStore } from '@/stores/auth.store'

const DEFAULT_API_URL = 'http://localhost:5000/api'
const BASE_URL = process.env.EXPO_PUBLIC_API_URL ?? DEFAULT_API_URL

// The Gateway takes the tenant from the request HOST; localhost belongs to no tenant (risk R15).
if (BASE_URL === DEFAULT_API_URL) {
  console.warn('EXPO_PUBLIC_API_URL is not set: calls go to localhost, which is no tenant. See docs/frontend/mobile-apps.md.')
}

// No answer at all (offline, wrong address, name not resolved, timeout): not the same as a refused sign-in.
export const isNetworkError = (e: unknown) => axios.isAxiosError(e) && !e.response

export const apiClient = axios.create({
  baseURL: BASE_URL,
  headers: { 'Content-Type': 'application/json' },
  timeout: 15_000,
})

apiClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

apiClient.interceptors.response.use(
  (r) => r,
  (error) => {
    if (error.response?.status === 401) {
      useAuthStore.getState().logout()
    }
    return Promise.reject(error)
  },
)
