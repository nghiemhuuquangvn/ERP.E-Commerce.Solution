import { gatewayClient as apiClient } from './client'
import type { CarrierCode } from './settings/shipping.settings.api'

// ShippingService is mounted under the Gateway's `/shp/api/...` route (same as the shipping settings).
const BASE = '/shp/api/v1'

export type ShipmentStatus =
  | 'Pending' | 'PickedUp' | 'InTransit' | 'OutForDelivery' | 'Delivered' | 'DeliveryFailed' | 'Returned' | 'Cancelled' | 'ReadyForPickup'

export interface TrackingEvent {
  status: string
  location: string
  description: string
  occurredAt: string
}

export interface Shipment {
  id: string
  orderId: string
  carrierId: string
  carrierName: string
  trackingNumber?: string | null
  status: ShipmentStatus
  recipientName: string
  recipientPhone?: string | null
  addressStreet: string
  addressWard?: string | null
  addressDistrict: string
  addressProvince: string
  estimatedDelivery?: string | null
  deliveredAt?: string | null
  totalWeight: number
  /** Cash the carrier collects from the recipient (0 = paid online). */
  codAmount: number
  declaredValue: number
  /** What the carrier charges the shop. */
  carrierFee?: number | null
  carrierOrderCode?: string | null
  /** Why this carrier: the rule that matched or the quotes compared. */
  selectionNote?: string | null
  bookingError?: string | null
  /** Something a person should look at (cash mismatch, the carrier refused to cancel...). */
  reviewNote?: string | null
  lastCarrierSyncAt?: string | null
  /** 1-based number of this parcel and how many parcels the order travels in (1 / 1 = not split). */
  parcelNo?: number
  parcelCount?: number
  createdAt: string
  trackingEvents: TrackingEvent[]
  items?: { productId: string; productName: string; quantity: number; weight: number }[]
}

/** The goods' way back (customer to shop) booked at a carrier for a returned order. */
export interface ReturnParcel {
  id: string
  originalShipmentId: string
  orderId: string
  reason: string
  trackingNumber?: string | null
  status: 'Requested' | 'PickedUp' | 'Received' | 'Cancelled'
  carrierName?: string | null
  carrierOrderCode?: string | null
  carrierFee?: number | null
  bookingError?: string | null
  pickedUpAt?: string | null
  receivedAt?: string | null
  lastCarrierSyncAt?: string | null
  createdAt?: string
}

export interface ReturnParcelList {
  items: ReturnParcel[]
  totalCount: number
  pageNumber: number
  pageSize: number
}

/** One new parcel of a split: product quantities and, optionally, the cash the carrier collects on it. */
export interface SplitPart {
  items: { productId: string; quantity: number }[]
  codAmount?: number
}

export interface ShipmentList {
  items: Shipment[]
  totalCount: number
  pageNumber: number
  pageSize: number
}

/** One row of the shop's delivery price list: parcels to these provinces in this weight band cost this much. */
export interface ShippingFeeRule {
  id: string
  name: string
  priority: number
  isActive: boolean
  matchProvinces: string | null
  minWeightGrams: number | null
  maxWeightGrams: number | null
  fee: number
}

export type SaveShippingFeeRule = Omit<ShippingFeeRule, 'id'>

export interface FeeQuoteRequest {
  merchandiseValue: number
  province?: string
  /** Used when no items are given. */
  weightGrams?: number
  items: { productId: string; quantity: number }[]
}

export interface FeeQuote {
  fee: number
  isFree: boolean
  freeShippingThreshold: number
  ruleName: string | null
  weightGrams: number
  weightEstimated: boolean
}

export interface CarrierRule {
  id: string
  name: string
  priority: number
  isActive: boolean
  matchProvinces?: string | null
  minWeightGrams?: number | null
  maxWeightGrams?: number | null
  minOrderValue?: number | null
  maxOrderValue?: number | null
  /** true = only cash-on-delivery orders, false = only prepaid, null = both. */
  codOnly?: boolean | null
  carrierCode: CarrierCode
}

export type SaveCarrierRule = Omit<CarrierRule, 'id'>

export interface QuoteParcel {
  province: string
  district: string
  ward?: string
  street?: string
  weightGrams: number
  declaredValue: number
  codAmount: number
}

