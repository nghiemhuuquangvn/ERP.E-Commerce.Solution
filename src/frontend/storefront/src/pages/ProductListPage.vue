<template>
  <div class="mx-auto max-w-7xl px-4 py-8">
    <div class="mb-6 flex items-center gap-4">
      <input
        v-model="search"
        class="input max-w-xs"
        :placeholder="t('product.searchPlaceholder')"
        @input="page = 1"
      />
      <select v-model="sortBy" class="input w-auto">
        <option value="newest">{{ t('product.sortNewest') }}</option>
        <option value="price_asc">{{ t('product.sortPriceAsc') }}</option>
        <option value="price_desc">{{ t('product.sortPriceDesc') }}</option>
        <option value="name">{{ t('product.sortName') }}</option>
      </select>
    </div>

    <div v-if="isLoading" class="grid grid-cols-2 gap-4 md:grid-cols-4">
      <div v-for="n in 12" :key="n" class="h-64 animate-pulse rounded-lg bg-gray-100" />
    </div>

    <div v-else-if="data?.items.length === 0" class="py-20 text-center text-gray-400">
      {{ t('product.noProductsFound') }}
    </div>

    <div v-else class="grid grid-cols-2 gap-4 md:grid-cols-4">
      <ProductCard v-for="product in data?.items" :key="product.id" :product="product" />
    </div>

    <!-- Pagination -->
    <div v-if="(data?.totalCount ?? 0) > pageSize" class="mt-8 flex justify-center gap-2">
      <button
        :disabled="page === 1"
        @click="page--"
        class="btn-outline disabled:opacity-40"
      >{{ t('product.prev') }}</button>
      <span class="flex items-center px-3 text-sm text-gray-500">
        {{ t('product.pageOf', { page, totalPages }) }}
      </span>
      <button
        :disabled="page >= totalPages"
        @click="page++"
        class="btn-outline disabled:opacity-40"
      >{{ t('product.next') }}</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { productsApi } from '@/api/products.api'
import ProductCard from '@/components/product/ProductCard.vue'

const { t } = useI18n()
const search = ref('')
const page = ref(1)
const pageSize = 20
const sortBy = ref<'newest' | 'price_asc' | 'price_desc' | 'name'>('newest')

const { data, isLoading } = useQuery({
  queryKey: computed(() => ['products', { search: search.value, page: page.value, sortBy: sortBy.value }]),
  queryFn: () =>
    productsApi.list({ search: search.value || undefined, page: page.value, pageSize, sortBy: sortBy.value }),
})

const totalPages = computed(() => Math.ceil((data.value?.totalCount ?? 0) / pageSize))
</script>
