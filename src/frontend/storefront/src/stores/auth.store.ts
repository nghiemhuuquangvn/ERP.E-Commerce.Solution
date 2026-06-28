import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { apiClient } from '@/api/client'

interface User {
  id: string
  email: string
  firstName: string
  lastName: string
}

export const useAuthStore = defineStore(
  'auth',
  () => {
    const user = ref<User | null>(null)
    const accessToken = ref<string | null>(null)
    const refreshToken = ref<string | null>(null)

    const isAuthenticated = computed(() => !!accessToken.value)
    const fullName = computed(() =>
      user.value ? `${user.value.firstName} ${user.value.lastName}` : '',
    )

    async function login(email: string, password: string) {
      const { data } = await apiClient.post<{
        accessToken: string
        refreshToken: string
        user: User
      }>('/v1/auth/login', { email, password })
      accessToken.value = data.accessToken
      refreshToken.value = data.refreshToken
      user.value = data.user
    }

    async function register(firstName: string, lastName: string, email: string, password: string) {
      const { data } = await apiClient.post<{
        accessToken: string
        refreshToken: string
        user: User
      }>('/v1/auth/register', { firstName, lastName, email, password })
      accessToken.value = data.accessToken
      refreshToken.value = data.refreshToken
      user.value = data.user
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
