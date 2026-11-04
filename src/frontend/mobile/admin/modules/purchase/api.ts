import { gatewayClient } from '@/api/client'
import type { Page } from '@/modules/usePagedList'
import { PAGE_SIZE } from '@/modules/usePagedList'

// PurchaseOrderService is mounted at the Gateway's /po prefix (same paths as the Admin web: admin/src/api/purchaseOrders.api.ts).
const BASE = '/po/api/v1/purchase-orders'

export type PoStatus =
  | 'DRAFT' | 'SUBMITTED' | 'APPROVED' | 'ORDERED' | 'PARTIALLY_RECEIVED' | 'RECEIVED' | 'CANCELLED' | 'REJECTED' | 'PENDING_FINANCE_APPROVAL'

export interface PoItem {
  id: string
  productCode: string
  productName: string
  quantity: number
  unit: string
  unitPrice: number
  totalPrice: number
}

export interface PurchaseOrder {
  id: string
  poNumber: string
  supplier?: { id: string; name: string }
  status: PoStatus
  orderDate: string
  expectedDeliveryDate?: string
  totalAmount: number
  currency: string
  notes?: string
  items: PoItem[]
}

interface PoPage {
  items: PurchaseOrder[]
  totalCount: number
}

/** How many orders the count badge on Home looks at (the list endpoint gives no total). */
export const COUNT_LIMIT = 100

export const purchaseApi = {
  /**
   * The server answers a bare array (no total) today; a { items, totalCount } page is accepted too. Without a total, a full page
   * means "there may be another page" (the total is reported as one more than what was loaded), a short page means "that was the end".
   */
  async list(status: PoStatus | undefined, page: number, pageSize = PAGE_SIZE): Promise<Page<PurchaseOrder>> {
    const { data } = await gatewayClient.get<PurchaseOrder[] | PoPage>(BASE, { params: { status, pageNumber: page, pageSize } })
    if (!Array.isArray(data)) return { items: data.items, total: data.totalCount }
    const before = (page - 1) * pageSize
    return { items: data, total: data.length < pageSize ? before + data.length : before + data.length + 1 }
  },
  get: (id: string) => gatewayClient.get<PurchaseOrder>(`${BASE}/${id}`).then((r) => r.data),
  /** How many orders wait in a status, counted from the first COUNT_LIMIT rows (the list gives no total). */
  count: (status: PoStatus) => purchaseApi.list(status, 1, COUNT_LIMIT).then((p) => p.items.length),
  approve: (id: string) => gatewayClient.post(`${BASE}/${id}/approve`).then((r) => r.data),
  approveFinance: (id: string) => gatewayClient.post(`${BASE}/${id}/finance-approve`).then((r) => r.data),
  reject: (id: string, reason: string) => gatewayClient.post(`${BASE}/${id}/reject`, { reason }).then((r) => r.data),
  cancel: (id: string) => gatewayClient.post(`${BASE}/${id}/cancel`).then((r) => r.data),
}

export type PoActionKey = 'approve' | 'financeApprove' | 'reject' | 'cancel'

/**
 * Which actions a purchase order offers: the status decides what is possible, the permission decides who may
 * (purchase_order:approve, :finance_approve, :cancel - reject uses the approve permission). The server checks both again.
 */
export function poActions(status: PoStatus, has: (permission: string) => boolean): PoActionKey[] {
  const out: PoActionKey[] = []
  if (status === 'SUBMITTED') {
    if (has('purchase_order:approve')) out.push('approve', 'reject')
  } else if (status === 'PENDING_FINANCE_APPROVAL') {
    if (has('purchase_order:finance_approve')) out.push('financeApprove')
    if (has('purchase_order:approve')) out.push('reject')
  }
  if (['SUBMITTED', 'PENDING_FINANCE_APPROVAL', 'APPROVED'].includes(status) && has('purchase_order:cancel')) out.push('cancel')
  return out
}
