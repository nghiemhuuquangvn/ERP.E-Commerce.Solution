import { gatewayClient } from '@/api/client'
import type { ListParams } from '@/hooks/useListFilters'
import { PAGE_SIZE, type Page } from '@/modules/usePagedList'

// PurchaseOrderService answers these lists as pages { items, totalCount, pageNumber, pageSize } under the Gateway's /po prefix
// (admin/src/api/purchasing/procurement.api.ts, suppliers.api.ts).
const BASE = '/po/api/v1'

export interface Supplier {
  id: string
  code: string
  name: string
  contactPerson?: string | null
  email?: string | null
  phone?: string | null
  address?: { street?: string | null; city?: string | null; state?: string | null; postalCode?: string | null; country?: string | null } | null
  taxCode?: string | null
  paymentTermDays?: number | null
  currencyCode?: string | null
  isActive: boolean
}

export interface SupplierRatings {
  averageRating: number
  totalRatings: number
  ratings: { id: string; rating: number; comment?: string | null; ratedBy?: string | null; createdAt?: string | null }[]
}

export interface SupplierItem {
  id: string
  productId: string
  supplierItemCode?: string | null
  lastPurchasePrice: number
  leadTimeDays: number
  isPreferred: boolean
}

export interface SupplierInvoiceSummary {
  id: string
  invoiceNumber: string
  supplierName: string
  poNumber: string
  invoiceDate: string
  dueDate: string | null
  currencyCode: string
  totalAmount: number
  status: string
  holdReasons?: string | null
}

export interface InvoiceMatchLine {
  productCode: string
  ordered: number
  received: number
  returned: number
  previouslyInvoiced: number
  invoiced: number
  orderPrice: number
  invoicedPrice: number
  priceVariancePercent: number
  quantityVariance: number
  verdict: string
}

export interface SupplierInvoice extends SupplierInvoiceSummary {
  supplierId: string
  purchaseOrderId: string
  netAmount: number
  taxAmount: number
  approvalReason?: string | null
  rejectionReason?: string | null
  approvedBy?: string | null
  approvedAt?: string | null
  receivedValue: number
  createdAt: string
  lines: { productCode: string; quantity: number; unitPrice: number }[]
  matchLines: InvoiceMatchLine[]
}

export interface VendorReturnSummary {
  id: string
  returnNumber: string
  poNumber: string
  supplierName: string
  currencyCode: string
  status: string
  value: number
  createdAt: string
}

export interface VendorReturn extends VendorReturnSummary {
  purchaseOrderId: string
  supplierId: string
  reason: string
  shippedAt?: string | null
  creditNoteNumber?: string | null
  creditAmount: number
  creditedAt?: string | null
  lines: { productCode: string; productName: string; quantity: number; unitPrice: number }[]
}

export interface RfqSummary {
  id: string
  rfqNumber: string
  title: string
  status: string
  lines: number
  invited: number
  quoted: number
  neededBy?: string | null
  createdAt: string
}

export interface Rfq {
  id: string
  rfqNumber: string
  title: string
  neededBy?: string | null
  notes?: string | null
  status: string
  sentAt?: string | null
  awardedSupplierId?: string | null
  awardedPurchaseOrderId?: string | null
  createdAt: string
  lines: { id: string; productCode: string; productName: string; quantity: number; unit: string }[]
  invitations: { supplierId: string; supplierName: string; state: string }[]
  quotations: { id: string; supplierId: string; supplierName: string; quoteNumber: string; validUntil: string; currencyCode: string; leadTimeDays: number; isWinner: boolean }[]
}

export interface RfqComparison {
  suppliers: { supplierId: string; supplierName: string; quoteNumber: string; currencyCode: string; leadTimeDays: number; validUntil: string; expired: boolean; isWinner: boolean; total: number }[]
}

export const SUPPLIER_INVOICE_STATUSES = ['Recorded', 'Matched', 'OnHold', 'Approved', 'Rejected'] as const
export const VENDOR_RETURN_STATUSES = ['Draft', 'Shipped', 'Credited', 'Cancelled'] as const
export const RFQ_STATUSES = ['Draft', 'Sent', 'Awarded', 'Cancelled'] as const

interface Paged<T> {
  items: T[]
  totalCount: number
}
const toPage = <T>(d: Paged<T>): Page<T> => ({ items: d.items, total: d.totalCount })
const windowOf = (p: ListParams) => ({ status: p.status, dateFrom: p.dateFrom, dateTo: p.dateTo, search: p.search })
const get = <T>(url: string, params?: object) => gatewayClient.get<T>(`${BASE}/${url}`, { params }).then((r) => r.data)
const post = <T>(url: string, body?: object) => gatewayClient.post<T>(`${BASE}/${url}`, body).then((r) => r.data)

export const procurementApi = {
  /** `status` is "active" or "inactive" (the server takes isActive true / false). */
  suppliers: (params: ListParams, page: number, pageSize = PAGE_SIZE) => {
    const isActive = params.status === 'active' ? true : params.status === 'inactive' ? false : undefined
    return get<Paged<Supplier>>('suppliers', { isActive, search: params.search, pageNumber: page, pageSize }).then(toPage)
  },
  supplier: (id: string) => get<Supplier>(`suppliers/${id}`),
  supplierRatings: (id: string) => get<SupplierRatings>(`suppliers/${id}/ratings`),
  supplierItems: (id: string) => get<SupplierItem[]>(`suppliers/${id}/items`),

  /** Judged on the invoice date. */
  invoices: (params: ListParams, page: number, pageSize = PAGE_SIZE) => get<Paged<SupplierInvoiceSummary>>('supplier-invoices', { ...windowOf(params), pageNumber: page, pageSize }).then(toPage),
  invoice: (id: string) => get<SupplierInvoice>(`supplier-invoices/${id}`),
  approveInvoice: (id: string, reason?: string) => post<SupplierInvoice>(`supplier-invoices/${id}/approve`, { reason: reason ?? null }),
  rejectInvoice: (id: string, reason: string) => post<SupplierInvoice>(`supplier-invoices/${id}/reject`, { reason }),

  returns: (params: ListParams, page: number, pageSize = PAGE_SIZE) => get<Paged<VendorReturnSummary>>('vendor-returns', { ...windowOf(params), pageNumber: page, pageSize }).then(toPage),
  vendorReturn: (id: string) => get<VendorReturn>(`vendor-returns/${id}`),
  shipReturn: (id: string) => post<VendorReturn>(`vendor-returns/${id}/ship`),
  cancelReturn: (id: string) => post<VendorReturn>(`vendor-returns/${id}/cancel`),
  creditReturn: (id: string, creditNoteNumber: string, creditAmount: number) => post<VendorReturn>(`vendor-returns/${id}/credit`, { creditNoteNumber, creditAmount }),

  rfqs: (params: ListParams, page: number, pageSize = PAGE_SIZE) => get<Paged<RfqSummary>>('rfqs', { ...windowOf(params), pageNumber: page, pageSize }).then(toPage),
  rfq: (id: string) => get<Rfq>(`rfqs/${id}`),
  rfqComparison: (id: string) => get<RfqComparison>(`rfqs/${id}/comparison`),
}
