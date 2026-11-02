<template>
  <div class="mx-auto max-w-5xl px-4 py-10">
    <div v-if="isLoading" class="grid grid-cols-1 gap-10 md:grid-cols-2" role="status" :aria-label="t('product.loadingProduct')">
      <Skeleton class="aspect-square" />
      <div class="space-y-4"><Skeleton class="h-4 w-1/4" /><Skeleton class="h-8 w-3/4" /><Skeleton class="h-8 w-1/3" /><Skeleton class="h-24" /></div>
    </div>

    <Alert v-else-if="isError" variant="danger">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <span>{{ t('product.loadFailed') }}</span>
        <Button variant="outline" size="sm" @click="refetch()">{{ t('common.retry') }}</Button>
      </div>
    </Alert>

    <EmptyState v-else-if="!product" :title="t('product.notFound')">
      <Button to="/products" variant="outline">{{ t('cart.browseProducts') }}</Button>
    </EmptyState>

    <div v-else class="grid grid-cols-1 gap-10 md:grid-cols-2">
      <div class="aspect-square overflow-hidden rounded-xl bg-surface-muted">
        <img v-if="product.imageUrl" :src="product.imageUrl" :alt="product.name" class="h-full w-full object-cover" />
        <div v-else class="flex h-full items-center justify-center text-ink-muted"><Package :size="64" :stroke-width="1.75" aria-hidden="true" /></div>
      </div>

      <div class="flex flex-col">
        <p v-if="product.categoryName" class="text-sm text-ink-secondary">{{ product.categoryName }}</p>
        <h1 class="mt-1 text-2xl font-bold text-ink">{{ product.name }}</h1>
        <p class="mt-3"><PriceText :amount="product.price" size="lg" /></p>
        <p class="mt-2 text-sm" :class="product.stockQuantity === 0 ? 'text-danger-soft-text' : 'text-success-soft-text'">
          {{ product.stockQuantity === 0 ? t('product.outOfStock') : t('product.inStock') }}
        </p>
        <p class="mt-4 text-sm text-ink-secondary">{{ product.description }}</p>

        <div class="mt-6 hidden items-center gap-3 md:flex">
          <QuantityStepper v-model="qty" :label="t('cart.quantity')" :decrease-label="t('cart.decrease')" :increase-label="t('cart.increase')" />
          <Button size="lg" class="flex-1" :disabled="product.stockQuantity === 0" @click="addToCart">
            {{ product.stockQuantity === 0 ? t('product.outOfStock') : t('product.addToCart') }}
          </Button>
        </div>
      </div>
    </div>

    <!-- Below md the add-to-cart controls stay in reach at the bottom of the screen (the chat button sits above this bar). -->
    <div v-if="product" class="fixed inset-x-0 bottom-0 z-dropdown flex items-center gap-3 border-t border-line bg-surface px-4 py-3 md:hidden">
      <QuantityStepper v-model="qty" :label="t('cart.quantity')" :decrease-label="t('cart.decrease')" :increase-label="t('cart.increase')" />
      <Button size="lg" class="flex-1" :disabled="product.stockQuantity === 0" @click="addToCart">
        {{ product.stockQuantity === 0 ? t('product.outOfStock') : t('product.addToCart') }}
      </Button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Package } from 'lucide-vue-next'
import { ref } from 'vue'
import { useRoute } from 'vue-router'
import { useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { productsApi } from '@/api/products.api'
import Alert from '@/components/ui/Alert.vue'
import Button from '@/components/ui/Button.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import PriceText from '@/components/ui/PriceText.vue'
import QuantityStepper from '@/components/ui/QuantityStepper.vue'
import Skeleton from '@/components/ui/Skeleton.vue'
import { useToast } from '@/composables/useToast'
import { useCartStore } from '@/stores/cart.store'

const { t } = useI18n()
const route = useRoute()
const cartStore = useCartStore()
const toast = useToast()
const qty = ref(1)

const { data: product, isLoading, isError, refetch } = useQuery({
  queryKey: ['products', route.params.id],
  queryFn: () => productsApi.get(route.params.id as string),
})

function addToCart() {
  if (!product.value) return
  cartStore.addItem(
    {
      productId: product.value.id,
      productName: product.value.name,
      unitPrice: product.value.price,
      imageUrl: product.value.imageUrl,
    },
    qty.value,
  )
  toast.success(t('product.addedToCart'))
}
</script>
