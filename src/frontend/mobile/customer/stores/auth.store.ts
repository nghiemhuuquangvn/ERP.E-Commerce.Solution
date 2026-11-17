import { create } from 'zustand'
import { persist, createJSONStorage } from 'zustand/middleware'
import AsyncStorage from '@react-native-async-storage/async-storage'
import { clearRefreshToken, saveRefreshToken } from '@/lib/secureStorage'

interface User {
  id: string
  email: string
  /** What the Identity service returns (UserSummaryDto.FullName). */
  fullName?: string | null
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
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      accessToken: null,
      isAuthenticated: false,
      setAuth: (user, accessToken, refreshToken) => {
        if (refreshToken) void saveRefreshToken(refreshToken)
        set({ user, accessToken, isAuthenticated: true })
      },
      setAccessToken: (accessToken) => set({ accessToken }),
      logout: () => {
        void clearRefreshToken()
        set({ user: null, accessToken: null, isAuthenticated: false })
      },
    }),
    {
      name: 'erp-customer-auth',
      storage: createJSONStorage(() => AsyncStorage),
      // Never a token in AsyncStorage (plain file on the device).
      partialize: (s) => ({ user: s.user, isAuthenticated: s.isAuthenticated }),
    },
  ),
)
