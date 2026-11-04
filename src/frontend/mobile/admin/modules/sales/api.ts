import { apiClient } from '@/api/client'
import type { Page } from '@/modules/usePagedList'
import { PAGE_SIZE } from '@/modules/usePagedList'

// SaleOrderService answers under /api/v1/orders (Gateway route, same as admin/src/api/orders.api.ts).
export type SoStatus =
  | 'Draft' | 'Submitted' | 'Approved' | 'Picked' | 'Shipped' | 'PartiallyShipped' | 'Delivered' | 'Rejected' | 'Cancelled' | 'ReturnRequested' | 'Returned'

export interface SalesOrderSummary {
  orderId: string
  orderNo: string
  customerName: string
  status: SoStatus
  totalAmount: number
  orderDate: string
  isAnomalous: boolean
  anomalyReason: string | null
}

export interface SalesOrder extends SalesOrderSummary {
  customerEmail: string
  lineItems: { productId: string; productName: string; quantity: number; unitPrice: number; shippedQuantity: number }[]
  shippingAddress: { street: string; ward?: string; city: string; state: string; postalCode: string; country: string }
  notes: string | null
  discountAmount: number
  shippingFee?: number
  trackingNumber: string | null
  carrier: string | null
}

export const salesApi = {
  async list(status: SoStatus | undefined, page: number, pageSize = PAGE_SIZE): Promise<Page<SalesOrderSummary>> {
    const { data } = await apiClient.get<{ total: number; items: SalesOrderSummary[] }>('/v1/orders', { params: { status, page, pageSize } })
    return { items: data.items, total: data.total }
  },
  get: (id: string) => apiClient.get<SalesOrder>(`/v1/orders/${id}`).then((r) => r.data),
  count: (status: SoStatus) => salesApi.list(status, 1, 1).then((p) => p.total),
  approve: (id: string) => apiClient.post(`/v1/orders/${id}/approve`).then((r) => r.data),
  reject: (id: string, reason: string) => apiClient.post(`/v1/orders/${id}/reject`, { reason }).then((r) => r.data),
  cancel: (id: string, reason: string) => apiClient.post(`/v1/orders/${id}/cancel`, { reason }).then((r) => r.data),
}

export type SoActionKey = 'approve' | 'reject' | 'cancel'

/** Status decides what is possible, the permission who may (sales_order:approve for approve / reject, sales_order:cancel). The server checks both again. */
export function soActions(status: SoStatus, has: (permission: string) => boolean): SoActionKey[] {
  const out: SoActionKey[] = []
  if (status === 'Submitted' && has('sales_order:approve')) out.push('approve', 'reject')
  if (['Submitted', 'Approved', 'Picked'].includes(status) && has('sales_order:cancel')) out.push('cancel')
  return out
}
