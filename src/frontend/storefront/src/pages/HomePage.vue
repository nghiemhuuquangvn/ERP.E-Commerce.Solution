<template>
  <div>
    <!-- Hero -->
    <section class="bg-gradient-to-r from-brand-500 to-rose-400 py-20 text-white">
      <div class="mx-auto max-w-4xl px-4 text-center">
        <h1 class="text-4xl font-bold">{{ t('home.heroTitle') }}</h1>
        <p class="mt-4 text-lg text-rose-100">{{ t('home.heroSubtitle') }}</p>
        <RouterLink to="/products" class="mt-8 inline-block rounded-md bg-white px-6 py-3 text-sm font-semibold text-brand-500 hover:bg-rose-50">
          {{ t('home.shopNow') }}
        </RouterLink>
      </div>
    </section>

    <!-- Featured Products -->
    <section class="mx-auto max-w-7xl px-4 py-12">
      <h2 class="mb-6 text-xl font-semibold text-gray-900">{{ t('home.featuredProducts') }}</h2>
      <div v-if="isLoading" class="grid grid-cols-2 gap-4 md:grid-cols-4">
        <div v-for="n in 8" :key="n" class="h-64 animate-pulse rounded-lg bg-gray-100" />
      </div>
      <div v-else class="grid grid-cols-2 gap-4 md:grid-cols-4">
        <ProductCard v-for="product in featured" :key="product.id" :product="product" />
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from '@tanstack/vue-query'
import { RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { productsApi } from '@/api/products.api'
import ProductCard from '@/components/product/ProductCard.vue'

const { t } = useI18n()
const { data: featured, isLoading } = useQuery({
  queryKey: ['products', 'featured'],
  queryFn: () => productsApi.featured(),
})
</script>
