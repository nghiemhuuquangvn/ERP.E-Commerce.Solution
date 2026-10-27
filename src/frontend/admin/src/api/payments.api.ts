import { apiClient, PaginatedResponse } from './client'

export type PaymentStatus = 'Pending' | 'Authorized' | 'Captured' | 'Failed' | 'Cancelled' | 'Refunded' | 'PartiallyRefunded'

export interface Payment {
  id: string
  orderId: string
  amount: number
  currency: string
  method: string
  /** What the customer chose at checkout: COD, VNPay, MoMo or ZaloPay. */
  provider?: string | null
  orderNumber?: string | null
  status: PaymentStatus
  failureReason?: string | null
  isFlaggedForReview?: boolean
  fraudReviewReason?: string | null
  gatewayTransactionId?: string
  paidAt?: string
  createdAt: string
}

export interface ListPaymentsParams {
  page?: number
  pageSize?: number
  status?: PaymentStatus
  orderId?: string
}

export const paymentsApi = {
  list: (params: ListPaymentsParams) =>
    apiClient.get<PaginatedResponse<Payment>>('/v1/payments', { params }).then((r) => r.data),

  get: (id: string) =>
    apiClient.get<Payment>(`/v1/payments/${id}`).then((r) => r.data),

  refund: (id: string, amount: number, reason: string) =>
    apiClient.post(`/v1/payments/${id}/refund`, { amount, reason }),
}
