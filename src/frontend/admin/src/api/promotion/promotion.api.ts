import { gatewayClient as apiClient } from '../client'

// PromotionService is mounted under the Gateway's `/prm/api/...` route.
const BASE = '/prm/api/v1'

/** Request enums are numeric (the API has no string-enum converter); responses carry the names. */
export const DISCOUNT_TYPES = { Percentage: 0, FixedAmount: 1, BuyXGetY: 2, FreeShipping: 3 } as const
export const PROMOTION_SCOPES = { Order: 0, Product: 1, Category: 2 } as const

export type DiscountTypeName = keyof typeof DISCOUNT_TYPES
export type PromotionScopeName = keyof typeof PROMOTION_SCOPES
export type PromotionStatus = 'Draft' | 'Active' | 'Paused' | 'Expired' | 'Cancelled'

export interface Promotion {
  id: string
  name: string
  description?: string | null
  discountType: DiscountTypeName
  scope: PromotionScopeName
  discountValue: number
  maxDiscountAmount?: number | null
  minOrderAmount: number
  maxUsageTotal?: number | null
  maxUsagePerUser?: number | null
  currentUsage: number
  buyQuantity?: number | null
  getQuantity?: number | null
  startDate: string
  endDate: string
  status: PromotionStatus
  isCurrentlyActive: boolean
  applicableProductIds: string[]
  applicableCategoryIds: string[]
}

/** Body of create and of the draft edit. */
export interface PromotionInput {
  name: string
  description?: string | null
  discountType: number
  scope: number
  discountValue: number
  maxDiscountAmount?: number | null
  minOrderAmount: number
  maxUsageTotal?: number | null
  maxUsagePerUser?: number | null
  buyQuantity?: number | null
  getQuantity?: number | null
  startDate: string
  endDate: string
  applicableProductIds: string[]
  applicableCategoryIds: string[]
}

export interface Coupon {
  id: string
  code: string
  isUsed: boolean
  usedAt?: string | null
  usedByUserId?: string | null
  createdAt: string
}

export interface BulkCouponResult {
  promotionId: string
  generated: number
  codes: string[]
}

export const promotionApi = {
  list: (signal?: AbortSignal) => apiClient.get<Promotion[]>(`${BASE}/promotions/all`, { signal }).then((r) => r.data),
  create: (body: PromotionInput) => apiClient.post<Promotion>(`${BASE}/promotions`, body).then((r) => r.data),
  update: (id: string, body: PromotionInput) => apiClient.put<Promotion>(`${BASE}/promotions/${id}`, body).then((r) => r.data),
  activate: (id: string) => apiClient.post<Promotion>(`${BASE}/promotions/${id}/activate`).then((r) => r.data),
  pause: (id: string) => apiClient.post<Promotion>(`${BASE}/promotions/${id}/pause`).then((r) => r.data),
  cancel: (id: string) => apiClient.post<Promotion>(`${BASE}/promotions/${id}/cancel`).then((r) => r.data),
  coupons: (id: string, signal?: AbortSignal) => apiClient.get<Coupon[]>(`${BASE}/promotions/${id}/coupons`, { signal }).then((r) => r.data),
  generateCoupons: (id: string, body: { count: number; prefix: string }) =>
    apiClient.post<BulkCouponResult>(`${BASE}/promotions/${id}/generate-coupons`, body).then((r) => r.data),
}

// ── flash sales ──────────────────────────────────────────────────────────

export type FlashSaleStatus = 'Scheduled' | 'Active' | 'Completed' | 'Cancelled'

export interface FlashSaleItem {
  id: string
  productId: string
  productName: string
  originalPrice: number
  salePrice: number
  discountPercentage: number
  allocatedQuantity: number
  soldQuantity: number
  remainingQuantity: number
}

export interface FlashSale {
  id: string
  name: string
  description?: string | null
  startTime: string
  endTime: string
  status: FlashSaleStatus
  items: FlashSaleItem[]
}

export interface FlashSaleInput {
  name: string
  description?: string | null
  startTime: string
  endTime: string
  items: { productId: string; productName: string; originalPrice: number; salePrice: number; allocatedQuantity: number }[]
}

export const flashSaleApi = {
  list: (signal?: AbortSignal) => apiClient.get<FlashSale[]>(`${BASE}/flash-sales`, { params: { activeOnly: false }, signal }).then((r) => r.data),
  create: (body: FlashSaleInput) => apiClient.post<FlashSale>(`${BASE}/flash-sales`, body).then((r) => r.data),
  start: (id: string) => apiClient.post<FlashSale>(`${BASE}/flash-sales/${id}/start`).then((r) => r.data),
  cancel: (id: string) => apiClient.post<FlashSale>(`${BASE}/flash-sales/${id}/cancel`).then((r) => r.data),
}

// ── loyalty ──────────────────────────────────────────────────────────────

export interface LoyaltyAccount {
  userId: string
  totalPoints: number
  availablePoints: number
  tier: 'Bronze' | 'Silver' | 'Gold' | 'Platinum'
  pointsToNextTier: number
  nextTier?: string | null
}

export const loyaltyApi = {
  get: (userId: string) => apiClient.get<LoyaltyAccount>(`${BASE}/loyalty/${userId}`).then((r) => r.data),
  earn: (userId: string, body: { points: number; reason: string }) =>
    apiClient.post<LoyaltyAccount>(`${BASE}/loyalty/${userId}/earn`, body).then((r) => r.data),
  redeem: (userId: string, body: { points: number }) =>
    apiClient.post<LoyaltyAccount>(`${BASE}/loyalty/${userId}/redeem`, body).then((r) => r.data),
}
