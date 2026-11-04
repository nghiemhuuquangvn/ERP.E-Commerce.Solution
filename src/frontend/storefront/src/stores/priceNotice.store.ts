import { defineStore } from 'pinia'
import { ref } from 'vue'

/** A cart line whose price moved (`now` = 0: the product has no price any more and was taken out of the cart). */
export interface PriceChange {
  productId: string
  productName: string
  was: number
  now: number
}

/** What the "Some prices changed" notice shows. Not persisted: it is about this visit, the cart itself is what is remembered. */
export const usePriceNoticeStore = defineStore('priceNotice', () => {
  const changes = ref<PriceChange[]>([])

  function show(list: PriceChange[]) {
    changes.value = list
  }

  function dismiss() {
    changes.value = []
  }

  return { changes, show, dismiss }
})
