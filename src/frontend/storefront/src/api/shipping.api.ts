import { apiClient } from './client'

/** What the shop charges for delivery (GET /v1/shipping/fee). A preview only: the order recomputes it when it is placed. */
export interface ShippingFeeQuote {
  fee: number
  /** The shop normally charges, but the goods reached its free-delivery threshold. */
  isFree: boolean
  /** 0 = the shop has no free-delivery threshold. */
  freeShippingThreshold: number
}

export const shippingApi = {
  fee: (merchandiseValue: number) =>
    apiClient.get<ShippingFeeQuote>('/v1/shipping/fee', { params: { merchandiseValue } }).then((r) => r.data),
}
