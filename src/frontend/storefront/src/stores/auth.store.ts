import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { apiClient } from '@/api/client'

interface User {
  id: string
  email: string
  fullName: string | null
}

interface AuthResponse {
  accessToken: string | null
  refreshToken: string | null
  succeeded: boolean
  errors?: string[]
  user?: User | null
}


export const useAuthStore = defineStore(
  'auth',
  () => {
    const user = ref<User | null>(null)
    const accessToken = ref<string | null>(null)
    const refreshToken = ref<string | null>(null)

    const isAuthenticated = computed(() => !!accessToken.value)
    const fullName = computed(() => user.value?.fullName ?? '')

    async function login(email: string, password: string) {
      // Tenant comes from the host (resolved by the Gateway), never from the client.
      const { data } = await apiClient.post<AuthResponse>('/v1/auth/login', {
        email,
        password,
        userType: 0, // Customer
      })
      accessToken.value = data.accessToken
      refreshToken.value = data.refreshToken
      user.value = data.user ?? null
    }

    async function register(firstName: string, lastName: string, email: string, password: string) {
      const { data } = await apiClient.post<AuthResponse>('/v1/auth/register', {
        email,
        password,
        userName: `${firstName} ${lastName}`.trim(),
        userType: 0, // Customer — returns LoginResponse with tokens
      })
      accessToken.value = data.accessToken
      refreshToken.value = data.refreshToken
      user.value = data.user ?? null
    }

    /** Forgets the session on this device only (used when the server already refused the token). */
    function logout() {
      user.value = null
      accessToken.value = null
      refreshToken.value = null
    }

    /**
     * The customer signs out: forget the session here first (the page must never stay signed in because the network
     * failed), then ask the server to revoke the refresh token and the access token, so a copy of them (another tab,
     * a stolen localStorage) stops working too. SEC-H3 / S1 (docs/business/Identity/SecurityReview-2026-11.md).
     */
    async function signOut() {
      const access = accessToken.value
      const refresh = refreshToken.value
      logout()
      if (!access || !refresh) return
      try {
        await apiClient.post(
          '/v1/auth/logout',
          { refreshToken: refresh },
          { headers: { Authorization: `Bearer ${access}` } },
        )
      } catch {
        // Best effort: an expired access token cannot be revoked any more and the refresh token then expires on its own.
      }
    }

    return { user, accessToken, refreshToken, isAuthenticated, fullName, login, register, logout, signOut }
  },
  { persist: true },
)
