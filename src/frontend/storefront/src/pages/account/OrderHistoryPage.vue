<template>
  <div class="mx-auto max-w-3xl px-4 py-10">
    <h1 class="mb-6 text-2xl font-bold text-ink">My Orders</h1>

    <div v-if="isLoading" class="text-ink-muted">Loading...</div>

    <div v-else-if="!orders?.length" class="py-10 text-center text-ink-muted">
      You haven't placed any orders yet.
    </div>

    <div v-else class="space-y-4">
      <div v-for="order in orders" :key="order.orderId" class="rounded-lg border border-line p-5">
        <div class="flex items-start justify-between">
          <div>
            <p class="font-medium text-ink">{{ order.orderNo }}</p>
            <p class="text-sm text-ink-muted">{{ fmt(new Date(order.orderDate)) }}</p>
          </div>
          <span :class="statusClass(order.status)" class="badge">{{ order.status }}</span>
        </div>
        <p class="mt-3 text-sm text-ink-secondary">
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
  Draft: 'bg-surface-muted text-ink-secondary',
  Confirmed: 'bg-info-soft text-info-soft-text',
  Shipped: 'bg-purple-100 text-purple-700',
  Delivered: 'bg-success-soft text-success-soft-text',
  Cancelled: 'bg-danger-soft text-danger-soft-text',
}

function statusClass(status: string) {
  return STATUS_COLORS[status] ?? 'bg-surface-muted text-ink-secondary'
}
</script>
