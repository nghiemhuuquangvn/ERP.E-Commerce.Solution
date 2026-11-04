<template>
  <RouterLink :to="`/products/${product.id}`" class="group block rounded-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand-action focus-visible:ring-offset-2">
    <div class="aspect-square overflow-hidden rounded-lg bg-surface-muted">
      <img
        v-if="product.imageUrl"
        :src="product.imageUrl"
        :alt="product.name"
        class="h-full w-full object-cover transition-transform duration-200 group-hover:scale-105"
        loading="lazy"
      />
      <div v-else class="flex h-full items-center justify-center text-ink-muted"><Package :size="40" :stroke-width="1.75" aria-hidden="true" /></div>
    </div>
    <div class="mt-3">
      <p v-if="product.categoryName" class="text-xs text-ink-secondary">{{ product.categoryName }}</p>
      <component :is="`h${level}`" class="mt-0.5 line-clamp-2 text-sm font-medium text-ink group-hover:text-brand-action">
        {{ product.name }}
      </component>
      <div class="mt-1 flex items-center justify-between gap-2">
        <PriceText v-if="product.price > 0" :amount="product.price" />
        <span v-else class="text-sm text-ink-secondary">{{ t('product.priceUnavailable') }}</span>
        <Badge v-if="product.stockQuantity === 0" variant="danger">{{ t('product.outOfStock') }}</Badge>
      </div>
    </div>
  </RouterLink>
</template>

<script setup lang="ts">
import { Package } from 'lucide-vue-next'
import { RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import Badge from '@/components/ui/Badge.vue'
import PriceText from '@/components/ui/PriceText.vue'
import type { Product } from '@/api/products.api'

const { t } = useI18n()
withDefaults(defineProps<{ product: Product; /** The heading level of the product name: 2 under a page title, 3 under a section title. */ level?: 2 | 3 }>(), { level: 3 })
</script>
