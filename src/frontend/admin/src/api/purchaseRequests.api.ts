import { apiClient, PO_BASE } from './poClient'

export type PurchaseRequestStatus = 'Suggested' | 'Dismissed' | 'Converted' | 'Ordered' | 'Quoting'

export interface PurchaseRequest {
  id: string
  productId: string
  productCode: string
  productName: string
  currentQuantity: number
  availableQuantity: number
  onOrderQuantity: number
  reorderPoint: number
  minStock: number
  maxStock: number
  safetyStock: number
  suggestedQuantity: number
  preferredSupplierId: string | null
  preferredSupplierName: string | null
  unitPrice: number | null
  leadTimeDays: number | null
  purchaseOrderId: string | null
  rfqId: string | null
  note: string | null
  status: PurchaseRequestStatus
  createdAt: string
  resolvedAt: string | null
}

export interface CreateOrdersResult {
  purchaseOrders: { id: string; poNumber: string; supplierId: string; supplierName: string; lines: number; total: number; currency: string; requestIds: string[] }[]
  rfq: { id: string; rfqNumber: string; lines: number; suppliers: number; requestIds: string[] } | null
  skipped: { requestId: string; reason: string }[]
}

const BASE = `${PO_BASE}/purchase-requests`

export const purchaseRequestsApi = {
  list: (status?: PurchaseRequestStatus) =>
    apiClient.get<PurchaseRequest[]>(BASE, { params: status ? { status } : undefined }).then((r) => r.data),

  dismiss: (id: string) => apiClient.post<PurchaseRequest>(`${BASE}/${id}/dismiss`).then((r) => r.data),

  /** Draft purchase orders (one per preferred supplier) and one RFQ for the rest; no ids = every open request. */
  createOrders: (requestIds?: string[]) => apiClient.post<CreateOrdersResult>(`${BASE}/create-orders`, { requestIds: requestIds ?? null }).then((r) => r.data),

  convert: (id: string) => apiClient.post<PurchaseRequest>(`${BASE}/${id}/convert`).then((r) => r.data),
}
