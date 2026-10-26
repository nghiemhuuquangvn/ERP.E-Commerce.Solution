import { apiClient } from './client'

/** One step of the order's processing: Payment, Inventory, Approval, Shipping, Done (ROADMAP P1-5). */
export interface OrderSagaStep {
  key: 'Payment' | 'Inventory' | 'Approval' | 'Shipping' | 'Done'
  state: 'Done' | 'Current' | 'Waiting' | 'Failed'
}

export interface OrderSaga {
  orderId: string
  /** False for an order that predates the saga: nothing to show. */
  exists: boolean
  status: 'Running' | 'Completed' | 'Compensating' | 'Failed' | ''
  currentStep: string
  steps: OrderSagaStep[]
  approved: boolean
  inventoryReserved: boolean
  retryCount: number
  maxRetries: number
  /** When the saga last moved: the current step has waited since then. */
  lastUpdatedAt: string
  createdAt: string
  completedAt: string | null
  /** Minutes the current step may wait before its command is published again; null when it never times out. */
  timeoutMinutes: number | null
  stuck: boolean
  /** The error, the reason of the compensation, or the last failed delivery. */
  message: string | null
  canRetry: boolean
}

export const orderSagaApi = {
  get: (orderId: string) => apiClient.get<OrderSaga>(`/v1/orders/${orderId}/saga`).then((r) => r.data),

  retry: (orderId: string) => apiClient.post<OrderSaga>(`/v1/orders/${orderId}/saga/retry`).then((r) => r.data),
}
