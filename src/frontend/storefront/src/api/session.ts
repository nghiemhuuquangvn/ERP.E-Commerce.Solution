import axios from 'axios'
import { useAuthStore } from '@/stores/auth.store'

const BASE_URL = import.meta.env.VITE_API_URL ?? '/api'

/**
 * SEC-H3 (docs/business/Identity/Sessions.md): Identity keeps the customer's refresh token in an HttpOnly cookie
 * (path /api/v1/auth) and reads it only when this header is present - a header another site cannot add.
 */
export const AUTH_TRANSPORT_HEADERS = { 'X-Auth-Transport': 'cookie' }

let inFlight: Promise<string> | null = null

/**
 * One refresh at a time through the cookie; every caller that met a 401 meanwhile waits for the same answer. A
 * refusal (idle timeout, session limit, revoked) signs the customer out on this device.
 */
export function refreshSession(): Promise<string> {
  if (inFlight) return inFlight
  inFlight = axios
    .post(`${BASE_URL}/v1/auth/refresh`, {}, { headers: AUTH_TRANSPORT_HEADERS })
    .then(({ data }) => {
      useAuthStore().setAccessToken(data.accessToken)
      return data.accessToken as string
    })
    .catch((error) => {
      useAuthStore().logout()
      throw error
    })
    .finally(() => {
      inFlight = null
    })
  return inFlight
}
