import { apiClient, PaginatedResponse } from './client'

export type OrderStatus = 'Draft' | 'Confirmed' | 'Processing' | 'Shipped' | 'Delivered' | 'Cancelled'

export interface OrderLine {
  id: string
  productId: string
  productName: string
  quantity: number
  unitPrice: number
  lineTotal: number
}

export interface Order {
  id: string
  orderNumber: string
  customerId: string
  customerName: string
  status: OrderStatus
  totalAmount: number
  orderDate: string
  lines: OrderLine[]
}

export interface ListOrdersParams {
  page?: number
  pageSize?: number
  status?: OrderStatus
  search?: string
}

export interface CreateOrderRequest {
  customerId: string
  lines: Array<{ productId: string; quantity: number; unitPrice: number }>
}

export const ordersApi = {
  list: (params: ListOrdersParams) =>
    apiClient.get<PaginatedResponse<Order>>('/v1/orders', { params }).then((r) => r.data),

  get: (id: string) =>
    apiClient.get<Order>(`/v1/orders/${id}`).then((r) => r.data),

  create: (body: CreateOrderRequest) =>
    apiClient.post<Order>('/v1/orders', body).then((r) => r.data),

  approve: (id: string) =>
    apiClient.post<Order>(`/v1/orders/${id}/approve`).then((r) => r.data),

  cancel: (id: string, reason: string) =>
    apiClient.post(`/v1/orders/${id}/cancel`, { reason }),
}
