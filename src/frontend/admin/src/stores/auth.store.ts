import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export interface User {
  id: string
  email: string
  fullName?: string | null
  // Decoded once from the JWT's `tenant_id` claim at login time (see
  // useAuth.ts's completeLogin) and stored alongside the user — the backend's
  // LoginResponse.user doesn't carry tenantId itself, only the token does.
  tenantId: string
  roles: string[]
  permissions: string[]
}

interface AuthState {
  user: User | null
  /**
   * In memory only (SEC-H3, docs/business/Identity/Sessions.md): never written to localStorage. The refresh token is an
   * HttpOnly cookie this page cannot read; after a reload the first 401 refreshes through that cookie.
   */
  accessToken: string | null
  isAuthenticated: boolean
  /** Set when the server ended the session (idle timeout / absolute limit / revoked); the login page explains it. */
  sessionExpired: boolean
  setUser: (user: User) => void
  setAccessToken: (accessToken: string) => void
  logout: () => void
  /** The session ended on the server: sign out here and remember why. */
  expire: () => void
  clearSessionExpired: () => void
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      user: null,
      accessToken: null,
      isAuthenticated: false,
      sessionExpired: false,

      setUser: (user) => set({ user, isAuthenticated: true, sessionExpired: false }),

      setAccessToken: (accessToken) => set({ accessToken }),

      logout: () => set({ user: null, accessToken: null, isAuthenticated: false }),

      expire: () => set({ user: null, accessToken: null, isAuthenticated: false, sessionExpired: true }),

      clearSessionExpired: () => set({ sessionExpired: false }),
    }),
    {
      name: 'erp-auth',
      // Only what the page needs to render before the first call - never a token.
      partialize: (s) => ({
        user: s.user,
        isAuthenticated: s.isAuthenticated,
      }),
    },
  ),
)
