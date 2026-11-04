import fs from 'fs'
import path from 'path'
import en from '../i18n/locales/en.json'
import vi from '../i18n/locales/vi.json'
import { MODULES } from '@/modules'
import { barFor, MAX_BAR_ITEMS, moduleLanding, owningModule, visibleModules, visibleSections } from '@/modules/visible'
import { poActions } from '@/modules/purchase/api'
import { soActions } from '@/modules/sales/api'
import { leaveActions } from '@/modules/hrm/api'
import type { ErpModule } from '@/modules/types'

const ids = (ms: { id?: string; key?: string }[]) => ms.map((m) => m.id ?? m.key)
const holds = (...p: string[]) => (x: string) => p.includes(x)
const get = (o: unknown, key: string) => key.split('.').reduce<unknown>((acc, k) => (acc as Record<string, unknown>)?.[k], o)
const tabFile = (route: string) => path.resolve(__dirname, '../app/(tabs)', route + '.tsx')
const EVERYTHING = MODULES.flatMap((m) => [...m.permissions, ...(m.sections ?? []).flatMap((s) => s.permissions)])

describe('module registry', () => {
  it('lists the four modules in menu order', () => {
    expect(ids([...MODULES].sort((a, b) => a.order - b.order))).toEqual(['dashboard', 'purchase', 'sales', 'hrm'])
  })

  it.each(MODULES.map((m) => [m.id, m] as const))('%s has its screen file and its title in both languages', (_id, m) => {
    expect(fs.existsSync(tabFile(m.route))).toBe(true)
    expect(typeof get(en, m.titleKey)).toBe('string')
    expect(typeof get(vi, m.titleKey)).toBe('string')
  })

  it('module ids, routes and orders are unique', () => {
    expect(new Set(ids(MODULES)).size).toBe(MODULES.length)
    expect(new Set(MODULES.map((m) => m.route)).size).toBe(MODULES.length)
    expect(new Set(MODULES.map((m) => m.order)).size).toBe(MODULES.length)
  })

  const SECTIONS = MODULES.flatMap((m) => (m.sections ?? []).map((s) => [m.id + '/' + s.id, m, s] as const))

  it.each(SECTIONS)('section %s has its screen file and its title in both languages', (_id, _m, s) => {
    expect(fs.existsSync(tabFile(s.route))).toBe(true)
    expect(typeof get(en, s.titleKey)).toBe('string')
    expect(typeof get(vi, s.titleKey)).toBe('string')
    expect(s.permissions.length).toBeGreaterThan(0)
  })

  it('section ids and routes are unique across the whole app, and the first section is the module route', () => {
    const all = SECTIONS.map(([, , s]) => s)
    expect(new Set(all.map((s) => s.id)).size).toBe(all.length)
    expect(new Set(all.map((s) => s.route)).size).toBe(all.length)
    for (const m of MODULES.filter((x) => x.sections?.length)) {
      expect([...m.sections!].sort((a, b) => a.order - b.order)[0].route).toBe(m.route)
      expect(new Set(m.sections!.map((s) => s.order)).size).toBe(m.sections!.length)
    }
  })

  it('a module with sections lists the union of their permissions', () => {
    for (const m of MODULES.filter((x) => x.sections?.length)) {
      expect([...m.permissions].sort()).toEqual([...new Set(m.sections!.flatMap((s) => s.permissions))].sort())
    }
  })

  it('every "needs action" line names a permission and a label in both languages', () => {
    for (const a of MODULES.flatMap((m) => m.attention ?? [])) {
      expect(a.permission).toMatch(/:/)
      expect(typeof get(en, a.labelKey)).toBe('string')
      expect(typeof get(vi, a.labelKey)).toBe('string')
      expect(fs.existsSync(tabFile(a.href.pathname.replace(/^\//, '')))).toBe(true)
    }
  })
})

describe('what a user sees', () => {
  it('Home is always there; the other modules need one of their sections permissions', () => {
    expect(ids(visibleModules([], []))).toEqual(['dashboard'])
    expect(ids(visibleModules(['purchase_order:read'], []))).toEqual(['dashboard', 'purchase'])
    expect(ids(visibleModules(['supplier:read'], []))).toEqual(['dashboard', 'purchase'])
    expect(ids(visibleModules(['hrm:leave:manage'], []))).toEqual(['dashboard', 'hrm'])
    expect(ids(visibleModules(['hrm:payroll:manage'], []))).toEqual(['dashboard', 'hrm'])
  })

  it('a licensed-module list hides unlicensed modules; an empty list hides nothing', () => {
    const perms = ['purchase_order:read', 'sales_order:read']
    expect(ids(visibleModules(perms, ['SO']))).toEqual(['dashboard', 'sales'])
    expect(ids(visibleModules(perms, []))).toEqual(['dashboard', 'purchase', 'sales'])
  })

  it('a module shows only the sections its permissions allow, in order', () => {
    const purchase = MODULES.find((m) => m.id === 'purchase')!
    expect(ids(visibleSections(purchase, ['rfq:read', 'purchase_order:read', 'supplier:read']))).toEqual(['purchase-orders', 'purchase-suppliers', 'purchase-rfqs'])
    expect(visibleSections(purchase, ['sales_order:read'])).toEqual([])
  })

  it('opening a module lands on its first section the user may open', () => {
    const purchase = MODULES.find((m) => m.id === 'purchase')!
    expect(moduleLanding(purchase, ['purchase_order:read'])).toBe('purchase')
    expect(moduleLanding(purchase, ['supplier_invoice:read', 'rfq:read'])).toBe('purchase-invoices')
    expect(moduleLanding(MODULES[0], [])).toBe('index')
  })

  it('finds the module a screen belongs to', () => {
    expect(owningModule('purchase-suppliers')?.id).toBe('purchase')
    expect(owningModule('hrm-leave')?.id).toBe('hrm')
    expect(owningModule('index')?.id).toBe('dashboard')
    expect(owningModule('notifications')).toBeUndefined()
  })
})

describe('the bottom bar changes with the module', () => {
  const visible = (perms: string[]) => visibleModules(perms, [])

  it('on Home it lists the modules', () => {
    const bar = barFor('index', visible(EVERYTHING), EVERYTHING)
    expect(bar.mode).toBe('modules')
    expect(ids(bar.items)).toEqual(['dashboard', 'purchase', 'sales', 'hrm'])
    expect(bar.items.map((i) => i.route)).toEqual(['index', 'purchase', 'sales', 'hrm'])
    expect(bar.hasMore).toBe(false)
  })

  it('inside Purchasing it shows Home and the purchasing functions', () => {
    const bar = barFor('purchase-suppliers', visible(EVERYTHING), EVERYTHING)
    expect(bar.mode).toBe('module')
    expect(bar.module?.id).toBe('purchase')
    expect(bar.items.map((i) => i.route)).toEqual(['index', 'purchase', 'purchase-suppliers', 'purchase-invoices'])
    expect(bar.overflow.map((i) => i.route)).toEqual(['purchase-returns', 'purchase-rfqs'])
    expect(bar.hasMore).toBe(true)
  })

  it('inside Sales all four functions fit, with no More', () => {
    const bar = barFor('sales-quotations', visible(EVERYTHING), EVERYTHING)
    expect(bar.items.map((i) => i.route)).toEqual(['index', 'sales', 'sales-quotations', 'sales-credit-limits', 'sales-commissions'])
    expect(bar.hasMore).toBe(false)
  })

  it('HR has five functions: Home, three, and More with the other two', () => {
    const bar = barFor('hrm-leave', visible(EVERYTHING), EVERYTHING)
    expect(bar.items).toHaveLength(MAX_BAR_ITEMS - 1)
    expect(bar.items.map((i) => i.route)).toEqual(['index', 'hrm', 'hrm-leave', 'hrm-attendance'])
    expect(bar.overflow.map((i) => i.route)).toEqual(['hrm-payslips', 'hrm-shifts'])
  })

  it('a function the user may not open is not in the bar', () => {
    const perms = ['purchase_order:read', 'rfq:read']
    const bar = barFor('purchase', visible(perms), perms)
    expect(bar.items.map((i) => i.route)).toEqual(['index', 'purchase', 'purchase-rfqs'])
    expect(bar.hasMore).toBe(false)
  })

  it('a module without sections, or an unknown screen, shows the module bar', () => {
    expect(barFor('index', visible([]), []).items.map((i) => i.route)).toEqual(['index'])
    expect(barFor('nowhere', visible(EVERYTHING), EVERYTHING).mode).toBe('modules')
  })

  it('too many modules: the bar keeps room for More, which opens the side menu', () => {
    const extra: ErpModule[] = Array.from({ length: 3 }, (_, i) => ({ id: 'x' + i, titleKey: 't', icon: MODULES[0].icon, route: 'x' + i, permissions: [], order: 100 + i }))
    const bar = barFor('index', visibleModules(EVERYTHING, [], [...MODULES, ...extra]), EVERYTHING)
    expect(bar.items).toHaveLength(MAX_BAR_ITEMS - 1)
    expect(bar.hasMore).toBe(true)
    expect(bar.overflow).toEqual([])
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
