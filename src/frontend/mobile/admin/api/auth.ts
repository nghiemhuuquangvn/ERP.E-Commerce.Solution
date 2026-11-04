import axios from 'axios'
import { tenantConfig } from '@/lib/tenant'

/** The Admin web signs in as user type 2 (Admin); staff and admins use the same endpoint. */
export const ADMIN_USER_TYPE = 2

export interface LoginResponse {
  succeeded?: boolean
  accessToken?: string
  refreshToken?: string
  requiresTwoFactor?: boolean
  challengeId?: string
  twoFactorMethod?: string
  expiresInSeconds?: number
  errors?: string[]
  user?: { id: string; email: string; fullName?: string; roles?: string[]; permissions?: string[] }
}

// Plain axios on purpose: these calls must not go through the interceptors (a 401 here is a refusal, not an expired session).
const http = axios.create({ baseURL: tenantConfig.apiUrl, headers: { 'Content-Type': 'application/json' }, timeout: 15_000 })

export const authApi = {
  login: (email: string, password: string) =>
    http.post<LoginResponse>('/v1/auth/login', { email, password, userType: ADMIN_USER_TYPE }).then((r) => r.data),
  verifyTwoFactor: (challengeId: string, code: string) =>
    http.post<LoginResponse>('/v1/auth/2fa/verify', { challengeId, code }).then((r) => r.data),
  resendTwoFactor: (challengeId: string) => http.post('/v1/auth/2fa/resend', { challengeId }).then((r) => r.data),
  refresh: (refreshToken: string) =>
    http.post<{ accessToken: string; refreshToken: string }>('/v1/auth/refresh', { refreshToken }).then((r) => r.data),
  logout: (refreshToken: string) => http.post('/v1/auth/logout', { refreshToken }).then((r) => r.data),
}
