import { apiClient } from './client'

export interface CouponValidation {
  valid: boolean
  discountAmount: number
  discountType: 'Percentage' | 'Fixed'
  promotionName: string
}

export const promotionsApi = {
  validateCoupon: (code: string, orderAmount: number) =>
    apiClient
      .post<CouponValidation>('/v1/promotions/coupons/validate', { code, orderAmount })
      .then((r) => r.data),
}
