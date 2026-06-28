import { apiClient } from './client'

export interface CartLine {
  productId: string
  productName: string
  unitPrice: number
  quantity: number
}

export interface CreateOrderRequest {
  customerId: string
  shippingAddress: {
    street: string
    city: string
    state: string
    postalCode: string
    country: string
  }
  lines: CartLine[]
  couponCode?: string
}

export interface Order {
  id: string
  orderNumber: string
  status: string
  totalAmount: number
  orderDate: string
  lines: Array<CartLine & { lineTotal: number }>
}

export const ordersApi = {
  create: (body: CreateOrderRequest) =>
    apiClient.post<Order>('/v1/orders', body).then((r) => r.data),

  myOrders: () =>
    apiClient.get<Order[]>('/v1/orders/my').then((r) => r.data),

  get: (id: string) =>
    apiClient.get<Order>(`/v1/orders/${id}`).then((r) => r.data),
}
