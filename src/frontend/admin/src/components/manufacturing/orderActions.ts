import type { OrderStatus } from '@/api/manufacturing/mfg.api'

/** Which buttons of the order screen a user sees (TC-UI-001): decided by the order's status first, then by the permissions. The server stays the authority. */
export interface OrderActions {
  edit: boolean
  release: boolean
  unrelease: boolean
  cancel: boolean
  hold: boolean
  resume: boolean
  changeQuantity: boolean
  issue: boolean
  returnMaterial: boolean
  receive: boolean
  complete: boolean
  reopen: boolean
  close: boolean
  /** Start / pause / resume / stop a time log, add time, report an operation. */
  work: boolean
  /** Complete / reopen one operation by hand. */
  operationManual: boolean
  qcRelease: boolean
  /** The Quality tab (inspections of the order): needs manufacturing:qc:view. */
  viewQuality: boolean
  retryDocuments: boolean
  /** Reverse an operation report (P4-2): the order must be open for work; a Completed order is reopened first. */
  reverseReport: boolean
  /** Reverse a finished-goods receipt (P4-2): also on a Completed order (it reopens by itself when it falls below the threshold). */
  reverseReceipt: boolean
}

export const ORDER_PERMISSION = {
  view: 'manufacturing:order:view',
  create: 'manufacturing:order:create',
  release: 'manufacturing:order:release',
  cancel: 'manufacturing:order:cancel',
  hold: 'manufacturing:order:hold',
  changeQty: 'manufacturing:order:change-qty',
  shortClose: 'manufacturing:order:short-close',
  reopen: 'manufacturing:order:reopen',
  close: 'manufacturing:order:close',
  issue: 'manufacturing:material:issue',
  returnMaterial: 'manufacturing:material:return',
  receipt: 'manufacturing:receipt:post',
  execute: 'manufacturing:operation:execute',
  completeManual: 'manufacturing:operation:complete-manual',
  qc: 'manufacturing:qc:disposition',
  qcView: 'manufacturing:qc:view',
  retry: 'manufacturing:stock-document:retry',
  reverseReport: 'manufacturing:operation:reverse',
  reverseReceipt: 'manufacturing:receipt:reverse',
} as const

const NONE: OrderActions = {
  edit: false, release: false, unrelease: false, cancel: false, hold: false, resume: false, changeQuantity: false, issue: false, returnMaterial: false, receive: false,
  complete: false, reopen: false, close: false, work: false, operationManual: false, qcRelease: false, viewQuality: false, retryDocuments: false, reverseReport: false, reverseReceipt: false,
}

export function orderActions(status: OrderStatus, has: (permission: string) => boolean): OrderActions {
  const p = ORDER_PERMISSION
  const open = status === 'Released' || status === 'InProgress'
  return {
    ...NONE,
    edit: status === 'Draft' && has(p.create),
    release: status === 'Draft' && has(p.release),
    unrelease: status === 'Released' && has(p.release),
    cancel: (status === 'Draft' || status === 'Released') && has(p.cancel),
    hold: open && has(p.hold),
    resume: status === 'OnHold' && has(p.hold),
    changeQuantity: open && has(p.changeQty),
    issue: open && has(p.issue),
    returnMaterial: (open || status === 'Completed') && has(p.returnMaterial),   // a return is accepted on a Completed order so issued material can be settled
    receive: open && has(p.receipt),
    complete: status === 'InProgress' && (has(p.completeManual) || has(p.shortClose)),
    reopen: status === 'Completed' && has(p.reopen),
    close: status === 'Completed' && has(p.close),
    work: open && has(p.execute),
    operationManual: open && has(p.completeManual),
    // the interim quality release and the document retry do not depend on the order's status
    qcRelease: status !== 'Draft' && status !== 'Cancelled' && has(p.qc),
    viewQuality: has(p.qcView),
    retryDocuments: has(p.retry),
    reverseReport: open && has(p.reverseReport),
    reverseReceipt: (open || status === 'Completed') && has(p.reverseReceipt),
  }
}

/** The statuses an order can be released from the list (Draft only). */
export const canBulkRelease = (status: OrderStatus) => status === 'Draft'

/** What the server may refuse with, by code, and the screen reacts to (the text itself comes from the server). */
export const SHORT_CLOSE_REFUSAL = 'MFG_PO_012'

/** The error code of a refused request (ProblemDetails `errorCode`), or null. */
export function errorCodeOf(e: unknown): string | null {
  const data = (e as { response?: { data?: { errorCode?: string } } } | null)?.response?.data
  return typeof data === 'object' && data && typeof data.errorCode === 'string' ? data.errorCode : null
}
