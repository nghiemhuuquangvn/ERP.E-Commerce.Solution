import { create } from 'zustand'
import { persist, createJSONStorage } from 'zustand/middleware'
import AsyncStorage from '@react-native-async-storage/async-storage'
import { useTerminalStore } from '@/stores/terminal.store'
import { clearRefreshToken, saveRefreshToken } from '@/lib/secureStorage'

interface User {
  id: string
  email: string
  firstName: string
  lastName: string
  roles: string[]
  permissions: string[]
}

interface AuthState {
  user: User | null
  /**
   * In memory only (SEC-M15, docs/business/Identity/Sessions.md). The refresh token sits in the device keystore
   * (expo-secure-store), never in AsyncStorage; after an app restart the first 401 refreshes through it.
   */
  accessToken: string | null
  isAuthenticated: boolean
  setAuth: (user: User, accessToken: string, refreshToken?: string | null) => void
  setAccessToken: (accessToken: string) => void
  logout: () => void
  hasPermission: (permission: string) => boolean
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      user: null,
      accessToken: null,
      isAuthenticated: false,

      setAuth: (user, accessToken, refreshToken) => {
        if (refreshToken) void saveRefreshToken(refreshToken)
        set({ user, accessToken, isAuthenticated: true })
      },

      setAccessToken: (accessToken) => set({ accessToken }),

      logout: () => {
        useTerminalStore.getState().clear() // the next person signing in chooses their own terminal
        void clearRefreshToken()
        set({ user: null, accessToken: null, isAuthenticated: false })
      },

      hasPermission: (permission: string) => {
        const { user } = get()
        if (!user) return false
        if (user.roles.includes('Admin')) return true
        return user.permissions.includes(permission)
      },
    }),
    {
      name: 'erp-staff-auth',
      storage: createJSONStorage(() => AsyncStorage),
      // Never a token in AsyncStorage (plain file on the device).
      partialize: (s) => ({ user: s.user, isAuthenticated: s.isAuthenticated }),
    },
  ),
)
