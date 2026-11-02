<template>
  <div v-if="loading" class="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-4" role="status" :aria-label="t('common.loading')">
    <div v-for="n in skeletons" :key="n">
      <Skeleton class="aspect-square" />
      <Skeleton class="mt-3 h-4 w-3/4" />
      <Skeleton class="mt-2 h-4 w-1/3" />
    </div>
  </div>

  <Alert v-else-if="failed" variant="danger">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <span>{{ t('product.loadFailed') }}</span>
      <Button variant="outline" size="sm" @click="emit('retry')">{{ t('common.retry') }}</Button>
    </div>
  </Alert>

  <slot v-else-if="!products.length" name="empty"><EmptyState :title="t('product.noProductsFound')" /></slot>

  <ul v-else class="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-4">
    <li v-for="product in products" :key="product.id"><ProductCard :product="product" :level="cardLevel" /></li>
  </ul>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import ProductCard from '@/components/product/ProductCard.vue'
import Alert from '@/components/ui/Alert.vue'
import Button from '@/components/ui/Button.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import Skeleton from '@/components/ui/Skeleton.vue'
import type { Product } from '@/api/products.api'

const { t } = useI18n()
withDefaults(defineProps<{ products: Product[]; loading?: boolean; failed?: boolean; skeletons?: number; cardLevel?: 2 | 3 }>(), { skeletons: 8, cardLevel: 3 })
const emit = defineEmits<{ retry: [] }>()
</script>
