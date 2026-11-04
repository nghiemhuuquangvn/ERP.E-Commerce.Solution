import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { productsApi } from '@/api/products.api'
import { usePriceNoticeStore, type PriceChange } from '@/stores/priceNotice.store'

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

    /**
     * Reads the current price of every cart line (the cart is remembered between visits, so its prices can be old) and takes the new ones. A product that
     * has no price any more leaves the cart. Returns what changed and puts it in the "Some prices changed" notice. A line whose product cannot be read is
     * kept: the server decides at checkout (R113).
     */
    async function refreshPrices(): Promise<PriceChange[]> {
      const changes: PriceChange[] = []
      await Promise.all(
        items.value.map(async (item) => {
          try {
            const current = await productsApi.get(item.productId)
            if (current.price <= 0) {
              changes.push({ productId: item.productId, productName: item.productName, was: item.unitPrice, now: 0 })
            } else if (Math.abs(current.price - item.unitPrice) >= 0.005) {
              changes.push({ productId: item.productId, productName: item.productName, was: item.unitPrice, now: current.price })
              item.unitPrice = current.price
            }
          } catch {
            // not readable right now: keep the line as it is
          }
        }),
      )
      const gone = new Set(changes.filter((c) => c.now === 0).map((c) => c.productId))
      if (gone.size) items.value = items.value.filter((i) => !gone.has(i.productId))
      if (changes.length) usePriceNoticeStore().show(changes)
      return changes
    }

    function clear() {
      items.value = []
      couponCode.value = null
      couponDiscount.value = 0
    }

    return {
      items, couponCode, couponDiscount,
      itemCount, subtotal, total,
      addItem, removeItem, updateQuantity, applyCoupon, removeCoupon, refreshPrices, clear,
    }
  },
  { persist: true },
)
