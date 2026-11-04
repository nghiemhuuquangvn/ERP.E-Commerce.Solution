import { create } from 'zustand'
import { persist, createJSONStorage } from 'zustand/middleware'
import AsyncStorage from '@react-native-async-storage/async-storage'
import { apiClient } from '@/api/client'

export interface CartItem {
  productId: string
  productName: string
  unitPrice: number
  quantity: number
  imageUrl?: string
}

/** A cart line whose price moved (`now` = 0: the product has no price any more and was taken out of the cart). */
export interface PriceChange {
  productId: string
  productName: string
  was: number
  now: number
}

interface CartState {
  items: CartItem[]
  /** What the "Some prices changed" notice shows. Not persisted: it is about this visit. */
  priceChanges: PriceChange[]
  couponCode: string | null
  couponDiscount: number
  itemCount: () => number
  subtotal: () => number
  total: () => number
  addItem: (item: Omit<CartItem, 'quantity'>, qty?: number) => void
  removeItem: (productId: string) => void
  updateQty: (productId: string, qty: number) => void
  applyCoupon: (code: string, discount: number) => void
  /**
   * Reads the current price of every cart line (the cart is remembered between visits, so its prices can be old) and takes the new ones; a product that has
   * no price any more leaves the cart. Returns what changed and keeps it for the notice. A line whose product cannot be read is kept: the server decides when
   * the order is placed (R113).
   */
  refreshPrices: () => Promise<PriceChange[]>
  dismissPriceChanges: () => void
  clear: () => void
}

export const useCartStore = create<CartState>()(
  persist(
    (set, get) => ({
      items: [],
      couponCode: null,
      couponDiscount: 0,
      priceChanges: [],

      itemCount: () => get().items.reduce((s, i) => s + i.quantity, 0),
      subtotal: () => get().items.reduce((s, i) => s + i.unitPrice * i.quantity, 0),
      total: () => Math.max(0, get().subtotal() - get().couponDiscount),

      addItem: (item, qty = 1) => {
        set((state) => {
          const existing = state.items.find((i) => i.productId === item.productId)
          if (existing) {
            return { items: state.items.map((i) => i.productId === item.productId ? { ...i, quantity: i.quantity + qty } : i) }
          }
          return { items: [...state.items, { ...item, quantity: qty }] }
        })
      },

      removeItem: (productId) =>
        set((state) => ({ items: state.items.filter((i) => i.productId !== productId) })),

      updateQty: (productId, qty) => {
        if (qty <= 0) return get().removeItem(productId)
        set((state) => ({
          items: state.items.map((i) => i.productId === productId ? { ...i, quantity: qty } : i),
        }))
      },

      applyCoupon: (code, discount) => set({ couponCode: code, couponDiscount: discount }),

      refreshPrices: async () => {
        const changes: PriceChange[] = []
        const checked = await Promise.all(
          get().items.map(async (item) => {
            try {
              const { data } = await apiClient.get<{ price: number }>(`/v1/products/${item.productId}`)
              return { item, price: Number(data.price) }
            } catch {
              return null // not readable right now: keep the line as it is
            }
          }),
        )
        for (const c of checked) {
          if (!c || Number.isNaN(c.price)) continue
          if (c.price <= 0) changes.push({ productId: c.item.productId, productName: c.item.productName, was: c.item.unitPrice, now: 0 })
          else if (Math.abs(c.price - c.item.unitPrice) >= 0.005) changes.push({ productId: c.item.productId, productName: c.item.productName, was: c.item.unitPrice, now: c.price })
        }
        if (changes.length) {
          set((state) => ({
            items: state.items
              .filter((i) => !changes.some((c) => c.productId === i.productId && c.now === 0))
              .map((i) => {
                const change = changes.find((c) => c.productId === i.productId)
                return change ? { ...i, unitPrice: change.now } : i
              }),
            priceChanges: changes,
          }))
        }
        return changes
      },

      dismissPriceChanges: () => set({ priceChanges: [] }),

      clear: () => set({ items: [], couponCode: null, couponDiscount: 0 }),
    }),
    {
      name: 'erp-customer-cart',
      storage: createJSONStorage(() => AsyncStorage),
      // only the cart itself is remembered, never the notice
      partialize: (state) => ({ items: state.items, couponCode: state.couponCode, couponDiscount: state.couponDiscount }),
    },
  ),
)
