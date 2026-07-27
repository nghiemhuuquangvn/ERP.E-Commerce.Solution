import axios from 'axios'
import { apiClient } from './client'

// InventoryValuationSnapshotsController is nested under /api/v1/inventory, same as
// InventoryController/LotsController/SerialsController — reachable via the bare
// apiClient (dedicated public Gateway route), not the /wh/api/v1 catch-all.

export interface InventoryValuationSnapshotLine {
  productId: string
  quantity: number
  unitCost: number
  totalValue: number
}

export interface InventoryValuationSnapshot {
  id: string
  periodCode: string
  warehouseId: string | null
  snapshotDate: string
  isClosed: boolean
  totalValue: number
  lines: InventoryValuationSnapshotLine[]
}

export const valuationSnapshotsApi = {
  close: (body: { periodCode: string; warehouseId?: string }) =>
    apiClient.post<InventoryValuationSnapshot>('/v1/inventory/valuation/snapshots', body).then((r) => r.data),

  /** Returns null (not an error) if no snapshot has been closed for this period yet. */
  getByPeriod: async (periodCode: string, warehouseId?: string): Promise<InventoryValuationSnapshot | null> => {
    try {
      const { data } = await apiClient.get<InventoryValuationSnapshot>(
        `/v1/inventory/valuation/snapshots/${periodCode}`,
        { params: warehouseId ? { warehouseId } : undefined },
      )
      return data
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.status === 404) return null
      throw err
    }
  },
}
