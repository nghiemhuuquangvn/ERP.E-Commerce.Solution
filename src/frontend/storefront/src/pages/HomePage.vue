<template>
  <div>
    <section class="bg-brand-subtle py-16 sm:py-20">
      <div class="mx-auto max-w-4xl px-4 text-center">
        <h1 class="text-3xl font-bold text-ink sm:text-4xl">{{ t('home.heroTitle') }}</h1>
        <p class="mt-4 text-lg text-ink-secondary">{{ t('home.heroSubtitle') }}</p>
        <Button to="/products" size="lg" class="mt-8">{{ t('home.shopNow') }}</Button>
      </div>
    </section>

    <section class="mx-auto max-w-7xl px-4 py-12" :aria-labelledby="'featured-title'">
      <h2 id="featured-title" class="mb-6 text-xl font-semibold text-ink">{{ t('home.featuredProducts') }}</h2>
      <ProductGrid :products="featured ?? []" :loading="isLoading" :failed="isError" @retry="refetch()" />
    </section>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { productsApi } from '@/api/products.api'
import ProductGrid from '@/components/product/ProductGrid.vue'
import Button from '@/components/ui/Button.vue'

const { t } = useI18n()
const { data: featured, isLoading, isError, refetch } = useQuery({
  queryKey: ['products', 'featured'],
  queryFn: () => productsApi.featured(),
})
</script>
