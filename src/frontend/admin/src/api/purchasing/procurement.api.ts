import { apiClient, PO_BASE } from '../poClient'

// PurchaseOrderService (ROADMAP P1-8): RFQs and quotations, supplier invoices with the three-way match, returns to vendor, match settings.

export interface RfqSummary {
  id: string; rfqNumber: string; title: string; status: string; lines: number; invited: number; quoted: number; neededBy: string | null; createdAt: string
}
export interface RfqLine { id: string; productCode: string; productName: string; quantity: number; unit: string }
export interface RfqInvitation { supplierId: string; supplierName: string; state: string }
export interface QuotationLine { rfqLineId: string; unitPrice: number }
export interface Quotation {
  id: string; supplierId: string; supplierName: string; quoteNumber: string; validUntil: string; currencyCode: string; leadTimeDays: number
  notes: string | null; isWinner: boolean; lines: QuotationLine[]
}
export interface Rfq {
  id: string; rfqNumber: string; title: string; neededBy: string | null; notes: string | null; status: string; sentAt: string | null
  awardedSupplierId: string | null; awardedPurchaseOrderId: string | null; createdAt: string
  lines: RfqLine[]; invitations: RfqInvitation[]; quotations: Quotation[]
}
export interface RfqComparison {
  rfqId: string; rfqNumber: string; status: string
  lines: { lineId: string; productCode: string; productName: string; quantity: number; unit: string; lowestPrice: number | null; prices: { supplierId: string; unitPrice: number; expired: boolean; isLowest: boolean }[] }[]
  suppliers: { supplierId: string; supplierName: string; quoteNumber: string; currencyCode: string; leadTimeDays: number; validUntil: string; expired: boolean; isWinner: boolean; total: number }[]
}
export interface CreateRfqRequest {
  title: string; neededBy: string | null; notes: string | null
  lines: { productCode: string; productName: string; quantity: number; unit: string }[]
  supplierIds: string[]
}
export interface RecordQuotationRequest {
  supplierId: string; quoteNumber: string; validUntil: string; currencyCode: string; leadTimeDays: number; notes: string | null
  unitPrices: Record<string, number>
}

export interface InvoiceMatchLine {
  productCode: string; ordered: number; received: number; returned: number; previouslyInvoiced: number; invoiced: number
  orderPrice: number; invoicedPrice: number; priceVariancePercent: number; quantityVariance: number; verdict: string
}
export interface SupplierInvoiceSummary {
  id: string; invoiceNumber: string; supplierName: string; poNumber: string; invoiceDate: string; dueDate: string; currencyCode: string
  totalAmount: number; status: string; holdReasons: string | null
}
export interface SupplierInvoice {
  id: string; invoiceNumber: string; supplierId: string; supplierName: string; purchaseOrderId: string; poNumber: string; invoiceDate: string; dueDate: string
  currencyCode: string; netAmount: number; taxAmount: number; totalAmount: number; status: string; holdReasons: string | null
  approvalReason: string | null; rejectionReason: string | null; approvedBy: string | null; approvedAt: string | null; receivedValue: number; createdAt: string
  lines: { productCode: string; quantity: number; unitPrice: number }[]
  matchLines: InvoiceMatchLine[]
}
export interface RecordInvoiceRequest {
  purchaseOrderId: string; invoiceNumber: string; invoiceDate: string; dueDate: string | null; currencyCode: string | null; taxAmount: number
  lines: { productCode: string; quantity: number; unitPrice: number }[]
}

export interface OrderProcurementLine {
  productCode: string; productName: string; ordered: number; received: number; rejected: number; returned: number; invoiced: number
  returnable: number; billable: number; unitPrice: number
}
export interface OrderProcurement { purchaseOrderId: string; poNumber: string; supplierId: string; currencyCode: string; lines: OrderProcurementLine[] }

