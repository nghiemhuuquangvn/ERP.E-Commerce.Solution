<template>
  <div class="mx-auto max-w-lg px-4 py-20 text-center">
    <h1 class="text-2xl font-bold text-ink">{{ t('payment.resultTitle') }}</h1>

    <!-- The browser's return is only a hint; the page always shows what the payment really is. -->
    <Alert v-if="route.query.status === 'invalid'" variant="warning" class="mt-3 text-left">{{ t('payment.invalid') }}</Alert>

    <p v-if="!orderId" class="mt-8 text-ink-secondary">{{ t('payment.failed') }}</p>
    <p v-else-if="isLoading || (!payment && !timedOut)" role="status" class="mt-8 flex items-center justify-center gap-2 text-ink-secondary">
      <Spinner :size="18" />{{ t('payment.checking') }}
    </p>

    <div v-else-if="payment?.status === 'Captured' || payment?.status === 'Refunded' || payment?.status === 'PartiallyRefunded'" class="mt-8 space-y-2" role="status">
      <div class="flex justify-center text-success"><CheckCircle2 :size="48" :stroke-width="1.75" aria-hidden="true" /></div>
      <p class="text-lg font-semibold text-success-soft-text">{{ t('payment.paid') }}</p>
      <p class="text-sm text-ink-secondary">{{ t('payment.paidDetail', { amount: money(payment.amount, payment.currency) }) }}</p>
    </div>

    <div v-else-if="payment?.status === 'Failed'" class="mt-8 space-y-2" role="alert">
      <div class="flex justify-center text-danger"><XCircle :size="48" :stroke-width="1.75" aria-hidden="true" /></div>
      <p class="text-lg font-semibold text-danger-soft-text">{{ t('payment.failed') }}</p>
      <p class="text-sm text-ink-secondary">{{ t('payment.failedDetail') }}</p>
      <p v-if="payment.failureReason" class="text-xs text-ink-secondary">{{ payment.failureReason }}</p>
    </div>

    <div v-else-if="payment?.status === 'Cancelled'" class="mt-8 space-y-2" role="status">
      <div class="flex justify-center text-ink-muted"><Ban :size="48" :stroke-width="1.75" aria-hidden="true" /></div>
      <p class="text-lg font-semibold text-ink">{{ t('payment.cancelled') }}</p>
    </div>

    <div v-else class="mt-8 space-y-3" role="status">
      <div class="flex justify-center text-warning"><Clock :size="48" :stroke-width="1.75" aria-hidden="true" /></div>
      <p class="text-lg font-semibold text-ink">{{ t('payment.pending') }}</p>
      <p class="text-sm text-ink-secondary">
        {{ route.query.status === 'paid' ? t('payment.waitingConfirm') : t('payment.pendingDetail') }}
      </p>
      <Button v-if="payment?.provider !== 'COD'" :loading="starting" @click="payAgain">
        {{ route.query.status === 'pending' ? t('payment.payNow') : t('payment.payAgain') }}
      </Button>
      <Alert v-if="startError" variant="danger" class="text-left">{{ startError }}</Alert>
    </div>

    <div class="mt-10 flex flex-wrap justify-center gap-3">
      <Button to="/account/orders">{{ t('payment.viewOrders') }}</Button>
      <Button to="/products" variant="outline">{{ t('payment.continueShopping') }}</Button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { Ban, CheckCircle2, Clock, XCircle } from 'lucide-vue-next'
import { computed, onBeforeUnmount, ref } from 'vue'
import { useRoute } from 'vue-router'
import { useQuery } from '@tanstack/vue-query'
import { useI18n } from 'vue-i18n'
import { paymentsApi } from '@/api/payments.api'
import Alert from '@/components/ui/Alert.vue'
import Button from '@/components/ui/Button.vue'
import Spinner from '@/components/ui/Spinner.vue'
import { useMoney } from '@/composables/useMoney'

const { t } = useI18n()
const { money } = useMoney()
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
