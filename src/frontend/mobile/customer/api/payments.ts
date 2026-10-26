import { apiClient } from '@/api/client'

export interface PaymentMethodOption {
  code: 'COD' | 'VNPay' | 'MoMo' | 'ZaloPay'
  name: string
  isGateway: boolean
}

export interface OrderPaymentStatus {
  paymentId: string
  orderId: string
  /** Pending | Authorized | Captured | Failed | Cancelled | Refunded | PartiallyRefunded */
  status: string
  provider: string | null
  amount: number
  currency: string
  failureReason: string | null
}

/** What the shop offers at checkout: cash on delivery plus the gateways it has switched on and configured. */
export const fetchPaymentMethods = () =>
  apiClient.get<PaymentMethodOption[]>('/v1/payment-gateway/methods').then((r) => r.data)

/** Starts (or restarts, while still pending) the gateway payment of the customer's own order; returns the page to open. */
export const startCheckout = (orderId: string) =>
  apiClient
    .post<{ paymentUrl: string; transactionRef: string; provider: string }>(`/v1/payment-gateway/orders/${orderId}/checkout`)
    .then((r) => r.data)

export const fetchPaymentStatus = (orderId: string) =>
  apiClient.get<OrderPaymentStatus>(`/v1/payment-gateway/orders/${orderId}/status`).then((r) => r.data)

/** The shape SaleOrderService returns (SalesOrderDto). */
export interface Order {
  orderId: string
  orderNo: string
  status: string
  totalAmount: number
  orderDate: string
  paymentMethod?: string | null
  lineItems: Array<{ productId: string; productName: string; quantity: number; unitPrice: number }>
}
