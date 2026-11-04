import type { InternalAxiosRequestConfig } from 'axios'
import { makeToken } from './token'

const mockRefresh = jest.fn()
jest.mock('@/api/auth', () => ({
  ADMIN_USER_TYPE: 2,
  authApi: { refresh: (...a: unknown[]) => mockRefresh(...a), logout: jest.fn().mockResolvedValue({}) },
}))

import { apiClient, gatewayClient } from '@/api/client'
import { useAuthStore } from '@/stores/auth.store'

const fresh = makeToken({ sub: 'u1', permission: ['x:y'] })

/** A fake server: 401 for any token but the fresh one; records the Authorization it saw. */
function install(client: typeof apiClient) {
  const seen: (string | undefined)[] = []
  client.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
    const auth = config.headers.Authorization as string | undefined
    seen.push(auth)
    if (auth !== `Bearer ${fresh}`) throw Object.assign(new Error('401'), { config, response: { status: 401, data: {}, config } })
    return { data: { ok: true }, status: 200, statusText: 'OK', headers: {}, config }
  }
  return seen
}

beforeEach(() => {
  jest.clearAllMocks()
  useAuthStore.setState({ status: 'signedIn', accessToken: 'stale', refreshToken: 'r0', permissions: [], licensedModules: [], user: null })
})

describe('api client', () => {
  it('several calls that meet a 401 together cause ONE refresh and are all retried with the new token', async () => {
    const seen = install(apiClient)
    mockRefresh.mockResolvedValue({ accessToken: fresh, refreshToken: 'r1' })
    const results = await Promise.all([apiClient.get('/a'), apiClient.get('/b'), apiClient.get('/c')])
    expect(results.map((r) => r.data)).toEqual([{ ok: true }, { ok: true }, { ok: true }])
    expect(mockRefresh).toHaveBeenCalledTimes(1)
    expect(seen.filter((s) => s === `Bearer ${fresh}`)).toHaveLength(3)
    expect(useAuthStore.getState().refreshToken).toBe('r1')
  })

  it('the Gateway-rooted client refreshes the same way', async () => {
    install(gatewayClient)
    mockRefresh.mockResolvedValue({ accessToken: fresh, refreshToken: 'r1' })
    await expect(gatewayClient.get('/po/api/v1/purchase-orders')).resolves.toMatchObject({ data: { ok: true } })
  })

  it('when the refresh is refused the user is signed out and the original error is returned', async () => {
    install(apiClient)
    mockRefresh.mockRejectedValue({ response: { status: 401 } })
    await expect(apiClient.get('/a')).rejects.toMatchObject({ response: { status: 401 } })
    expect(useAuthStore.getState().status).toBe('signedOut')
  })

  it('a 401 is retried once only (no refresh loop)', async () => {
    install(apiClient)
    mockRefresh.mockResolvedValue({ accessToken: 'still-bad', refreshToken: 'r1' })
    await expect(apiClient.get('/a')).rejects.toMatchObject({ response: { status: 401 } })
    expect(mockRefresh).toHaveBeenCalledTimes(1)
  })

  it('sends the language the user chose', async () => {
    let lang: unknown
    apiClient.defaults.adapter = async (config: InternalAxiosRequestConfig) => {
      lang = config.headers['Accept-Language']
      return { data: {}, status: 200, statusText: 'OK', headers: {}, config }
    }
    await apiClient.get('/a')
    expect(lang).toBe('en-US')
  })
})
