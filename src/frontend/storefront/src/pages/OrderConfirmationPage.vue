<template>
  <div class="mx-auto max-w-lg px-4 py-20 text-center">
    <div class="text-5xl mb-4">🎉</div>
    <h1 class="text-2xl font-bold text-gray-900">Order Placed!</h1>
    <p class="mt-2 text-gray-600">
      Your order <span class="font-medium">{{ order?.orderNumber }}</span> has been received.
      We'll notify you when it ships.
    </p>
    <div class="mt-8 flex justify-center gap-3">
      <RouterLink to="/account/orders" class="btn-brand">View My Orders</RouterLink>
      <RouterLink to="/products" class="btn-outline">Continue Shopping</RouterLink>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRoute, RouterLink } from 'vue-router'
import { useQuery } from '@tanstack/vue-query'
import { ordersApi } from '@/api/orders.api'

const route = useRoute()
const { data: order } = useQuery({
  queryKey: ['orders', route.params.id],
  queryFn: () => ordersApi.get(route.params.id as string),
})
</script>
