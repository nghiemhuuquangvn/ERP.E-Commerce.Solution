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

    function logout() {
      user.value = null
      accessToken.value = null
      refreshToken.value = null
    }

    return { user, accessToken, refreshToken, isAuthenticated, fullName, login, register, logout }
  },
  { persist: true },
)
