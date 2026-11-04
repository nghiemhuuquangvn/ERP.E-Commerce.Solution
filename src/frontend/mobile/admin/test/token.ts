const b64url = (o: object) => Buffer.from(JSON.stringify(o), 'utf8').toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')

/** An unsigned test token with the given claims (the app only reads claims; the server verifies the signature). */
export const makeToken = (claims: Record<string, unknown>) => `${b64url({ alg: 'RS256' })}.${b64url(claims)}.sig`
