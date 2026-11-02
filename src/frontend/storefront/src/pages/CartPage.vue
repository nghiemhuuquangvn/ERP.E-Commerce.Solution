<template>
  <div class="mx-auto max-w-4xl px-4 py-10">
    <h1 class="mb-6 text-2xl font-bold text-ink">{{ t('cart.title') }}</h1>

    <div v-if="items.length === 0" class="py-20 text-center text-ink-muted">
      {{ t('cart.empty') }}
      <RouterLink to="/products" class="block mt-3 text-brand-action hover:underline">{{ t('cart.browseProducts') }}</RouterLink>
    </div>

    <div v-else class="grid grid-cols-1 gap-8 md:grid-cols-3">
      <!-- Items -->
      <div class="md:col-span-2 space-y-4">
        <div v-for="item in items" :key="item.productId" class="flex gap-4 rounded-lg border border-line p-4">
          <div class="h-20 w-20 flex-shrink-0 overflow-hidden rounded-md bg-surface-muted">
            <img v-if="item.imageUrl" :src="item.imageUrl" :alt="item.productName" class="h-full w-full object-cover" />
            <div v-else class="flex h-full items-center justify-center text-2xl text-ink-muted"><Package :size="24" :stroke-width="1.75" aria-hidden="true" /></div>
          </div>
          <div class="flex flex-1 flex-col">
            <p class="font-medium text-ink">{{ item.productName }}</p>
            <p class="text-sm text-ink-muted">{{ fmt(item.unitPrice) }} {{ t('cart.each') }}</p>
            <div class="mt-2 flex items-center gap-3">
              <div class="flex items-center border border-line rounded">
                <button @click="updateQty(item.productId, item.quantity - 1)" class="px-2 py-0.5 text-ink-muted">−</button>
                <span class="px-3 text-sm">{{ item.quantity }}</span>
                <button @click="updateQty(item.productId, item.quantity + 1)" class="px-2 py-0.5 text-ink-muted">+</button>
              </div>
              <button @click="cartStore.removeItem(item.productId)" class="text-xs text-danger hover:underline">{{ t('cart.remove') }}</button>
            </div>
          </div>
          <span class="font-semibold text-ink">{{ fmt(item.unitPrice * item.quantity) }}</span>
        </div>
      </div>

      <!-- Summary -->
      <div class="rounded-lg border border-line p-5 h-fit space-y-3">
        <h2 class="font-semibold text-ink">{{ t('cart.orderSummary') }}</h2>
        <div class="flex justify-between text-sm text-ink-secondary">
          <span>{{ t('cart.subtotal') }}</span>
          <span>{{ fmt(subtotal) }}</span>
        </div>
        <div v-if="couponDiscount > 0" class="flex justify-between text-sm text-success-soft-text">
          <span>{{ t('cart.couponDiscount') }}</span>
          <span>−{{ fmt(couponDiscount) }}</span>
        </div>
        <div class="border-t border-line pt-3 flex justify-between font-semibold text-ink">
          <span>{{ t('common.total') }}</span>
          <span>{{ fmt(total) }}</span>
        </div>
        <RouterLink to="/checkout" class="block w-full btn-brand text-center mt-2">
          {{ t('cart.proceedToCheckout') }}
        </RouterLink>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Package } from 'lucide-vue-next'
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useCartStore } from '@/stores/cart.store'

const { t } = useI18n()
const cartStore = useCartStore()
const items = computed(() => cartStore.items)
const subtotal = computed(() => cartStore.subtotal)
const couponDiscount = computed(() => cartStore.couponDiscount)
const total = computed(() => cartStore.total)

const fmt = (n: number) =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)

function updateQty(productId: string, qty: number) {
  cartStore.updateQuantity(productId, qty)
}
</script>
