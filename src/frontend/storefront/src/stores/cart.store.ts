import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

export interface CartItem {
  productId: string
  productName: string
  unitPrice: number
  quantity: number
  imageUrl?: string
}

export const useCartStore = defineStore(
  'cart',
  () => {
    const items = ref<CartItem[]>([])
    const couponCode = ref<string | null>(null)
    const couponDiscount = ref(0)

    const itemCount = computed(() => items.value.reduce((sum, i) => sum + i.quantity, 0))
    const subtotal = computed(() => items.value.reduce((sum, i) => sum + i.unitPrice * i.quantity, 0))
    const total = computed(() => Math.max(0, subtotal.value - couponDiscount.value))

    function addItem(item: Omit<CartItem, 'quantity'>, quantity = 1) {
      const existing = items.value.find((i) => i.productId === item.productId)
      if (existing) {
        existing.quantity += quantity
      } else {
        items.value.push({ ...item, quantity })
      }
    }

    function removeItem(productId: string) {
      items.value = items.value.filter((i) => i.productId !== productId)
    }

    function updateQuantity(productId: string, quantity: number) {
      if (quantity <= 0) return removeItem(productId)
      const item = items.value.find((i) => i.productId === productId)
      if (item) item.quantity = quantity
    }

    function applyCoupon(code: string, discount: number) {
      couponCode.value = code
      couponDiscount.value = discount
    }

    function removeCoupon() {
      couponCode.value = null
      couponDiscount.value = 0
    }

    function clear() {
      items.value = []
      couponCode.value = null
      couponDiscount.value = 0
    }

    return {
      items, couponCode, couponDiscount,
      itemCount, subtotal, total,
      addItem, removeItem, updateQuantity, applyCoupon, removeCoupon, clear,
    }
  },
  { persist: true },
)
