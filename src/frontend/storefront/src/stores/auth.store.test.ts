import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

const post = vi.fn()
const refreshSession = vi.fn()
vi.mock('@/api/client', () => ({ apiClient: { post: (...args: unknown[]) => post(...args) } }))
vi.mock('@/api/session', () => ({ refreshSession: () => refreshSession() }))

import { useAuthStore } from './auth.store'

// SEC-H3 / S1-S2: the session is revoked on the server, and no token is ever kept in localStorage.
describe('auth store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    post.mockReset()
    refreshSession.mockReset()
  })

  function signedIn(access: string | null = 'access-1') {
    const store = useAuthStore()
    store.accessToken = access
    store.signedIn = true
    store.user = { id: 'u1', email: 'a@b.test', fullName: 'A' }
    return store
  }

  it('signs in from the body access token; the refresh token stays in the HttpOnly cookie', async () => {
    post.mockResolvedValue({ data: { accessToken: 'access-1', refreshToken: null, succeeded: true, user: { id: 'u1', email: 'a@b.test', fullName: 'A' } } })
    const store = useAuthStore()

    await store.login('a@b.test', 'pw')

    expect(store.isAuthenticated).toBe(true)
    expect(store.accessToken).toBe('access-1')
  })

  it('asks the server to revoke the session with the access token of the session', async () => {
    post.mockResolvedValue({ data: {} })
    const store = signedIn()

    await store.signOut()

    expect(post).toHaveBeenCalledWith('/v1/auth/logout', {}, { headers: { Authorization: 'Bearer access-1' } })
    expect(store.isAuthenticated).toBe(false)
    expect(store.accessToken).toBeNull()
  })

  it('after a reload (no access token in memory) refreshes once through the cookie, then signs out', async () => {
    refreshSession.mockResolvedValue('access-2')
    post.mockResolvedValue({ data: {} })
    const store = signedIn(null)

    await store.signOut()

    expect(refreshSession).toHaveBeenCalledOnce()
    expect(post).toHaveBeenCalledWith('/v1/auth/logout', {}, { headers: { Authorization: 'Bearer access-2' } })
  })

  it('is signed out on this device even when the server cannot be reached', async () => {
    post.mockRejectedValue(new Error('network'))
    const store = signedIn()

    await expect(store.signOut()).resolves.toBeUndefined()

    expect(store.isAuthenticated).toBe(false)
    expect(store.user).toBeNull()
  })

  it('does not call the server when nobody is signed in', async () => {
    await useAuthStore().signOut()

    expect(post).not.toHaveBeenCalled()
    expect(refreshSession).not.toHaveBeenCalled()
  })
})
