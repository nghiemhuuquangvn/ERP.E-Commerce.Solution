import { create } from 'zustand'
import { authApi, type LoginResponse } from '@/api/auth'
import { readToken } from '@/lib/jwt'
import { unregisterThisDevice } from '@/features/notifications/push'
import { clearRefreshToken, loadRefreshToken, saveRefreshToken } from '@/lib/secureStorage'

export interface SessionUser {
  id: string
  email: string
  name: string
  roles: string[]
}

type Status = 'booting' | 'signedOut' | 'signedIn'

interface AuthState {
  status: Status
  user: SessionUser | null
  /** In memory only. */
  accessToken: string | null
  /** Mirrored in the keystore (expo-secure-store). */
  refreshToken: string | null
  permissions: string[]
  licensedModules: string[]
  /** Restores the session from the keystore at app start (one refresh). */
  bootstrap: () => Promise<void>
  /** Takes a finished sign-in (tokens present). */
  signIn: (login: LoginResponse) => Promise<void>
  /** New tokens after a refresh. */
  setTokens: (accessToken: string, refreshToken: string) => Promise<void>
  logout: () => Promise<void>
  /** Permission as the token states it - never a role name. */
  hasPermission: (permission: string) => boolean
  hasAnyPermission: (permissions: string[]) => boolean
}

const signedOut = { status: 'signedOut' as const, user: null, accessToken: null, refreshToken: null, permissions: [], licensedModules: [] }

function fromTokens(accessToken: string, refreshToken: string, fallbackName?: string) {
  const info = readToken(accessToken)
  return {
    status: 'signedIn' as const,
    accessToken,
    refreshToken,
    permissions: info.permissions,
    licensedModules: info.licensedModules,
    user: { id: info.userId, email: info.email, name: info.name || fallbackName || info.email, roles: info.roles },
  }
}

export const useAuthStore = create<AuthState>()((set, get) => ({
  status: 'booting',
  user: null,
  accessToken: null,
  refreshToken: null,
  permissions: [],
  licensedModules: [],

  bootstrap: async () => {
    const stored = await loadRefreshToken()
    if (!stored) return set(signedOut)
    try {
      const t = await authApi.refresh(stored)
      await saveRefreshToken(t.refreshToken)
      set(fromTokens(t.accessToken, t.refreshToken))
    } catch (e) {
      // Offline at start-up is not a sign-out: keep the token for the next try and show the sign-in screen meanwhile.
      const refused = (e as { response?: { status?: number } })?.response?.status
      if (refused === 400 || refused === 401) await clearRefreshToken()
      set(signedOut)
    }
  },

  signIn: async (login) => {
    if (!login.accessToken || !login.refreshToken) throw new Error('The sign-in answer has no tokens.')
    await saveRefreshToken(login.refreshToken)
    set(fromTokens(login.accessToken, login.refreshToken, login.user?.fullName))
  },

  setTokens: async (accessToken, refreshToken) => {
    await saveRefreshToken(refreshToken)
    set(fromTokens(accessToken, refreshToken, get().user?.name))
  },

  logout: async () => {
    const token = get().refreshToken
    await unregisterThisDevice() // while the access token still works; a no-op unless push is switched on
    set(signedOut)
    await clearRefreshToken()
    if (token) authApi.logout(token).catch(() => undefined) // best effort: the session is already gone on the phone
  },

  hasPermission: (p) => get().permissions.includes(p),
  hasAnyPermission: (ps) => ps.some((p) => get().permissions.includes(p)),
}))
