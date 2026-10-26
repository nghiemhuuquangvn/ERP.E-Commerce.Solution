import { apiClient } from './client'
import { WH_BASE } from './whClient'

// Real contract of WareHouseService's InventoryController (api/v1/inventory) —
// reconciled 2026-07-27. The previous version of this file used a contract that never
// matched the backend at all (id/quantityOnHand/warehouseLocation-as-string instead of
// the real productId-as-PK/totalQuantity/per-location breakdown).

export interface LocationStock {
  location: string
  quantity: number
  reserved: number
}

export interface Inventory {
  productId: string
  productName: string
  totalQuantity: number
  availableQuantity: number
  reservedQuantity: number
  lockedQuantity: number
  reorderPoint: number
  reorderQuantity: number
  minStock: number
  maxStock: number
  safetyStock: number
  isLowStock: boolean
  locations: LocationStock[]
  lastUpdated: string
}

export interface InventoryListItem {
  productId: string
  productName: string
  totalQuantity: number
  availableQuantity: number
  reorderPoint: number
  minStock: number
  maxStock: number
  safetyStock: number
  productSku?: string
  status: 'OK' | 'Low Stock'
}

export interface ListInventoryParams {
  page?: number
  pageSize?: number
  search?: string
  lowStockOnly?: boolean
}

export interface ListInventoryResponse {
  total: number
  items: InventoryListItem[]
}

export interface InventoryValuation {
  productId: string
  valuationMethod: 'FIFO' | 'LIFO' | 'Average'
  totalQuantity: number
  totalValue: number
  averageCost: number
}

export interface InventoryLot {
  lotId: string
  productId: string
  lotNumber: string
  receivedQuantity: number
  remainingQuantity: number
  reservedQuantity: number
  availableQuantity: number
  unitCost: number
  receivedDate: string
  expiryDate: string | null
  purchaseOrderId: string | null
}

// Per-sale COGS audit trail (Module 8 reconciliation) — written once per lot consumed
// (or once at the product's average cost for non-lot-tracked products) when an order
// ships. Read-only.
export interface CostConsumptionLog {
  id: string
  productId: string
  lotId: string | null
  quantity: number
  unitCost: number
  totalCost: number
  sourceOrderId: string
  consumedAt: string
}

export interface InventorySerial {
  serialId: string
  productId: string
  serialNumber: string
  lotId: string | null
  status: 'InStock' | 'Reserved' | 'Sold' | 'Damaged'
  receivedDate: string
  soldDate: string | null
}

export interface StockPolicy {
  minStock: number
  maxStock: number
  safetyStock: number
}

export interface ReplenishmentScanResult {
  checked: number
  belowMinimum: number
  alertsSent: number
}

export const inventoryApi = {
  get: (productId: string) => apiClient.get<Inventory>(`/v1/inventory/${productId}`).then((r) => r.data),

  list: (params: ListInventoryParams) => {
    const skip = ((params.page ?? 1) - 1) * (params.pageSize ?? 50)
    return apiClient
      .get<ListInventoryResponse>('/v1/inventory', {
        params: { search: params.search, lowStockOnly: params.lowStockOnly, skip, take: params.pageSize ?? 50 },
      })
      .then((r) => r.data)
  },

  /** Replenishment policy of one product: order when available stock (plus goods on order) is at or below min, up to max. */
  updateStockPolicy: (productId: string, policy: StockPolicy) =>
    apiClient.put<StockPolicy & { productId: string; availableQuantity: number; isLowStock: boolean }>(`/v1/inventory/${productId}/stock-policy`, policy).then((r) => r.data),

  scanReplenishment: () => apiClient.post<ReplenishmentScanResult>('/v1/inventory/replenishment/scan').then((r) => r.data),

  lowStock: () => apiClient.get<ListInventoryResponse>('/v1/inventory/low-stock').then((r) => r.data),

  valuation: (productId: string) =>
    apiClient.get<InventoryValuation>(`/v1/inventory/${productId}/valuation`).then((r) => r.data),

  lotsByProduct: (productId: string) =>
    apiClient.get<InventoryLot[]>(`/v1/inventory/lots/product/${productId}`).then((r) => r.data),

  costConsumption: (productId: string, params?: { startDate?: string; endDate?: string }) =>
    apiClient
      .get<CostConsumptionLog[]>(`/v1/inventory/${productId}/cost-consumption`, { params })
      .then((r) => r.data),

  expiringLots: () => apiClient.get<InventoryLot[]>('/v1/inventory/lots/expiring').then((r) => r.data),

  serialsByProduct: (productId: string) =>
    apiClient.get<InventorySerial[]>(`/v1/inventory/serials/product/${productId}`).then((r) => r.data),

  setSerialStatus: (serialNumber: string, status: InventorySerial['status']) =>
    apiClient.post(`/v1/inventory/serials/${serialNumber}/status`, { status }),

  receive: (body: {
    productId: string
    productName?: string
    productSku?: string
    quantity: number
    unitCost: number
    locationId?: string
    lotNumber?: string
    expiryDate?: string
    serialNumbers?: string[]
    reference?: string
    purchaseOrderId?: string
  }) => apiClient.post(`/v1/inventory/receive`, body).then((r) => r.data),
}

export const movementsApi = {
  list: (productId: string, params?: { startDate?: string; endDate?: string; skip?: number; take?: number }) =>
    apiClient.get(`${WH_BASE}/movements/${productId}`, { params }).then((r) => r.data),
}
