import axios from 'axios'
import { apiClient, WH_BASE } from './whClient'

export interface BarcodeMapping {
  id: string
  barcode: string
  productId: string
  lotId: string | null
  serialNumber: string | null
}

export interface ResolveBarcodeResponse {
  productId: string
  productName: string
  availableQuantity: number
  lotId: string | null
  serialNumber: string | null
}

export const barcodeMappingsApi = {
  /** Returns null (not an error) if the barcode isn't mapped. */
  resolve: async (code: string): Promise<ResolveBarcodeResponse | null> => {
    try {
      const { data } = await apiClient.get<ResolveBarcodeResponse>(`${WH_BASE}/barcode/${encodeURIComponent(code)}/resolve`)
      return data
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.status === 404) return null
      throw err
    }
  },

  create: (body: { barcode: string; productId: string; lotId?: string; serialNumber?: string }) =>
    apiClient.post<BarcodeMapping>(`${WH_BASE}/barcode`, body).then((r) => r.data),

  delete: (id: string) => apiClient.delete(`${WH_BASE}/barcode/${id}`),
}
