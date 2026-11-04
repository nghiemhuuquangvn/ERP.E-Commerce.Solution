import { readToken } from '@/lib/jwt'
import { makeToken } from './token'

describe('readToken', () => {
  it('reads identity, repeated permission / module claims and the expiry', () => {
    const info = readToken(
      makeToken({
        sub: 'u1', email: 'a@b.c', name: 'Ann Lê', tenant_id: 't1', exp: 1900000000,
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': ['Admin'],
        permission: ['purchase_order:read', 'sales_order:read'],
        licensed_module: ['PO', 'SO'],
      }),
    )
    expect(info).toMatchObject({ userId: 'u1', email: 'a@b.c', name: 'Ann Lê', tenantId: 't1', roles: ['Admin'], licensedModules: ['PO', 'SO'], expiresAt: 1900000000 })
    expect(info.permissions).toEqual(['purchase_order:read', 'sales_order:read'])
  })

  it('a single permission claim is a list of one, and a missing one is empty', () => {
    expect(readToken(makeToken({ sub: 'u', permission: 'x:y' })).permissions).toEqual(['x:y'])
    expect(readToken(makeToken({ sub: 'u' })).permissions).toEqual([])
  })

  it('throws on something that is not a token', () => {
    expect(() => readToken('nope')).toThrow()
  })
})
