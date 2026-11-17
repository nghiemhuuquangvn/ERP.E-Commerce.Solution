import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

const post = vi.fn()
vi.mock('@/api/client', () => ({ apiClient: { post: (...args: unknown[]) => post(...args) } }))

import { useAuthStore } from './auth.store'

// SEC-H3 / S1: signing out must revoke the tokens on the server, not only forget them in this browser.
describe('auth store sign-out', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    post.mockReset()
  })

  function signedIn() {
    const store = useAuthStore()
    store.accessToken = 'access-1'
    store.refreshToken = 'refresh-1'
    store.user = { id: 'u1', email: 'a@b.test', fullName: 'A' }
    return store
  }

  it('asks the server to revoke the refresh token with the access token of the session', async () => {
    post.mockResolvedValue({ data: {} })
    const store = signedIn()

    await store.signOut()

    expect(post).toHaveBeenCalledWith(
      '/v1/auth/logout',
      { refreshToken: 'refresh-1' },
      { headers: { Authorization: 'Bearer access-1' } },
    )
    expect(store.isAuthenticated).toBe(false)
    expect(store.refreshToken).toBeNull()
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
  })
})
