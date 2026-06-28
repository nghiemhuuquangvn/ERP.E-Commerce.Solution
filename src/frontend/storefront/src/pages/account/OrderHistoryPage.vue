<template>
  <div class="mx-auto max-w-3xl px-4 py-10">
    <h1 class="mb-6 text-2xl font-bold text-gray-900">My Orders</h1>

    <div v-if="isLoading" class="text-gray-400">Loading...</div>

    <div v-else-if="!orders?.length" class="py-10 text-center text-gray-400">
      You haven't placed any orders yet.
    </div>

    <div v-else class="space-y-4">
      <div v-for="order in orders" :key="order.id" class="rounded-lg border border-gray-200 p-5">
        <div class="flex items-start justify-between">
          <div>
            <p class="font-medium text-gray-800">{{ order.orderNumber }}</p>
            <p class="text-sm text-gray-500">{{ fmt(new Date(order.orderDate)) }}</p>
          </div>
          <span :class="statusClass(order.status)" class="badge">{{ order.status }}</span>
        </div>
        <p class="mt-3 text-sm text-gray-600">
          {{ order.lines.length }} item(s) ·
          <span class="font-semibold">{{ fmtCurrency(order.totalAmount) }}</span>
        </p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from '@tanstack/vue-query'
import { format } from 'date-fns'
import { ordersApi } from '@/api/orders.api'

const { data: orders, isLoading } = useQuery({
  queryKey: ['my-orders'],
  queryFn: ordersApi.myOrders,
})

const fmt = (d: Date) => format(d, 'dd MMM yyyy')
const fmtCurrency = (n: number) =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)

const STATUS_COLORS: Record<string, string> = {
  Draft: 'bg-gray-100 text-gray-600',
  Confirmed: 'bg-blue-100 text-blue-700',
  Shipped: 'bg-purple-100 text-purple-700',
  Delivered: 'bg-green-100 text-green-700',
  Cancelled: 'bg-red-100 text-red-700',
}

function statusClass(status: string) {
  return STATUS_COLORS[status] ?? 'bg-gray-100 text-gray-600'
}
</script>
