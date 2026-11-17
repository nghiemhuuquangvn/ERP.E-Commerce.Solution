import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { apiClient } from '@/api/client'
import { refreshSession } from '@/api/session'

interface User {
  id: string
  email: string
  fullName: string | null
}

interface AuthResponse {
  accessToken: string | null
  /** Null for this app: Identity keeps the refresh token in an HttpOnly cookie (X-Auth-Transport: cookie, SEC-H3). */
  refreshToken: string | null
  succeeded: boolean
  errors?: string[]
  user?: User | null
}

/**
 * Session of a signed-in customer (docs/business/Identity/Sessions.md). The access token lives in memory only; the
 * refresh token is an HttpOnly cookie the page cannot read. Only `user` and the "signed in" flag are kept in
 * localStorage, so a reload shows the customer's name and the first call refreshes through the cookie.
 */
export const useAuthStore = defineStore(
  'auth',
  () => {
    const user = ref<User | null>(null)
    const accessToken = ref<string | null>(null)
    const signedIn = ref(false)

    const isAuthenticated = computed(() => signedIn.value)
    const fullName = computed(() => user.value?.fullName ?? '')

    function accept(data: AuthResponse) {
      accessToken.value = data.accessToken
      user.value = data.user ?? null
      signedIn.value = !!data.accessToken
    }

    async function login(email: string, password: string) {
      // Tenant comes from the host (resolved by the Gateway), never from the client.
      const { data } = await apiClient.post<AuthResponse>('/v1/auth/login', {
        email,
        password,
        userType: 0, // Customer
      })
      accept(data)
    }

    async function register(firstName: string, lastName: string, email: string, password: string) {
      const { data } = await apiClient.post<AuthResponse>('/v1/auth/register', {
        email,
        password,
        userName: `${firstName} ${lastName}`.trim(),
        userType: 0, // Customer — returns LoginResponse with tokens
      })
      accept(data)
    }

    function setAccessToken(token: string) {
      accessToken.value = token
    }

    /** Forgets the session on this device only (the server already refused it). */
    function logout() {
      user.value = null
      accessToken.value = null
      signedIn.value = false
    }

    /**
     * The customer signs out: the server revokes the session (refresh cookie + access token) and deletes the cookie,
     * then the page forgets it - also when the server cannot be reached. After a reload there is no access token yet,
     * so one refresh through the cookie gets the token the logout call needs.
     */
    async function signOut() {
      let access = accessToken.value
      if (!access && signedIn.value) {
        access = await refreshSession().catch(() => null)
      }
      logout()
      if (!access) return
      try {
        await apiClient.post('/v1/auth/logout', {}, { headers: { Authorization: `Bearer ${access}` } })
      } catch {
        // Best effort: the session then ends by itself after the idle timeout.
      }
    }

    return { user, accessToken, signedIn, isAuthenticated, fullName, login, register, setAccessToken, logout, signOut }
  },
  // Never a token in localStorage (SEC-H3).
  { persist: { paths: ['user', 'signedIn'] } },
)
