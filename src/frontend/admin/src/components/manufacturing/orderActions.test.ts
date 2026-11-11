import { describe, expect, it } from 'vitest'
import type { OrderStatus } from '@/api/manufacturing/mfg.api'
import { ORDER_PERMISSION, canBulkRelease, errorCodeOf, orderActions, type OrderActions } from './orderActions'

const ALL = Object.values(ORDER_PERMISSION) as string[]
const everyone = (p: string) => ALL.includes(p)
const nobody = () => false
const viewOnly = (p: string) => p === ORDER_PERMISSION.view

const names = (a: OrderActions) => (Object.keys(a) as (keyof OrderActions)[]).filter((k) => a[k]).sort()

describe('orderActions (TC-UI-001): the buttons follow the status, then the permissions', () => {
  const expected: Record<OrderStatus, (keyof OrderActions)[]> = {
    Draft: ['cancel', 'edit', 'release', 'retryDocuments', 'viewQuality'],
    Released: ['cancel', 'changeQuantity', 'hold', 'issue', 'operationManual', 'qcRelease', 'receive', 'retryDocuments', 'returnMaterial', 'reverseReceipt', 'reverseReport', 'unrelease', 'viewQuality', 'work'],
    InProgress: ['changeQuantity', 'complete', 'hold', 'issue', 'operationManual', 'qcRelease', 'receive', 'retryDocuments', 'returnMaterial', 'reverseReceipt', 'reverseReport', 'viewQuality', 'work'],
    OnHold: ['qcRelease', 'resume', 'retryDocuments', 'viewQuality'],
    Completed: ['close', 'qcRelease', 'reopen', 'retryDocuments', 'returnMaterial', 'reverseReceipt', 'viewQuality'],
    Closed: ['qcRelease', 'retryDocuments', 'viewQuality'],
    Cancelled: ['retryDocuments', 'viewQuality'],
  }

  for (const status of Object.keys(expected) as OrderStatus[]) {
    it(`${status}: a user holding every permission sees exactly the right buttons`, () => {
      expect(names(orderActions(status, everyone))).toEqual([...expected[status]].sort())
    })
    it(`${status}: nobody sees any button, a viewer neither`, () => {
      expect(names(orderActions(status, nobody))).toEqual([])
      expect(names(orderActions(status, viewOnly))).toEqual([])
    })
  }

  it('a Draft is released and cancelled by different permissions', () => {
    const has = (p: string) => p === ORDER_PERMISSION.release
    expect(names(orderActions('Draft', has))).toEqual(['release'])
  })

  it('a return is accepted on a Completed order but an issue is not', () => {
    const a = orderActions('Completed', everyone)
    expect(a.returnMaterial).toBe(true)
    expect(a.issue).toBe(false)
  })

  it('a report is reversed only while the order is open for work, a receipt also on a Completed order (it reopens by itself)', () => {
    expect(orderActions('Completed', everyone).reverseReport).toBe(false)
    expect(orderActions('Completed', everyone).reverseReceipt).toBe(true)
    expect(orderActions('InProgress', (p) => p === ORDER_PERMISSION.reverseReport).reverseReceipt).toBe(false)
    expect(orderActions('InProgress', (p) => p === ORDER_PERMISSION.reverseReceipt).reverseReport).toBe(false)
  })

  it('completing needs the manual-complete or the short-close permission', () => {
    expect(orderActions('InProgress', (p) => p === ORDER_PERMISSION.shortClose).complete).toBe(true)
    expect(orderActions('InProgress', (p) => p === ORDER_PERMISSION.completeManual).complete).toBe(true)
    expect(orderActions('InProgress', (p) => p === ORDER_PERMISSION.issue).complete).toBe(false)
  })

  it('only a Draft can be selected for bulk release', () => {
    expect(canBulkRelease('Draft')).toBe(true)
    expect(canBulkRelease('Released')).toBe(false)
  })

  it('reads the error code of a refusal', () => {
    expect(errorCodeOf({ response: { data: { errorCode: 'MFG_PO_012' } } })).toBe('MFG_PO_012')
    expect(errorCodeOf({ response: { data: 'text' } })).toBeNull()
    expect(errorCodeOf(null)).toBeNull()
  })
})
