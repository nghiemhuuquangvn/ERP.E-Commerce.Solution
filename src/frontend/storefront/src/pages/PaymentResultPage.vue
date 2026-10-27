<template>
  <div class="mx-auto max-w-lg px-4 py-20 text-center">
    <h1 class="text-2xl font-bold text-gray-900">{{ t('payment.resultTitle') }}</h1>

    <!-- The browser's return is only a hint; the page always shows what the payment really is. -->
    <p v-if="route.query.status === 'invalid'" class="mt-3 rounded bg-amber-50 p-3 text-sm text-amber-800">{{ t('payment.invalid') }}</p>

    <div v-if="!orderId" class="mt-8 text-gray-500">{{ t('payment.failed') }}</div>
    <div v-else-if="isLoading || (!payment && !timedOut)" class="mt-8 text-gray-400">{{ t('payment.checking') }}</div>

    <div v-else-if="payment?.status === 'Captured' || payment?.status === 'Refunded' || payment?.status === 'PartiallyRefunded'" class="mt-8 space-y-2">
      <div class="text-5xl">✅</div>
      <p class="text-lg font-semibold text-green-700">{{ t('payment.paid') }}</p>
      <p class="text-sm text-gray-600">{{ t('payment.paidDetail', { amount: money(payment.amount, payment.currency) }) }}</p>
    </div>

    <div v-else-if="payment?.status === 'Failed'" class="mt-8 space-y-2">
      <div class="text-5xl">❌</div>
      <p class="text-lg font-semibold text-red-700">{{ t('payment.failed') }}</p>
      <p class="text-sm text-gray-600">{{ t('payment.failedDetail') }}</p>
      <p v-if="payment.failureReason" class="text-xs text-gray-400">{{ payment.failureReason }}</p>
    </div>

    <div v-else-if="payment?.status === 'Cancelled'" class="mt-8">
      <p class="text-lg font-semibold text-gray-700">{{ t('payment.cancelled') }}</p>
    </div>

    <div v-else class="mt-8 space-y-3">
      <div class="text-5xl">⏳</div>
      <p class="text-lg font-semibold text-gray-800">{{ t('payment.pending') }}</p>
      <p class="text-sm text-gray-600">
        {{ route.query.status === 'paid' ? t('payment.waitingConfirm') : t('payment.pendingDetail') }}
      </p>
      <button v-if="payment?.provider !== 'COD'" class="btn-brand" :disabled="starting" @click="payAgain">
        {{ route.query.status === 'pending' ? t('payment.payNow') : t('payment.payAgain') }}
      </button>
      <p v-if="startError" class="text-sm text-red-600">{{ startError }}</p>
    </div>

    <div class="mt-10 flex justify-center gap-3">
      <RouterLink to="/account/orders" class="btn-brand">{{ t('payment.viewOrders') }}</RouterLink>
      <RouterLink to="/products" class="btn-outline">{{ t('payment.continueShopping') }}</RouterLink>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { useRoute, RouterLink } from 'vue-router'
import { useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { paymentsApi } from '@/api/payments.api'

const { t } = useI18n()
const route = useRoute()
const orderId = computed(() => (route.query.orderId as string | undefined) ?? '')

const starting = ref(false)
const startError = ref('')
const timedOut = ref(false)

// Poll until the payment settles: the gateway's server notification can arrive a little after the customer's browser does.
const FINAL = ['Captured', 'Failed', 'Cancelled', 'Refunded', 'PartiallyRefunded']
const startedAt = Date.now()
const { data: payment, isLoading } = useQuery({
  queryKey: ['order-payment', orderId],
  queryFn: () => paymentsApi.status(orderId.value).catch(() => null),
  enabled: computed(() => !!orderId.value),
  refetchInterval: (query) => {
    const status = query.state.data?.status
    if (status && FINAL.includes(status)) return false
    if (Date.now() - startedAt > 60_000) {
      timedOut.value = true
      return false
    }
    return 2000
  },
})
const timer = window.setTimeout(() => (timedOut.value = true), 15_000)
onBeforeUnmount(() => window.clearTimeout(timer))

const money = (amount: number, currency: string) =>
  new Intl.NumberFormat(currency === 'VND' ? 'vi-VN' : 'en-US', { style: 'currency', currency, maximumFractionDigits: currency === 'VND' ? 0 : 2 }).format(amount)

async function payAgain() {
  starting.value = true
  startError.value = ''
  try {
    const { paymentUrl } = await paymentsApi.checkout(orderId.value)
    window.location.href = paymentUrl
  } catch {
    startError.value = t('payment.startFailed')
  } finally {
    starting.value = false
  }
}
</script>
