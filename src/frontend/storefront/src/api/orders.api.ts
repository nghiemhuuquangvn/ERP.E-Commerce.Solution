import { apiClient } from './client'

export interface CartLine {
  productId: string
  productName: string
  unitPrice: number
  quantity: number
}

/** The shape SaleOrderService reads (CreateOrderRequest): `lineItems`, not `lines`, and the customer's name and e-mail. */
export interface CreateOrderRequest {
  customerId: string
  customerName: string
  customerEmail: string
  /** The recipient's phone: the carrier will not take a parcel without it. */
  customerPhone?: string
  shippingAddress: {
    street: string
    /** Ward (phuong / xa): GHTK and Viettel Post need it to book the parcel. */
    ward?: string
    city: string
    state: string
    postalCode: string
    country: string
  }
  lineItems: CartLine[]
  couponCode?: string
  /** COD, VNPay, MoMo or ZaloPay — one of the methods the shop offers (GET payment-gateway/methods). */
  paymentMethod?: string
}

/** The shape SaleOrderService returns (SalesOrderDto). */
export interface Order {
  orderId: string
  orderNo: string
  status: string
  /** What the customer owes: the goods after any coupon plus the delivery fee. */
  totalAmount: number
  /** The delivery fee already inside totalAmount; 0 = free (or an order from before it existed). */
  shippingFee?: number
  orderDate: string
  paymentMethod?: string | null
  lineItems: CartLine[]
}

/** One row of GET /v1/orders/my (SalesOrderSummaryDto): no line items - those come with GET /v1/orders/{id}. */
export type OrderSummary = Omit<Order, 'lineItems' | 'paymentMethod'> & { customerName: string }

export const ordersApi = {
  create: (body: CreateOrderRequest) =>
    apiClient.post<Order>('/v1/orders', body).then((r) => r.data),

  myOrders: () =>
    apiClient.get<OrderSummary[]>('/v1/orders/my').then((r) => r.data),

  get: (id: string) =>
    apiClient.get<Order>(`/v1/orders/${id}`).then((r) => r.data),
}
