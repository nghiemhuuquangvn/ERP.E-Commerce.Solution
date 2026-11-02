<template>
  <div class="mx-auto max-w-4xl px-4 py-10">
    <h1 class="mb-6 text-2xl font-bold text-ink">{{ t('cart.title') }}</h1>

    <EmptyState v-if="items.length === 0" :title="t('cart.empty')">
      <template #icon><ShoppingCart :size="40" :stroke-width="1.75" aria-hidden="true" /></template>
      <Button to="/products">{{ t('cart.browseProducts') }}</Button>
    </EmptyState>

    <div v-else class="grid grid-cols-1 gap-8 md:grid-cols-3">
      <ul class="space-y-4 md:col-span-2">
        <li v-for="item in items" :key="item.productId" class="flex gap-4 rounded-lg border border-line p-4">
          <div class="h-20 w-20 flex-shrink-0 overflow-hidden rounded-md bg-surface-muted">
            <img v-if="item.imageUrl" :src="item.imageUrl" :alt="item.productName" class="h-full w-full object-cover" />
            <div v-else class="flex h-full items-center justify-center text-ink-muted"><Package :size="24" :stroke-width="1.75" aria-hidden="true" /></div>
          </div>
          <div class="flex min-w-0 flex-1 flex-col">
            <p class="font-medium text-ink">{{ item.productName }}</p>
            <p class="text-sm text-ink-secondary">{{ usd(item.unitPrice) }} {{ t('cart.each') }}</p>
            <div class="mt-2 flex flex-wrap items-center gap-3">
              <QuantityStepper
                :model-value="item.quantity"
                :label="t('cart.quantityOf', { name: item.productName })"
                :decrease-label="t('cart.decrease')"
                :increase-label="t('cart.increase')"
                @update:model-value="(q: number) => updateQty(item.productId, q)"
              />
              <Button variant="link" size="sm" :aria-label="t('cart.removeItem', { name: item.productName })" @click="cartStore.removeItem(item.productId)">
                <Trash2 :size="16" :stroke-width="1.75" aria-hidden="true" />{{ t('cart.remove') }}
              </Button>
            </div>
          </div>
          <PriceText :amount="item.unitPrice * item.quantity" />
        </li>
      </ul>

      <section class="h-fit space-y-3 rounded-lg border border-line p-5" :aria-labelledby="'summary-title'">
        <h2 id="summary-title" class="font-semibold text-ink">{{ t('cart.orderSummary') }}</h2>
        <div class="flex justify-between text-sm text-ink-secondary">
          <span>{{ t('cart.subtotal') }}</span>
          <span class="tabular-nums">{{ usd(subtotal) }}</span>
        </div>
        <div v-if="couponDiscount > 0" class="flex justify-between text-sm text-success-soft-text">
          <span>{{ t('cart.couponDiscount') }}</span>
          <span class="tabular-nums">−{{ usd(couponDiscount) }}</span>
        </div>
        <p class="text-xs text-ink-secondary">{{ t('cart.deliveryAtCheckout') }}</p>
        <div class="flex justify-between border-t border-line pt-3 font-semibold text-ink">
          <span>{{ t('common.total') }}</span>
          <span class="tabular-nums">{{ usd(total) }}</span>
        </div>
        <Button to="/checkout" full-width class="mt-2">{{ t('cart.proceedToCheckout') }}</Button>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Package, ShoppingCart, Trash2 } from 'lucide-vue-next'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import Button from '@/components/ui/Button.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import PriceText from '@/components/ui/PriceText.vue'
import QuantityStepper from '@/components/ui/QuantityStepper.vue'
import { useMoney } from '@/composables/useMoney'
import { useCartStore } from '@/stores/cart.store'

const { t } = useI18n()
const { usd } = useMoney()
const cartStore = useCartStore()
const items = computed(() => cartStore.items)
const subtotal = computed(() => cartStore.subtotal)
const couponDiscount = computed(() => cartStore.couponDiscount)
const total = computed(() => cartStore.total)

function updateQty(productId: string, qty: number) {
  cartStore.updateQuantity(productId, qty)
}
</script>
