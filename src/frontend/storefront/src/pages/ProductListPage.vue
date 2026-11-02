<template>
  <div class="mx-auto max-w-7xl px-4 py-8">
    <h1 class="mb-6 text-2xl font-bold text-ink">{{ t('nav.products') }}</h1>

    <div class="mb-6 flex flex-wrap items-end gap-4">
      <FormField :label="t('common.search')" class="w-full sm:max-w-xs">
        <Input v-model="search" type="search" :placeholder="t('product.searchPlaceholder')" @input="page = 1" />
      </FormField>
      <FormField :label="t('product.sortBy')">
        <Select v-model="sortBy">
          <option value="newest">{{ t('product.sortNewest') }}</option>
          <option value="price_asc">{{ t('product.sortPriceAsc') }}</option>
          <option value="price_desc">{{ t('product.sortPriceDesc') }}</option>
          <option value="name">{{ t('product.sortName') }}</option>
        </Select>
      </FormField>
    </div>

    <ProductGrid :products="data?.items ?? []" :loading="isLoading" :failed="isError" :skeletons="12" :card-level="2" @retry="refetch()">
      <template #empty>
        <EmptyState :title="t('product.noProductsFound')" :description="search ? t('product.noProductsHint', { term: search }) : undefined">
          <Button v-if="search" variant="outline" @click="clearSearch">{{ t('product.clearSearch') }}</Button>
        </EmptyState>
      </template>
    </ProductGrid>

    <div class="mt-8">
      <Pagination
        v-model:page="page"
        :total-pages="totalPages"
        :label="t('product.pagination')"
        :prev-label="t('product.prev')"
        :next-label="t('product.next')"
        :page-label="t('product.pageOf', { page, totalPages })"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { productsApi } from '@/api/products.api'
import ProductGrid from '@/components/product/ProductGrid.vue'
import Button from '@/components/ui/Button.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import FormField from '@/components/ui/FormField.vue'
import Input from '@/components/ui/Input.vue'
import Pagination from '@/components/ui/Pagination.vue'
import Select from '@/components/ui/Select.vue'

const { t } = useI18n()
const search = ref('')
const page = ref(1)
const pageSize = 20
const sortBy = ref<'newest' | 'price_asc' | 'price_desc' | 'name'>('newest')

const { data, isLoading, isError, refetch } = useQuery({
  queryKey: computed(() => ['products', { search: search.value, page: page.value, sortBy: sortBy.value }]),
  queryFn: () =>
    productsApi.list({ search: search.value || undefined, page: page.value, pageSize, sortBy: sortBy.value }),
})

const totalPages = computed(() => Math.ceil((data.value?.totalCount ?? 0) / pageSize))

function clearSearch() {
  search.value = ''
  page.value = 1
}
</script>
