import { create } from 'zustand'

export interface POSItem {
  productId: string
  productName: string
  unitPrice: number
  quantity: number
  taxRate: number
  sku?: string
}

/** A sale line whose price the server changed (the server's price is the one that counts, R113). */
export interface PriceChange {
  productId: string
  productName: string
  was: number
  now: number
}

interface POSState {
  transactionId: string | null
  /** The lines of the cart are on the server's transaction (so a retry must only complete it: adding them again would double the quantities). */
  linesSynced: boolean
  priceChanges: PriceChange[]
  items: POSItem[]
  discountAmount: number

  subtotal: () => number
  taxAmount: () => number
  total: () => number

  addItem: (item: Omit<POSItem, 'quantity'>) => void
  removeItem: (productId: string) => void
  updateQty: (productId: string, qty: number) => void
  applyDiscount: (amount: number) => void
  setTransactionId: (id: string | null) => void
  markLinesSynced: () => void
  /** Forget the server's transaction (a failure part-way): the next attempt starts a fresh one. */
  forgetTransaction: () => void
  /** Take the server's prices into the cart and keep what changed for the notice. */
  applyServerPrices: (prices: { productId: string; unitPrice: number }[]) => PriceChange[]
  dismissPriceChanges: () => void
  reset: () => void
}

export const usePOSStore = create<POSState>()((set, get) => ({
  transactionId: null,
  linesSynced: false,
  priceChanges: [],
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
        return { items: state.items.map((i) => i.productId === item.productId ? { ...i, quantity: i.quantity + 1 } : i), transactionId: null, linesSynced: false }
      }
      return { items: [...state.items, { ...item, quantity: 1 }], transactionId: null, linesSynced: false }
    })
  },

  removeItem: (productId) =>
    set((state) => ({ items: state.items.filter((i) => i.productId !== productId), transactionId: null, linesSynced: false })),

  updateQty: (productId, qty) => {
    if (qty <= 0) return get().removeItem(productId)
    set((state) => ({
      items: state.items.map((i) => i.productId === productId ? { ...i, quantity: qty } : i),
      transactionId: null,
      linesSynced: false,
    }))
  },

  applyDiscount: (amount) => set({ discountAmount: amount }),
  setTransactionId: (id) => set({ transactionId: id }),
  markLinesSynced: () => set({ linesSynced: true }),
  forgetTransaction: () => set({ transactionId: null, linesSynced: false }),
  applyServerPrices: (prices) => {
    const changes: PriceChange[] = []
    for (const item of get().items) {
      const server = prices.find((p) => p.productId === item.productId)
      if (server && Math.abs(server.unitPrice - item.unitPrice) >= 0.005) {
        changes.push({ productId: item.productId, productName: item.productName, was: item.unitPrice, now: server.unitPrice })
      }
    }
    if (changes.length) {
      set((state) => ({
        items: state.items.map((i) => {
          const change = changes.find((c) => c.productId === i.productId)
          return change ? { ...i, unitPrice: change.now } : i
        }),
        priceChanges: changes,
      }))
    }
    return changes
  },
  dismissPriceChanges: () => set({ priceChanges: [] }),
  reset: () => set({ transactionId: null, linesSynced: false, priceChanges: [], items: [], discountAmount: 0 }),
}))
