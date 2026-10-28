import { apiClient, ACC_BASE } from './accClient'

// InvoiceDto — note Status is a string on the wire (backend does
// `((InvoiceStatus)i.Status).ToString()` when mapping), even though the list
// filter query param below is the underlying int enum value.
export interface InvoiceDto {
  id: string
  invoiceNumber: string
  customerId: string
  orderId: string
  /** The gross the customer owes; netAmount + taxAmount. */
  amount: number
  netAmount: number
  taxRatePercent: number
  taxAmount: number
  /** The delivery part of the net amount, booked on its own revenue account (ROADMAP P1-11c). */
  shippingNetAmount?: number
  dueDate: string
  status: string
  createdAt: string
  /** False when the journal entry could not be posted (no open fiscal period …); see the posting gaps. */
  postedToLedger?: boolean
  postingWarning?: string | null
  /** Set for a document in a foreign currency (ROADMAP P1-4): amount is in the base currency. */
  foreignCurrencyCode?: string | null
  exchangeRate?: number | null
  foreignAmount?: number | null
}

/** A VAT line as the API takes it (ROADMAP P1-3b): category 0 taxable / 1 not subject to VAT. */
export interface VatLineInput {
  category: number
  ratePercent: number
  amount: number
  description?: string
}

/** The e-invoice of an invoice as the provider answered (ROADMAP P1-3). */
export interface EInvoiceDto {
  id: string
  invoiceId: string
  provider: string
  status: 'Issued' | 'Cancelled' | 'Failed' | 'Replaced'
  kind: 'Original' | 'Replacement' | 'Adjustment'
  reason: string | null
  adjustNet: number | null
  adjustTax: number | null
  series: string
  number: string | null
  lookupCode: string | null
  issuedAt: string | null
  buyerName: string
  buyerTaxCode: string | null
  buyerAddress: string | null
  error: string | null
  cancelReason: string | null
  cancelledAt: string | null
}

// InvoiceStatus enum (ERP.ACC.Domain.Enums.InvoiceStatus): Unpaid=0, Paid=1, Overdue=2
export const INVOICE_STATUS = {
  Unpaid: 0,
  Paid: 1,
  Overdue: 2,
} as const

export interface ListInvoicesParams {
  customerId?: string
  status?: number
  pageNumber?: number
  pageSize?: number
}

// CreateInvoiceCommand(InvoiceNumber, CustomerId, OrderId, Amount, DueDate) — no update endpoint exists.
export interface CreateInvoiceRequest {
  invoiceNumber: string
  customerId: string
  orderId: string
  amount: number
  dueDate: string
  taxRatePercent?: number
  lines?: VatLineInput[]
  /** A document in a foreign currency: the currency, the total in it and the rate (base per 1 foreign). */
  currencyCode?: string
  foreignAmount?: number
  exchangeRate?: number
}

export interface ARAgingDto {
  customerId: string
  current: number
  days30: number
  days60: number
  days90Plus: number
  totalOutstanding: number
}

export const invoicesApi = {
  // Plain array response — NOT a PagedResult wrapper (see accClient.ts note).
  list: (params: ListInvoicesParams) =>
    apiClient.get<InvoiceDto[]>(`${ACC_BASE}/invoices`, { params }).then((r) => r.data),

  get: (id: string) => apiClient.get<InvoiceDto>(`${ACC_BASE}/invoices/${id}`).then((r) => r.data),

  create: (body: CreateInvoiceRequest) =>
    apiClient.post<InvoiceDto>(`${ACC_BASE}/invoices`, body).then((r) => r.data),

  adjustVat: (id: string, taxRatePercent: number, lines?: VatLineInput[]) =>
    apiClient.post<InvoiceDto>(`${ACC_BASE}/invoices/${id}/adjust-vat`, { taxRatePercent, lines: lines ?? [] }).then((r) => r.data),

  eInvoices: (id: string) => apiClient.get<EInvoiceDto[]>(`${ACC_BASE}/invoices/${id}/e-invoices`).then((r) => r.data),

  replaceEInvoice: (id: string, body: { reason: string; buyerName?: string; buyerTaxCode?: string; buyerAddress?: string; taxRatePercent?: number }) =>
    apiClient.post<EInvoiceDto>(`${ACC_BASE}/invoices/${id}/e-invoice/replace`, body).then((r) => r.data),

  adjustEInvoice: (id: string, body: { adjustNet: number; adjustTax: number; reason: string }) =>
    apiClient.post<EInvoiceDto>(`${ACC_BASE}/invoices/${id}/e-invoice/adjust`, body).then((r) => r.data),

  eInvoiceProviders: () => apiClient.get<string[]>(`${ACC_BASE}/invoices/e-invoice-providers`).then((r) => r.data),

  eInvoice: (id: string) =>
    apiClient.get<EInvoiceDto>(`${ACC_BASE}/invoices/${id}/e-invoice`, { validateStatus: (s) => s === 200 || s === 404 }).then((r) => (r.status === 200 ? r.data : null)),

  issueEInvoice: (id: string, buyer: { buyerName?: string; buyerTaxCode?: string; buyerAddress?: string }) =>
    apiClient.post<EInvoiceDto>(`${ACC_BASE}/invoices/${id}/e-invoice`, buyer).then((r) => r.data),

  cancelEInvoice: (id: string, reason: string) =>
    apiClient.post<EInvoiceDto>(`${ACC_BASE}/invoices/${id}/e-invoice/cancel`, { reason }).then((r) => r.data),

  markPaid: (id: string, paymentRate?: number) =>
    apiClient.post<InvoiceDto>(`${ACC_BASE}/invoices/${id}/mark-paid`, { paymentRate }).then((r) => r.data),

  arAging: (asOfDate?: string) =>
    apiClient
      .get<ARAgingDto[]>(`${ACC_BASE}/invoices/ar-aging`, { params: { asOfDate } })
      .then((r) => r.data),
}
