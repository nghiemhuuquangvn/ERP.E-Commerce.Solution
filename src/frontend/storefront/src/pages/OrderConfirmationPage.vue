<template>
  <div class="mx-auto max-w-lg px-4 py-20 text-center">
    <div class="text-5xl mb-4">🎉</div>
    <h1 class="text-2xl font-bold text-gray-900">{{ t('orders.orderPlaced') }}</h1>
    <p class="mt-2 text-gray-600">
      {{ t('orders.orderReceived', { orderNumber: order?.orderNo ?? '' }) }}
    </p>
    <p v-if="order?.paymentMethod === 'COD'" class="mt-2 text-sm text-gray-500">{{ t('orders.payOnDelivery') }}</p>
    <p v-else-if="payment" class="mt-2 text-sm text-gray-500">{{ t('payment.statusLine', { status: t(`payment.status.${payment.status}`) }) }}</p>
    <RouterLink v-if="payment?.status === 'Pending' && payment.provider !== 'COD'" :to="`/payment/result?orderId=${route.params.id}&status=pending`" class="btn-brand mt-4 inline-block">
      {{ t('payment.payNow') }}
    </RouterLink>
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
import { paymentsApi } from '@/api/payments.api'

const { t } = useI18n()
const route = useRoute()
const { data: order } = useQuery({
  queryKey: ['orders', route.params.id],
  queryFn: () => ordersApi.get(route.params.id as string),
})
// The payment is made by the order saga a moment after the order; an order without one (older orders) just shows nothing.
const { data: payment } = useQuery({
  queryKey: ['order-payment', route.params.id],
  queryFn: () => paymentsApi.status(route.params.id as string).catch(() => null),
})
</script>
