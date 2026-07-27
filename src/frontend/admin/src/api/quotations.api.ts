import { apiClient, SO_BASE } from './soClient'
import type { Address, SalesOrder } from './orders.api'

export type QuotationStatus = 'Draft' | 'Sent' | 'Accepted' | 'Rejected' | 'Expired' | 'Converted'

export interface QuotationLineItem {
  productId: string
  productName: string
  quantity: number
  unitPrice: number
}

export interface SalesQuotation {
  quotationId: string
  customerId: string
  customerName: string
  customerEmail: string
  quotationNo: string
  status: QuotationStatus
  validUntil: string
  totalAmount: number
  convertedOrderId: string | null
  lineItems: QuotationLineItem[]
}

export interface SalesQuotationSummary {
  quotationId: string
  customerId: string
  customerName: string
  quotationNo: string
  status: QuotationStatus
  totalAmount: number
  validUntil: string
}

export interface ListQuotationsResponse {
  total: number
  items: SalesQuotationSummary[]
}

export interface CreateQuotationRequest {
  customerId: string
  customerName: string
  customerEmail: string
  lineItems: QuotationLineItem[]
  validUntil: string
}

export const quotationsApi = {
  list: (params: { customerId?: string; page?: number; pageSize?: number }) =>
    apiClient.get<ListQuotationsResponse>(`${SO_BASE}/quotations`, { params }).then((r) => r.data),

  get: (id: string) => apiClient.get<SalesQuotation>(`${SO_BASE}/quotations/${id}`).then((r) => r.data),

  create: (body: CreateQuotationRequest) =>
    apiClient.post<SalesQuotation>(`${SO_BASE}/quotations`, body).then((r) => r.data),

  send: (id: string) => apiClient.post<SalesQuotation>(`${SO_BASE}/quotations/${id}/send`).then((r) => r.data),

  accept: (id: string) => apiClient.post<SalesQuotation>(`${SO_BASE}/quotations/${id}/accept`).then((r) => r.data),

  reject: (id: string) => apiClient.post<SalesQuotation>(`${SO_BASE}/quotations/${id}/reject`).then((r) => r.data),

  convertToOrder: (id: string, shippingAddress: Address) =>
    apiClient
      .post<SalesOrder>(`${SO_BASE}/quotations/${id}/convert-to-order`, { shippingAddress })
      .then((r) => r.data),
}