export interface CarrierChoicePreview {
  ruleCarrierCode?: string | null
  ruleName?: string | null
  quotes: { carrierCode: string; carrierName: string; success: boolean; fee: number; error?: string | null }[]
}

export const shippingApi = {
  listShipments: (params: { page?: number; pageSize?: number; status?: string; search?: string; needsReview?: boolean }) =>
    apiClient.get<ShipmentList>(`${BASE}/shipments`, { params }).then((r) => r.data),

  getShipment: (id: string) => apiClient.get<Shipment>(`${BASE}/shipments/${id}`).then((r) => r.data),

  cancelAtCarrier: (id: string, reason?: string) =>
    apiClient.post<Shipment>(`${BASE}/shipments/${id}/cancel-at-carrier`, { reason }).then((r) => r.data),

  rebook: (id: string) => apiClient.post<Shipment>(`${BASE}/shipments/${id}/rebook`, {}).then((r) => r.data),

  /** Splits a parcel the carrier has not collected into several; all or nothing. */
  split: (id: string, parts: SplitPart[]) => apiClient.post<Shipment[]>(`${BASE}/shipments/${id}/split`, { parts }).then((r) => r.data),

  /** The return parcels of an order. */
  orderReturns: (orderId: string) => apiClient.get<ReturnParcel[]>(`${BASE}/shipments/order-returns/${orderId}`).then((r) => r.data),

  /** Every return parcel of the shop (admin Returns screen). */
  listReturns: (params: { page?: number; pageSize?: number; status?: string; search?: string; needsAttention?: boolean }) =>
    apiClient.get<ReturnParcelList>(`${BASE}/shipments/returns`, { params }).then((r) => r.data),

  /** A refused reverse booking: book it at a carrier again. */
  rebookReturn: (returnId: string) => apiClient.post<ReturnParcel>(`${BASE}/shipments/returns/${returnId}/rebook`, {}).then((r) => r.data),

  /** Cancels the pickup at the carrier first; a refusal comes back as an error and the return stays open. */
  cancelReturn: (returnId: string) => apiClient.post<ReturnParcel>(`${BASE}/shipments/returns/${returnId}/cancel`, {}).then((r) => r.data),

  /** The goods arrived and nobody told the carrier's system (a person receives them by hand). */
  receiveReturn: (shipmentId: string, returnId: string) =>
    apiClient.post<ReturnParcel>(`${BASE}/shipments/${shipmentId}/returns/${returnId}/received`, {}).then((r) => r.data),

  listFeeRules: () => apiClient.get<ShippingFeeRule[]>(`${BASE}/shipping/fee-rules`).then((r) => r.data),

  createFeeRule: (body: SaveShippingFeeRule) => apiClient.post<ShippingFeeRule>(`${BASE}/shipping/fee-rules`, body).then((r) => r.data),

  updateFeeRule: (id: string, body: SaveShippingFeeRule) => apiClient.put<ShippingFeeRule>(`${BASE}/shipping/fee-rules/${id}`, body).then((r) => r.data),

  deleteFeeRule: (id: string) => apiClient.delete(`${BASE}/shipping/fee-rules/${id}`),

  /** What a customer would pay for these goods to this province (the "try a parcel" box). Charges nothing. */
  previewFee: (body: FeeQuoteRequest) => apiClient.post<FeeQuote>(`${BASE}/shipping/fee`, body).then((r) => r.data),

  listRules: () => apiClient.get<CarrierRule[]>(`${BASE}/carriers/rules`).then((r) => r.data),

  createRule: (body: SaveCarrierRule) => apiClient.post<CarrierRule>(`${BASE}/carriers/rules`, body).then((r) => r.data),

  updateRule: (id: string, body: SaveCarrierRule) => apiClient.put<CarrierRule>(`${BASE}/carriers/rules/${id}`, body).then((r) => r.data),

  deleteRule: (id: string) => apiClient.delete(`${BASE}/carriers/rules/${id}`),

  quote: (body: QuoteParcel) => apiClient.post<CarrierChoicePreview>(`${BASE}/carriers/quote`, body).then((r) => r.data),
}
