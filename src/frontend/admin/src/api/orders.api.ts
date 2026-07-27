import { apiClient } from './client'

// Real contract of SaleOrderService's SalesOrdersController (api/v1/orders) — reconciled
// 2026-07-27 against the actual SalesOrderDto/ListOrdersResponse shape (the previous version
// of this file used an entirely different, never-real contract: orderNumber/lines/a 6-value
// OrderStatus enum that doesn't exist in the backend).

export type OrderStatus =
  | 'Draft'
  | 'Submitted'
  | 'Approved'
  | 'Picked'
  | 'Shipped'
  | 'PartiallyShipped'
  | 'Delivered'
  | 'Rejected'
  | 'Cancelled'
  | 'ReturnRequested'
  | 'Returned'

export type OrderSource = 'Manual' | 'Ecommerce' | 'POS'

export interface Address {
  street: string
  city: string
  state: string
  postalCode: string
  country: string
}

export interface OrderLineItem {
  productId: string
  productName: string
  quantity: number
  unitPrice: number
  shippedQuantity: number
}

export interface OrderNote {
  note: string
  addedBy: string | null
  createdAt: string
}

export interface OrderStatusHistoryEntry {
  fromStatus: string
  toStatus: string
  reason: string | null
  changedBy: string | null
  changedAt: string
}

export interface SalesOrder {
  orderId: string
  customerId: string
  customerName: string
  customerEmail: string
  status: OrderStatus
  totalAmount: number
  lineItems: OrderLineItem[]
  shippingAddress: Address
  notes: string | null
  orderDate: string
  approvedAt: string | null
  shippedAt: string | null
  deliveredAt: string | null
  cancelledAt: string | null
  trackingNumber: string | null
  carrier: string | null
  hasPriceChangeWarning: boolean
  returnRequestedAt: string | null
  returnedAt: string | null
  returnReason: string | null
  refundAmount: number | null
  internalNotes: OrderNote[]
  statusHistory: OrderStatusHistoryEntry[]
  orderNo: string
  orderSource: OrderSource
  departmentId: string | null
  salesRepId: string | null
  quotationId: string | null
  promotionId: string | null
  discountAmount: number
}

export interface SalesOrderSummary {
  orderId: string
  customerId: string
  customerName: string
  status: OrderStatus
  totalAmount: number
  orderDate: string
  orderNo: string
  salesRepId: string | null
  departmentId: string | null
}

export interface ListOrdersParams {
  page?: number
  pageSize?: number
  customerId?: string
  status?: OrderStatus
}

export interface ListOrdersResponse {
  total: number
  items: SalesOrderSummary[]
}

export interface CreateOrderRequest {
  customerId: string
  customerName: string
  customerEmail: string
  lineItems: Array<{ productId: string; productName: string; quantity: number; unitPrice: number }>
  shippingAddress: Address
  notes?: string
  couponCode?: string
  orderSource?: OrderSource
  departmentId?: string
  salesRepId?: string
}

export const ordersApi = {
  list: (params: ListOrdersParams) =>
    apiClient.get<ListOrdersResponse>('/v1/orders', { params }).then((r) => r.data),

  get: (id: string) => apiClient.get<SalesOrder>(`/v1/orders/${id}`).then((r) => r.data),

  create: (body: CreateOrderRequest) =>
    apiClient.post<SalesOrder>('/v1/orders', body).then((r) => r.data),

  update: (id: string, body: { shippingAddress?: Address; notes?: string }) =>
    apiClient.put<SalesOrder>(`/v1/orders/${id}`, body).then((r) => r.data),

  addNote: (id: string, note: string) => apiClient.post(`/v1/orders/${id}/notes`, { note }),

  submit: (id: string) => apiClient.post<SalesOrder>(`/v1/orders/${id}/submit`).then((r) => r.data),

  approve: (id: string) => apiClient.post<SalesOrder>(`/v1/orders/${id}/approve`).then((r) => r.data),

  reject: (id: string, reason: string) =>
    apiClient.post<SalesOrder>(`/v1/orders/${id}/reject`, { reason }).then((r) => r.data),

  pick: (id: string) => apiClient.post<SalesOrder>(`/v1/orders/${id}/pick`).then((r) => r.data),

  ship: (id: string, trackingNumber: string, carrier: string) =>
    apiClient.post<SalesOrder>(`/v1/orders/${id}/ship`, { trackingNumber, carrier }).then((r) => r.data),

  shipItems: (id: string, quantities: Record<string, number>, trackingNumber: string, carrier: string) =>
    apiClient
      .post<SalesOrder>(`/v1/orders/${id}/ship-items`, { quantities, trackingNumber, carrier })
      .then((r) => r.data),

  cancel: (id: string, reason: string) =>
    apiClient.post<SalesOrder>(`/v1/orders/${id}/cancel`, { reason }).then((r) => r.data),

  markDelivered: (id: string) =>
    apiClient.post<SalesOrder>(`/v1/orders/${id}/mark-delivered`).then((r) => r.data),

  requestReturn: (id: string, reason: string) =>
    apiClient.post<SalesOrder>(`/v1/orders/${id}/return`, { reason }).then((r) => r.data),

  completeReturn: (id: string, refundAmount: number) =>
    apiClient.post<SalesOrder>(`/v1/orders/${id}/return/complete`, { refundAmount }).then((r) => r.data),
}
