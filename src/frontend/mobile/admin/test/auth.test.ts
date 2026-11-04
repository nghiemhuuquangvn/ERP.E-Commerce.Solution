import * as SecureStore from 'expo-secure-store'
import { makeToken } from './token'

const mockRefresh = jest.fn()
const mockLogout = jest.fn()
jest.mock('@/api/auth', () => ({
  ADMIN_USER_TYPE: 2,
  authApi: { refresh: (...a: unknown[]) => mockRefresh(...a), logout: (...a: unknown[]) => mockLogout(...a) },
}))

import { useAuthStore } from '@/stores/auth.store'

const access = makeToken({ sub: 'u1', email: 'a@b.c', name: 'Ann', permission: ['sales_order:read'], licensed_module: ['SO'] })
const reset = () => useAuthStore.setState({ status: 'booting', user: null, accessToken: null, refreshToken: null, permissions: [], licensedModules: [] })

beforeEach(async () => {
  jest.clearAllMocks()
  mockLogout.mockResolvedValue({})
  await SecureStore.deleteItemAsync('erp-admin-refresh-token')
  reset()
})

describe('auth store', () => {
  it('signs in from the tokens: permissions and licensed modules come from the token, the refresh token goes to the keystore', async () => {
    await useAuthStore.getState().signIn({ accessToken: access, refreshToken: 'r1' })
    const s = useAuthStore.getState()
    expect(s.status).toBe('signedIn')
    expect(s.user).toMatchObject({ id: 'u1', email: 'a@b.c', name: 'Ann' })
    expect(s.hasPermission('sales_order:read')).toBe(true)
    expect(s.hasPermission('sales_order:approve')).toBe(false)
    expect(s.licensedModules).toEqual(['SO'])
    expect(await SecureStore.getItemAsync('erp-admin-refresh-token')).toBe('r1')
  })

  it('a role name gives no permission', async () => {
    await useAuthStore.getState().signIn({ accessToken: makeToken({ sub: 'u', 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Admin' }), refreshToken: 'r' })
    expect(useAuthStore.getState().hasPermission('purchase_order:approve')).toBe(false)
  })

  it('start-up restores the session with one refresh and keeps the new refresh token', async () => {
    await SecureStore.setItemAsync('erp-admin-refresh-token', 'old')
    mockRefresh.mockResolvedValue({ accessToken: access, refreshToken: 'new' })
    await useAuthStore.getState().bootstrap()
    expect(mockRefresh).toHaveBeenCalledWith('old')
    expect(useAuthStore.getState().status).toBe('signedIn')
    expect(await SecureStore.getItemAsync('erp-admin-refresh-token')).toBe('new')
  })

  it('start-up without a stored token is signed out and calls nothing', async () => {
    await useAuthStore.getState().bootstrap()
    expect(useAuthStore.getState().status).toBe('signedOut')
    expect(mockRefresh).not.toHaveBeenCalled()
  })

  it('a refused refresh token is forgotten; an unreachable server keeps it for the next start', async () => {
    await SecureStore.setItemAsync('erp-admin-refresh-token', 'bad')
    mockRefresh.mockRejectedValue({ response: { status: 401 } })
    await useAuthStore.getState().bootstrap()
    expect(await SecureStore.getItemAsync('erp-admin-refresh-token')).toBeNull()

    await SecureStore.setItemAsync('erp-admin-refresh-token', 'keep')
    mockRefresh.mockRejectedValue(new Error('Network Error'))
    await useAuthStore.getState().bootstrap()
    expect(useAuthStore.getState().status).toBe('signedOut')
    expect(await SecureStore.getItemAsync('erp-admin-refresh-token')).toBe('keep')
  })

  it('sign out clears memory and keystore and tells the server', async () => {
    await useAuthStore.getState().signIn({ accessToken: access, refreshToken: 'r1' })
    await useAuthStore.getState().logout()
    const s = useAuthStore.getState()
    expect(s).toMatchObject({ status: 'signedOut', accessToken: null, refreshToken: null, permissions: [] })
    expect(await SecureStore.getItemAsync('erp-admin-refresh-token')).toBeNull()
    expect(mockLogout).toHaveBeenCalledWith('r1')
  })
})
