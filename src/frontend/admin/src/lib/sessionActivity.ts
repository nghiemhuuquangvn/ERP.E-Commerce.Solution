import { jwtDecode } from 'jwt-decode'

/**
 * "Idle" means the PERSON did nothing (BR-SES-01, docs/business/Identity/Sessions.md) - not "no API call": the
 * notification bell polls every minute and must not keep an abandoned session alive. Every refresh therefore checks
 * the last real input first. The idle window is read from the access token itself: Identity issues it for a third of
 * the tenant's idle timeout, so idle = 3 x (exp - iat).
 */

let lastActivity = Date.now()

export function markActivity(now: number = Date.now()) {
  lastActivity = now
}

export function lastActivityAt() {
  return lastActivity
}

interface TimedClaims {
  exp?: number
  iat?: number
}

function claims(token: string | null): TimedClaims | null {
  if (!token) return null
  try {
    return jwtDecode<TimedClaims>(token)
  } catch {
    return null
  }
}

/** Milliseconds until the access token expires (negative when expired); null when unknown. */
export function msUntilExpiry(token: string | null, now: number = Date.now()): number | null {
  const c = claims(token)
  return c?.exp ? c.exp * 1000 - now : null
}

/** The tenant's idle window as the token tells it; null when the token does not say (keep the server's word). */
export function idleWindowMs(token: string | null): number | null {
  const c = claims(token)
  return c?.exp && c?.iat && c.exp > c.iat ? (c.exp - c.iat) * 1000 * 3 : null
}

/** True when the person has been away longer than the idle window: the session must end, not be refreshed. */
export function isUserIdle(token: string | null, now: number = Date.now()): boolean {
  const window = idleWindowMs(token)
  return window !== null && now - lastActivity > window
}
