<template>
  <RouterLink :to="`/products/${product.id}`" class="group block">
    <div class="aspect-square overflow-hidden rounded-lg bg-surface-muted">
      <img
        v-if="product.imageUrl"
        :src="product.imageUrl"
        :alt="product.name"
        class="h-full w-full object-cover transition-transform duration-200 group-hover:scale-105"
        loading="lazy"
      />
      <div v-else class="flex h-full items-center justify-center text-4xl text-ink-muted"><Package :size="40" :stroke-width="1.75" aria-hidden="true" /></div>
    </div>
    <div class="mt-3">
      <p class="text-xs text-ink-muted">{{ product.categoryName }}</p>
      <h3 class="mt-0.5 text-sm font-medium text-ink group-hover:text-brand-action line-clamp-2">
        {{ product.name }}
      </h3>
      <div class="mt-1 flex items-center justify-between">
        <span class="text-base font-semibold text-ink">{{ fmt(product.price) }}</span>
        <span v-if="product.stockQuantity === 0" class="text-xs text-danger">{{ t('product.outOfStock') }}</span>
      </div>
    </div>
  </RouterLink>
</template>

<script setup lang="ts">
import { Package } from 'lucide-vue-next'
import { RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import type { Product } from '@/api/products.api'

const { t } = useI18n()
defineProps<{ product: Product }>()

const fmt = (n: number) =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)
</script>
