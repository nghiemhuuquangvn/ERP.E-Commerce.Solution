import { gatewayClient } from '@/api/client'
import type { ListParams } from '@/hooks/useListFilters'
import { onePage, pageOfArray, type Page } from '@/modules/usePagedList'

// PurchaseOrderService answers these lists as bare arrays (no total) under the Gateway's /po prefix (admin/src/api/purchasing/procurement.api.ts).
const BASE = '/po/api/v1'

export interface Supplier {
  id: string
  code: string
  name: string
  contactPerson?: string | null
  email?: string | null
  phone?: string | null
  paymentTermDays?: number | null
  currencyCode?: string | null
  isActive: boolean
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

export const SUPPLIER_PAGE_SIZE = 50
export const SUPPLIER_INVOICE_STATUSES = ['Recorded', 'Matched', 'OnHold', 'Approved', 'Rejected'] as const
export const VENDOR_RETURN_STATUSES = ['Draft', 'Shipped', 'Credited', 'Cancelled'] as const
export const RFQ_STATUSES = ['Draft', 'Sent', 'Awarded', 'Cancelled'] as const

const windowOf = (p: ListParams) => ({ status: p.status, dateFrom: p.dateFrom, dateTo: p.dateTo, search: p.search })

export const procurementApi = {
  /** `status` is "active" or "inactive" (the server takes isActive true / false). */
  async suppliers(params: ListParams, page: number, pageSize = SUPPLIER_PAGE_SIZE): Promise<Page<Supplier>> {
    const isActive = params.status === 'active' ? true : params.status === 'inactive' ? false : undefined
    const { data } = await gatewayClient.get<Supplier[]>(`${BASE}/suppliers`, { params: { isActive, search: params.search, pageNumber: page, pageSize } })
    return pageOfArray(data, page, pageSize)
  },
  /** Judged on the invoice date. */
  invoices: (params: ListParams) => gatewayClient.get<SupplierInvoiceSummary[]>(`${BASE}/supplier-invoices`, { params: windowOf(params) }).then((r) => onePage(r.data)),
  returns: (params: ListParams) => gatewayClient.get<VendorReturnSummary[]>(`${BASE}/vendor-returns`, { params: windowOf(params) }).then((r) => onePage(r.data)),
  rfqs: (params: ListParams) => gatewayClient.get<RfqSummary[]>(`${BASE}/rfqs`, { params: windowOf(params) }).then((r) => onePage(r.data)),
}
