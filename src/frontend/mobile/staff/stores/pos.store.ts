import { create } from 'zustand'

export interface POSItem {
  productId: string
  productName: string
  unitPrice: number
  quantity: number
  taxRate: number
}

interface POSState {
  transactionId: string | null
  items: POSItem[]
  discountAmount: number

  subtotal: () => number
  taxAmount: () => number
  total: () => number

  addItem: (item: Omit<POSItem, 'quantity'>) => void
  removeItem: (productId: string) => void
  updateQty: (productId: string, qty: number) => void
  applyDiscount: (amount: number) => void
  setTransactionId: (id: string) => void
  reset: () => void
}

export const usePOSStore = create<POSState>()((set, get) => ({
  transactionId: null,
  items: [],
  discountAmount: 0,

  subtotal: () => get().items.reduce((s, i) => s + i.unitPrice * i.quantity, 0),
  taxAmount: () =>
    get().items.reduce((s, i) => s + i.unitPrice * i.quantity * (i.taxRate / 100), 0),
  total: () => Math.max(0, get().subtotal() + get().taxAmount() - get().discountAmount),

  addItem: (item) => {
    set((state) => {
      const existing = state.items.find((i) => i.productId === item.productId)
      if (existing) {
        return { items: state.items.map((i) => i.productId === item.productId ? { ...i, quantity: i.quantity + 1 } : i) }
      }
      return { items: [...state.items, { ...item, quantity: 1 }] }
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

  applyDiscount: (amount) => set({ discountAmount: amount }),
  setTransactionId: (id) => set({ transactionId: id }),
  reset: () => set({ transactionId: null, items: [], discountAmount: 0 }),
}))
