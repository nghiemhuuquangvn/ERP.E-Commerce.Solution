import { apiClient, gatewayClient } from '@/api/client'
import type { ListParams } from '@/hooks/useListFilters'
import type { Page } from '@/modules/usePagedList'
import { onePage, PAGE_SIZE } from '@/modules/usePagedList'

// SaleOrderService answers under /api/v1/orders (Gateway route, same as admin/src/api/orders.api.ts).
export type SoStatus =
  | 'Draft' | 'Submitted' | 'Approved' | 'Picked' | 'Shipped' | 'PartiallyShipped' | 'Delivered' | 'Rejected' | 'Cancelled' | 'ReturnRequested' | 'Returned'

/** Every status an order can be filtered by, in the order of its life. */
export const SO_STATUSES: SoStatus[] = ["Draft", "Submitted", "Approved", "Picked", "Shipped", "PartiallyShipped", "Delivered", "Rejected", "Cancelled", "ReturnRequested", "Returned"]

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
  /** One page; the server filters (status, order days, number / customer text), counts and pages in the database. */
  async list(params: ListParams, page: number, pageSize = PAGE_SIZE): Promise<Page<SalesOrderSummary>> {
    const { data } = await apiClient.get<{ total: number; items: SalesOrderSummary[] }>('/v1/orders', { params: { ...params, page, pageSize } })
    return { items: data.items, total: data.total }
  },
  get: (id: string) => apiClient.get<SalesOrder>(`/v1/orders/${id}`).then((r) => r.data),
  count: (status: SoStatus) => salesApi.list({ status }, 1, 1).then((p) => p.total),
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

export const QUOTATION_STATUSES = ['Draft', 'Sent', 'Accepted', 'Rejected', 'Expired', 'Converted'] as const

export interface QuotationSummary {
  quotationId: string
  customerName: string
  quotationNo: string
  status: string
  totalAmount: number
  validUntil: string
  quotationDate?: string
}

export interface CommissionRecord {
  id: string
  planId: string
  salesRepId: string
  orderId: string
  amount: number
  status: string
  calculatedAt: string
}

// Quotations and commissions sit under the Gateway's /so prefix (admin/src/api/quotations.api.ts, commissions.api.ts).
export const salesExtrasApi = {
  /** One page; the server filters (status, days the quotation was made, number / customer text), counts and pages in the database. */
  async quotations(params: ListParams, page: number, pageSize = PAGE_SIZE): Promise<Page<QuotationSummary>> {
    const { data } = await gatewayClient.get<{ total: number; items: QuotationSummary[] }>('/so/api/v1/quotations', { params: { ...params, page, pageSize } })
    return { items: data.items, total: data.total }
  },
  /** The endpoint answers a bare array; its range is two date-times, so the last day runs to the end of the day. */
  commissions: (params: ListParams) =>
    gatewayClient
      .get<CommissionRecord[]>('/so/api/v1/commissions', { params: { from: params.dateFrom, to: params.dateTo ? params.dateTo + 'T23:59:59' : undefined } })
      .then((r) => onePage(r.data)),
}