export interface VendorReturnSummary { id: string; returnNumber: string; poNumber: string; supplierName: string; currencyCode: string; status: string; value: number; createdAt: string }
export interface VendorReturn {
  id: string; returnNumber: string; purchaseOrderId: string; poNumber: string; supplierId: string; supplierName: string; currencyCode: string; reason: string
  status: string; value: number; shippedAt: string | null; creditNoteNumber: string | null; creditAmount: number; creditedAt: string | null; createdAt: string
  lines: { productCode: string; productName: string; quantity: number; unitPrice: number }[]
}
export interface PurchasingSettings { quantityTolerancePercent: number; priceTolerancePercent: number; autoApproveMatched: boolean }

export { serverMessage } from '@/lib/serverMessage'

const get = <T,>(url: string, params?: object) => apiClient.get<T>(`${PO_BASE}/${url}`, { params }).then((r) => r.data)
const post = <T,>(url: string, body?: object) => apiClient.post<T>(`${PO_BASE}/${url}`, body).then((r) => r.data)

export const procurementApi = {
  rfqs: (status?: string) => get<RfqSummary[]>('rfqs', { status }),
  rfq: (id: string) => get<Rfq>(`rfqs/${id}`),
  comparison: (id: string) => get<RfqComparison>(`rfqs/${id}/comparison`),
  createRfq: (body: CreateRfqRequest) => post<Rfq>('rfqs', body),
  sendRfq: (id: string) => post<Rfq>(`rfqs/${id}/send`),
  cancelRfq: (id: string) => post<Rfq>(`rfqs/${id}/cancel`),
  recordQuotation: (id: string, body: RecordQuotationRequest) => post<Rfq>(`rfqs/${id}/quotations`, body),
  declineRfq: (id: string, supplierId: string) => post<Rfq>(`rfqs/${id}/suppliers/${supplierId}/decline`),
  awardRfq: (id: string, supplierId: string) => post<Rfq>(`rfqs/${id}/award`, { supplierId }),

  invoices: (params: { status?: string; purchaseOrderId?: string }) => get<SupplierInvoiceSummary[]>('supplier-invoices', params),
  invoice: (id: string) => get<SupplierInvoice>(`supplier-invoices/${id}`),
  recordInvoice: (body: RecordInvoiceRequest) => post<SupplierInvoice>('supplier-invoices', body),
  matchInvoice: (id: string) => post<SupplierInvoice>(`supplier-invoices/${id}/match`),
  approveInvoice: (id: string, reason: string | null) => post<SupplierInvoice>(`supplier-invoices/${id}/approve`, { reason }),
  rejectInvoice: (id: string, reason: string) => post<SupplierInvoice>(`supplier-invoices/${id}/reject`, { reason }),

  orderProcurement: (poId: string) => get<OrderProcurement>(`purchase-orders/${poId}/procurement`),
  receiveGoods: (poId: string, receivedQuantities: Record<string, { quantityAccepted: number; quantityRejected: number }>) =>
    post<unknown>(`purchase-orders/${poId}/receive`, { receivedQuantities }),

  returns: (params: { purchaseOrderId?: string; status?: string }) => get<VendorReturnSummary[]>('vendor-returns', params),
  vendorReturn: (id: string) => get<VendorReturn>(`vendor-returns/${id}`),
  createReturn: (body: { purchaseOrderId: string; reason: string; lines: { productCode: string; quantity: number }[] }) => post<VendorReturn>('vendor-returns', body),
  shipReturn: (id: string) => post<VendorReturn>(`vendor-returns/${id}/ship`),
  cancelReturn: (id: string) => post<VendorReturn>(`vendor-returns/${id}/cancel`),
  creditReturn: (id: string, creditNoteNumber: string, creditAmount: number) => post<VendorReturn>(`vendor-returns/${id}/credit`, { creditNoteNumber, creditAmount }),

  settings: () => get<PurchasingSettings>('purchasing-settings'),
  saveSettings: (body: PurchasingSettings) => apiClient.put<PurchasingSettings>(`${PO_BASE}/purchasing-settings`, body).then((r) => r.data),
}
