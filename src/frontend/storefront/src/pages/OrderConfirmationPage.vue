<template>
  <div class="mx-auto max-w-lg px-4 py-20 text-center">
    <div class="mb-4 flex justify-center text-success"><CheckCircle2 :size="48" :stroke-width="1.75" aria-hidden="true" /></div>
    <h1 class="text-2xl font-bold text-ink">{{ t('orders.orderPlaced') }}</h1>
    <p class="mt-2 text-ink-secondary">
      {{ t('orders.orderReceived', { orderNumber: order?.orderNo ?? '' }) }}
    </p>
    <p v-if="order" class="mt-2 text-sm text-ink-secondary">
      {{ t('orders.totalLine', { total: vnd(order.totalAmount) }) }}
      <span v-if="order.shippingFee" class="text-ink-secondary">({{ t('orders.includesDelivery', { fee: vnd(order.shippingFee) }) }})</span>
      <span v-else class="font-medium text-success-soft-text">({{ t('checkout.freeDelivery') }})</span>
    </p>
    <p v-if="order?.paymentMethod === 'COD'" class="mt-2 text-sm text-ink-secondary">{{ t('orders.payOnDelivery') }}</p>
    <p v-else-if="payment" class="mt-2 text-sm text-ink-secondary">{{ t('payment.statusLine', { status: t(`payment.status.${payment.status}`) }) }}</p>
    <Button v-if="payment?.status === 'Pending' && payment.provider !== 'COD'" :to="`/payment/result?orderId=${route.params.id}&status=pending`" class="mt-4">
      {{ t('payment.payNow') }}
    </Button>
    <div class="mt-8 flex flex-wrap justify-center gap-3">
      <Button to="/account/orders">{{ t('orders.viewMyOrders') }}</Button>
      <Button to="/products" variant="outline">{{ t('orders.continueShopping') }}</Button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { CheckCircle2 } from 'lucide-vue-next'
import { useRoute } from 'vue-router'
import { useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { ordersApi } from '@/api/orders.api'
import { paymentsApi } from '@/api/payments.api'
import Button from '@/components/ui/Button.vue'
import { useMoney } from '@/composables/useMoney'

const { t } = useI18n()
const { vnd } = useMoney()
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
