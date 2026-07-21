import { apiClient, ACC_BASE } from './accClient'

// InvoiceDto — note Status is a string on the wire (backend does
// `((InvoiceStatus)i.Status).ToString()` when mapping), even though the list
// filter query param below is the underlying int enum value.
export interface InvoiceDto {
  id: string
  invoiceNumber: string
  customerId: string
  orderId: string
  amount: number
  dueDate: string
  status: string
  createdAt: string
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

  markPaid: (id: string) =>
    apiClient.post<InvoiceDto>(`${ACC_BASE}/invoices/${id}/mark-paid`).then((r) => r.data),

  arAging: (asOfDate?: string) =>
    apiClient
      .get<ARAgingDto[]>(`${ACC_BASE}/invoices/ar-aging`, { params: { asOfDate } })
      .then((r) => r.data),
}
