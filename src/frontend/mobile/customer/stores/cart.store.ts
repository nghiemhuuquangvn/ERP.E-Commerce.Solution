import { create } from 'zustand'
import { persist, createJSONStorage } from 'zustand/middleware'
import AsyncStorage from '@react-native-async-storage/async-storage'

export interface CartItem {
  productId: string
  productName: string
  unitPrice: number
  quantity: number
  imageUrl?: string
}

interface CartState {
  items: CartItem[]
  couponCode: string | null
  couponDiscount: number
  itemCount: () => number
  subtotal: () => number
  total: () => number
  addItem: (item: Omit<CartItem, 'quantity'>, qty?: number) => void
  removeItem: (productId: string) => void
  updateQty: (productId: string, qty: number) => void
  applyCoupon: (code: string, discount: number) => void
  clear: () => void
}

export const useCartStore = create<CartState>()(
  persist(
    (set, get) => ({
      items: [],
      couponCode: null,
      couponDiscount: 0,

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

      clear: () => set({ items: [], couponCode: null, couponDiscount: 0 }),
    }),
    {
      name: 'erp-customer-cart',
      storage: createJSONStorage(() => AsyncStorage),
    },
  ),
)
