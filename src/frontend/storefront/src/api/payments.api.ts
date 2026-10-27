import { apiClient } from './client'

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
  method: string
  amount: number
  currency: string
  failureReason: string | null
  capturedAt: string | null
}

export const paymentsApi = {
  /** What this shop offers at checkout: cash on delivery plus the gateways that are switched on and configured. */
  methods: () => apiClient.get<PaymentMethodOption[]>('/v1/payment-gateway/methods').then((r) => r.data),

  /** Starts (or restarts, while the payment is still pending) the gateway payment of the customer's own order; returns the page to go to. */
  checkout: (orderId: string) =>
    apiClient
      .post<{ paymentUrl: string; transactionRef: string; provider: string }>(`/v1/payment-gateway/orders/${orderId}/checkout`)
      .then((r) => r.data),

  status: (orderId: string) =>
    apiClient.get<OrderPaymentStatus>(`/v1/payment-gateway/orders/${orderId}/status`).then((r) => r.data),
}
