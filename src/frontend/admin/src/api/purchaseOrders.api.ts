import { PaginatedResponse } from './client'
import { apiClient, PO_BASE } from './poClient'

export type PurchaseOrderStatus =
  | 'DRAFT'
  | 'SUBMITTED'
  | 'APPROVED'
  | 'ORDERED'
  | 'PARTIALLY_RECEIVED'
  | 'RECEIVED'
  | 'CANCELLED'
  | 'REJECTED'
  | 'PENDING_FINANCE_APPROVAL'

export interface PurchaseOrderItem {
  id: string
  productCode: string
  productName: string
  quantity: number
  unit: string
  unitPrice: number
  totalPrice: number
  status: string
  receivedQuantity: number
  remainingQuantity: number
}

export interface PurchaseOrderSupplier {
  id: string
  name: string
}

export interface PurchaseOrder {
  id: string
  poNumber: string
  supplierId: string
  supplier?: PurchaseOrderSupplier
  status: PurchaseOrderStatus
  orderDate: string
  expectedDeliveryDate?: string
  totalAmount: number
  currency: string
  notes?: string
  scheduledReceiptDate?: string
  items: PurchaseOrderItem[]
}

export interface ListPurchaseOrdersParams {
  pageNumber?: number
  pageSize?: number
  status?: PurchaseOrderStatus
  supplierId?: string
}

export interface CreatePurchaseOrderRequest {
  poNumber: string
  supplierId: string
  expectedDeliveryDate?: string
  notes?: string
}

const BASE = `${PO_BASE}/purchase-orders`

export const purchaseOrdersApi = {
  list: (params: ListPurchaseOrdersParams) =>
    apiClient.get<PaginatedResponse<PurchaseOrder>>(BASE, { params }).then((r) => r.data),

  get: (id: string) =>
    apiClient.get<PurchaseOrder>(`${BASE}/${id}`).then((r) => r.data),

  create: (body: CreatePurchaseOrderRequest) =>
    apiClient.post<PurchaseOrder>(BASE, body).then((r) => r.data),

  submit: (id: string) =>
    apiClient.post<PurchaseOrder>(`${BASE}/${id}/submit`).then((r) => r.data),

  approve: (id: string) =>
    apiClient.post<PurchaseOrder>(`${BASE}/${id}/approve`).then((r) => r.data),

  /** Step 2 of the approval chain — only valid while status is PENDING_FINANCE_APPROVAL. */
  approveFinance: (id: string) =>
    apiClient.post<PurchaseOrder>(`${BASE}/${id}/finance-approve`).then((r) => r.data),

  reject: (id: string, reason: string) =>
    apiClient.post<PurchaseOrder>(`${BASE}/${id}/reject`, { reason }).then((r) => r.data),

  cancel: (id: string) =>
    apiClient.post<PurchaseOrder>(`${BASE}/${id}/cancel`).then((r) => r.data),
}
