const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
const B64 = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/'

/** Base64url -> UTF-8 text without relying on a global atob (not present in every runtime). */
function decodeBase64Url(input: string): string {
  const s = input.replace(/-/g, '+').replace(/_/g, '/').replace(/=+$/, '')
  const bytes: number[] = []
  let bits = 0
  let acc = 0
  for (const ch of s) {
    const v = B64.indexOf(ch)
    if (v < 0) throw new Error('bad base64')
    acc = (acc << 6) | v
    bits += 6
    if (bits >= 8) {
      bits -= 8
      bytes.push((acc >> bits) & 0xff)
    }
  }
  let out = ''
  for (let i = 0; i < bytes.length; ) {
    const b = bytes[i++]
    if (b < 0x80) out += String.fromCharCode(b)
    else if (b < 0xe0) out += String.fromCharCode(((b & 0x1f) << 6) | (bytes[i++] & 0x3f))
    else if (b < 0xf0) out += String.fromCharCode(((b & 0x0f) << 12) | ((bytes[i++] & 0x3f) << 6) | (bytes[i++] & 0x3f))
    else {
      const cp = ((b & 0x07) << 18) | ((bytes[i++] & 0x3f) << 12) | ((bytes[i++] & 0x3f) << 6) | (bytes[i++] & 0x3f)
      out += String.fromCodePoint(cp)
    }
  }
  return out
}

export interface TokenInfo {
  userId: string
  email: string
  name: string
  roles: string[]
  /** Every permission the token carries (the server already removed platform-only ones for a shop). */
  permissions: string[]
  /** Licensed module codes (SO, PO, HRM, RPT ...). */
  licensedModules: string[]
  tenantId: string
  /** Seconds since 1970; 0 when absent. */
  expiresAt: number
}

const many = (v: unknown): string[] => (Array.isArray(v) ? v.map(String) : v == null ? [] : [String(v)])

/** Reads (does not verify) the claims of an access token. The server verifies it on every call; the app only uses it to show or hide things. */
export function readToken(token: string): TokenInfo {
  const part = token.split('.')[1]
  if (!part) throw new Error('not a token')
  const c = JSON.parse(decodeBase64Url(part)) as Record<string, unknown>
  return {
    userId: String(c.sub ?? ''),
    email: String(c.email ?? ''),
    name: String(c.name ?? ''),
    roles: many(c.role ?? c[ROLE_CLAIM]),
    permissions: many(c.permission),
    licensedModules: many(c.licensed_module),
    tenantId: String(c.tenant_id ?? ''),
    expiresAt: Number(c.exp ?? 0),
  }
}
