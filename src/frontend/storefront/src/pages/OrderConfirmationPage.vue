<template>
  <div class="mx-auto max-w-lg px-4 py-20 text-center">
    <div class="text-5xl mb-4">🎉</div>
    <h1 class="text-2xl font-bold text-gray-900">{{ t('orders.orderPlaced') }}</h1>
    <p class="mt-2 text-gray-600">
      {{ t('orders.orderReceived', { orderNumber: order?.orderNumber ?? '' }) }}
    </p>
    <div class="mt-8 flex justify-center gap-3">
      <RouterLink to="/account/orders" class="btn-brand">{{ t('orders.viewMyOrders') }}</RouterLink>
      <RouterLink to="/products" class="btn-outline">{{ t('orders.continueShopping') }}</RouterLink>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRoute, RouterLink } from 'vue-router'
import { useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { ordersApi } from '@/api/orders.api'

const { t } = useI18n()
const route = useRoute()
const { data: order } = useQuery({
  queryKey: ['orders', route.params.id],
  queryFn: () => ordersApi.get(route.params.id as string),
})
</script>
