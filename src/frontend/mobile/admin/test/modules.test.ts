import fs from 'fs'
import path from 'path'
import en from '../i18n/locales/en.json'
import vi from '../i18n/locales/vi.json'
import { MODULES } from '@/modules'
import { MAX_TAB_MODULES, splitTabs, visibleModules } from '@/modules/visible'
import { poActions } from '@/modules/purchase/api'
import { soActions } from '@/modules/sales/api'
import { leaveActions } from '@/modules/hrm/api'
import type { ErpModule } from '@/modules/types'

const ids = (ms: ErpModule[]) => ms.map((m) => m.id)
const holds = (...p: string[]) => (x: string) => p.includes(x)

describe('module registry', () => {
  it('lists the four first modules in tab order', () => {
    expect(ids([...MODULES].sort((a, b) => a.order - b.order))).toEqual(['dashboard', 'purchase', 'sales', 'hrm'])
  })

  it.each(MODULES.map((m) => [m.id, m] as const))('%s has its tab file and its title in both languages', (_id, m) => {
    expect(fs.existsSync(path.resolve(__dirname, '../app/(tabs)', `${m.route}.tsx`))).toBe(true)
    const get = (o: unknown) => m.titleKey.split('.').reduce<unknown>((acc, k) => (acc as Record<string, unknown>)?.[k], o)
    expect(typeof get(en)).toBe('string')
    expect(typeof get(vi)).toBe('string')
  })

  it('module ids, routes and orders are unique', () => {
    expect(new Set(ids(MODULES)).size).toBe(MODULES.length)
    expect(new Set(MODULES.map((m) => m.route)).size).toBe(MODULES.length)
    expect(new Set(MODULES.map((m) => m.order)).size).toBe(MODULES.length)
  })

  it('every "needs action" line names a permission and a label in both languages', () => {
    for (const a of MODULES.flatMap((m) => m.attention ?? [])) {
      expect(a.permission).toMatch(/:/)
      const get = (o: unknown) => a.labelKey.split('.').reduce<unknown>((acc, k) => (acc as Record<string, unknown>)?.[k], o)
      expect(typeof get(en)).toBe('string')
      expect(typeof get(vi)).toBe('string')
    }
  })
})

describe('what a user sees', () => {
  it('Home is always there; the other modules need one of their permissions', () => {
    expect(ids(visibleModules([], []))).toEqual(['dashboard'])
    expect(ids(visibleModules(['purchase_order:read'], []))).toEqual(['dashboard', 'purchase'])
    expect(ids(visibleModules(['hrm:leave:manage'], []))).toEqual(['dashboard', 'hrm'])
  })

  it('a licensed-module list hides unlicensed modules; an empty list hides nothing', () => {
    const perms = ['purchase_order:read', 'sales_order:read']
    expect(ids(visibleModules(perms, ['SO']))).toEqual(['dashboard', 'sales'])
    expect(ids(visibleModules(perms, []))).toEqual(['dashboard', 'purchase', 'sales'])
  })

  it('modules beyond the tab limit go to More', () => {
    const extra: ErpModule[] = Array.from({ length: 3 }, (_, i) => ({ id: `x${i}`, titleKey: 't', icon: MODULES[0].icon, route: `x${i}`, permissions: [], order: 100 + i }))
    const everything = MODULES.flatMap((m) => m.permissions)
    const { tabs, overflow } = splitTabs(visibleModules(everything, [], [...MODULES, ...extra]))
    expect(tabs).toHaveLength(MAX_TAB_MODULES)
    expect(ids(overflow)).toEqual(['x0', 'x1', 'x2'])
  })
})

describe('which actions a record offers (status + permission)', () => {
  it('purchase order', () => {
    const all = holds('purchase_order:approve', 'purchase_order:finance_approve', 'purchase_order:cancel')
    expect(poActions('SUBMITTED', all)).toEqual(['approve', 'reject', 'cancel'])
    expect(poActions('PENDING_FINANCE_APPROVAL', all)).toEqual(['financeApprove', 'reject', 'cancel'])
    expect(poActions('APPROVED', all)).toEqual(['cancel'])
    expect(poActions('RECEIVED', all)).toEqual([])
    expect(poActions('SUBMITTED', holds())).toEqual([])
    expect(poActions('PENDING_FINANCE_APPROVAL', holds('purchase_order:approve'))).toEqual(['reject'])
  })

  it('sales order', () => {
    const all = holds('sales_order:approve', 'sales_order:cancel')
    expect(soActions('Submitted', all)).toEqual(['approve', 'reject', 'cancel'])
    expect(soActions('Approved', all)).toEqual(['cancel'])
    expect(soActions('Shipped', all)).toEqual([])
    expect(soActions('Submitted', holds('sales_order:cancel'))).toEqual(['cancel'])
  })

  it('leave request', () => {
    expect(leaveActions('Pending', holds('hrm:leave:manage'))).toEqual(['approve', 'reject'])
    expect(leaveActions('Approved', holds('hrm:leave:manage'))).toEqual(['cancel'])
    expect(leaveActions('Rejected', holds('hrm:leave:manage'))).toEqual([])
    expect(leaveActions('Pending', holds())).toEqual([])
  })
})
