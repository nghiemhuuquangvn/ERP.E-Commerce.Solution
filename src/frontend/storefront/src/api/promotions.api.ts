import { apiClient } from './client'

/** PromotionService CouponValidationResult. */
export interface CouponValidation {
  isValid: boolean
  errorMessage?: string | null
  discountAmount: number
  promotionId?: string | null
  couponCode?: string | null
}

export const promotionsApi = {
  validateCoupon: (code: string, orderAmount: number) =>
    apiClient
      .post<CouponValidation>('/v1/promotions/validate-coupon', { code, orderAmount })
      .then((r) => r.data),
}
