import { apiClient } from './client'

// IdentityService's UsersController is mounted under the Gateway's `/id/api/...`
// route, same as settings.api.ts — see that file's comment for why the prefix
// differs from apiClient's default `/api` baseURL.
const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const GATEWAY_ROOT = API_BASE.replace(/\/api\/?$/, '')
const USERS_BASE = `${GATEWAY_ROOT}/id/api/users`

export interface EnableTwoFactorRequest {
  method: 'Sms' | 'Email'
  phoneNumber?: string | null
}

export interface MessageResponse {
  message: string
}

// Minimal self-service 2FA API only — this is not a full Users CRUD wrapper.
export const usersApi = {
  enableTwoFactor: (userId: string, body: EnableTwoFactorRequest) =>
    apiClient.post<MessageResponse>(`${USERS_BASE}/${userId}/2fa/enable`, body).then((r) => r.data),

  disableTwoFactor: (userId: string) =>
    apiClient.post<MessageResponse>(`${USERS_BASE}/${userId}/2fa/disable`).then((r) => r.data),
}
