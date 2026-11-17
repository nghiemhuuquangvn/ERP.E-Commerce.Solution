import axios from 'axios'
import * as SecureStore from 'expo-secure-store'
import { refreshAccessToken } from '@/api/client'
import { useAuthStore } from '@/stores/auth.store'
import { loadRefreshToken } from '@/lib/secureStorage'

// SEC-M15 / S2 (docs/business/Identity/Sessions.md): the refresh token lives in the keystore, the access token in memory.
describe('session (customer app)', () => {
  const user = { id: 'u1', email: 'a@b.test', fullName: 'A' }

  afterEach(() => jest.restoreAllMocks())

  it('keeps the refresh token in the keystore and no token in AsyncStorage', async () => {
    useAuthStore.getState().setAuth(user, 'access-1', 'refresh-1')
    await new Promise((r) => setTimeout(r, 0))

    expect(SecureStore.setItemAsync).toHaveBeenCalledWith('erp-customer-refresh-token', 'refresh-1')
    const persisted = JSON.stringify(useAuthStore.persist.getOptions().partialize!(useAuthStore.getState()))
    expect(persisted).not.toContain('access-1')
    expect(persisted).not.toContain('refresh-1')
  })

  it('refreshes with the stored token and rotates it', async () => {
    useAuthStore.getState().setAuth(user, 'access-1', 'refresh-1')
    await new Promise((r) => setTimeout(r, 0))
    const post = jest.spyOn(axios, 'post').mockResolvedValue({ data: { accessToken: 'access-2', refreshToken: 'refresh-2' } })

    await expect(refreshAccessToken()).resolves.toBe('access-2')

    expect(post).toHaveBeenCalledWith(expect.stringContaining('/v1/auth/refresh'), { refreshToken: 'refresh-1' }, expect.anything())
    expect(useAuthStore.getState().accessToken).toBe('access-2')
    expect(await loadRefreshToken()).toBe('refresh-2')
  })

  it('signs out when the server refuses the refresh (idle timeout, session limit)', async () => {
    useAuthStore.getState().setAuth(user, 'access-1', 'refresh-1')
    await new Promise((r) => setTimeout(r, 0))
    jest.spyOn(axios, 'post').mockRejectedValue(Object.assign(new Error('401'), { isAxiosError: true, response: { status: 401 } }))

    await expect(refreshAccessToken()).rejects.toThrow()

    expect(useAuthStore.getState().isAuthenticated).toBe(false)
    await new Promise((r) => setTimeout(r, 0))
    expect(await loadRefreshToken()).toBeNull()
  })

  it('stays signed in when the phone is offline', async () => {
    useAuthStore.getState().setAuth(user, 'access-1', 'refresh-1')
    await new Promise((r) => setTimeout(r, 0))
    jest.spyOn(axios, 'post').mockRejectedValue(Object.assign(new Error('Network Error'), { isAxiosError: true }))

    await expect(refreshAccessToken()).rejects.toThrow()

    expect(useAuthStore.getState().isAuthenticated).toBe(true)
  })
})
