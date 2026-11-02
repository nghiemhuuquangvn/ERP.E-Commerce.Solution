<template>
  <div>
    <h1 class="mb-6 text-2xl font-bold text-ink">{{ t('account.myOrders') }}</h1>

    <div v-if="isLoading" class="space-y-4" role="status" :aria-label="t('common.loading')">
      <Skeleton v-for="n in 3" :key="n" class="h-24" />
    </div>

    <Alert v-else-if="isError" variant="danger">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <span>{{ t('account.ordersLoadFailed') }}</span>
        <Button variant="outline" size="sm" @click="refetch()">{{ t('common.retry') }}</Button>
      </div>
    </Alert>

    <EmptyState v-else-if="!orders?.length" :title="t('orders.noOrdersYet')">
      <Button to="/products">{{ t('cart.browseProducts') }}</Button>
    </EmptyState>

    <ul v-else class="space-y-4">
      <li v-for="order in orders" :key="order.orderId" class="rounded-lg border border-line p-5">
        <div class="flex items-start justify-between gap-3">
          <div>
            <p class="font-medium text-ink">{{ order.orderNo }}</p>
            <p class="text-sm text-ink-secondary">{{ fmtDate(new Date(order.orderDate)) }}</p>
          </div>
          <Badge :variant="statusVariant(order.status)">{{ statusLabel(order.status) }}</Badge>
        </div>
        <p class="mt-3"><PriceText :amount="order.totalAmount" /></p>
      </li>
    </ul>
  </div>
</template>

<script setup lang="ts">
import { useQuery } from '@tanstack/vue-query'
import { format } from 'date-fns'
import { useI18n } from 'vue-i18n'
import { ordersApi } from '@/api/orders.api'
import Alert from '@/components/ui/Alert.vue'
import Badge, { type BadgeVariant } from '@/components/ui/Badge.vue'
import Button from '@/components/ui/Button.vue'
import EmptyState from '@/components/ui/EmptyState.vue'
import PriceText from '@/components/ui/PriceText.vue'
import Skeleton from '@/components/ui/Skeleton.vue'

const { t, te } = useI18n()

const { data: orders, isLoading, isError, refetch } = useQuery({
  queryKey: ['my-orders'],
  queryFn: ordersApi.myOrders,
})

const fmtDate = (d: Date) => format(d, 'dd MMM yyyy')

const VARIANT: Record<string, BadgeVariant> = {
  Confirmed: 'brand',
  Shipped: 'warning',
  Delivered: 'success',
  Cancelled: 'danger',
}
const statusVariant = (status: string): BadgeVariant => VARIANT[status] ?? 'neutral'
// Known statuses are translated; any other status the back office adds is shown as it comes.
const statusLabel = (status: string) => (te(`orders.status.${status}`) ? t(`orders.status.${status}`) : status)
</script>
