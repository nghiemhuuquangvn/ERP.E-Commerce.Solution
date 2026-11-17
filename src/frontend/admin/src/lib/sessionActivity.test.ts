import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import axios from 'axios'
import { idleWindowMs, isUserIdle, markActivity, msUntilExpiry } from './sessionActivity'
import { refreshAccessToken } from '@/api/client'
import { useAuthStore } from '@/stores/auth.store'

// A JWT with only the timing claims (the signature is never checked in the browser).
function token(iatSec: number, expSec: number) {
  const b64 = (o: object) => btoa(JSON.stringify(o)).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_')
  return `${b64({ alg: 'none' })}.${b64({ iat: iatSec, exp: expSec })}.x`
}

const NOW = 1_800_000_000_000
const FIVE_MIN_TOKEN = token(NOW / 1000, NOW / 1000 + 300) // Identity: a third of a 15-minute idle timeout

describe('session activity (BR-SES-01: idle = the person did nothing)', () => {
  it('reads the idle window from the token: three times its lifetime', () => {
    expect(idleWindowMs(FIVE_MIN_TOKEN)).toBe(15 * 60_000)
    expect(msUntilExpiry(FIVE_MIN_TOKEN, NOW + 60_000)).toBe(240_000)
  })

  it('is idle only after the whole window without input', () => {
    markActivity(NOW)
    expect(isUserIdle(FIVE_MIN_TOKEN, NOW + 14 * 60_000)).toBe(false)
    expect(isUserIdle(FIVE_MIN_TOKEN, NOW + 16 * 60_000)).toBe(true)
    expect(isUserIdle(null, NOW + 16 * 60_000)).toBe(false) // unknown: the server decides
  })
})

describe('refresh through the HttpOnly cookie (SEC-H3)', () => {
  beforeEach(() => {
    useAuthStore.setState({ user: null, accessToken: FIVE_MIN_TOKEN, isAuthenticated: true, sessionExpired: false })
  })
  afterEach(() => vi.restoreAllMocks())

  it('never keeps a token in localStorage', () => {
    useAuthStore.getState().setAccessToken('secret-access')
    expect(localStorage.getItem('erp-auth') ?? '').not.toContain('secret-access')
  })

  it('posts an empty body with the transport header and keeps the new access token in memory', async () => {
    markActivity(Date.now())
    const post = vi.spyOn(axios, 'post').mockResolvedValue({ data: { accessToken: 'fresh', refreshToken: null } })

    await expect(refreshAccessToken()).resolves.toBe('fresh')

    expect(post).toHaveBeenCalledWith(expect.stringContaining('/v1/auth/refresh'), {}, { headers: { 'X-Auth-Transport': 'cookie' } })
    expect(useAuthStore.getState().accessToken).toBe('fresh')
  })

  it('ends the session instead of refreshing when the person was away (background polling)', async () => {
    const issued = Math.floor(Date.now() / 1000) - 3600
    useAuthStore.setState({ accessToken: token(issued, issued + 300) })
    markActivity(Date.now() - 20 * 60_000)
    const post = vi.spyOn(axios, 'post')

    await expect(refreshAccessToken()).rejects.toThrow()

    expect(post).not.toHaveBeenCalled()
    expect(useAuthStore.getState().isAuthenticated).toBe(false)
    expect(useAuthStore.getState().sessionExpired).toBe(true)
  })

  it('marks the session expired when the server refuses the refresh', async () => {
    markActivity(Date.now())
    vi.spyOn(axios, 'post').mockRejectedValue(new Error('401'))

    await expect(refreshAccessToken()).rejects.toThrow()

    expect(useAuthStore.getState().sessionExpired).toBe(true)
  })
})
