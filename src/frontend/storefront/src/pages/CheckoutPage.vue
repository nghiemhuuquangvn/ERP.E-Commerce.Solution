<template>
  <div class="mx-auto max-w-3xl px-4 py-10">
    <h1 class="mb-6 text-2xl font-bold text-gray-900">{{ t('checkout.title') }}</h1>

    <!-- Steps -->
    <div class="mb-8 flex items-center gap-4">
      <StepIndicator :step="1" :current="currentStep" :label="t('checkout.stepShipping')" />
      <div class="flex-1 border-t border-gray-200" />
      <StepIndicator :step="2" :current="currentStep" :label="t('checkout.stepPayment')" />
      <div class="flex-1 border-t border-gray-200" />
      <StepIndicator :step="3" :current="currentStep" :label="t('checkout.stepReview')" />
    </div>

    <!-- Step 1: Shipping Address -->
    <div v-if="currentStep === 1" class="card p-6 space-y-4">
      <h2 class="font-semibold text-gray-900">{{ t('checkout.shippingAddress') }}</h2>
      <input v-model="address.street" class="input" :placeholder="t('checkout.streetAddress')" />
      <input v-model="address.ward" class="input" :placeholder="t('checkout.ward')" />
      <input v-model="phone" type="tel" autocomplete="tel" class="input" :placeholder="t('checkout.phone')" />
      <div class="grid grid-cols-2 gap-3">
        <input v-model="address.city" class="input" :placeholder="t('checkout.city')" />
        <input v-model="address.state" class="input" :placeholder="t('checkout.state')" />
      </div>
      <div class="grid grid-cols-2 gap-3">
        <input v-model="address.postalCode" class="input" :placeholder="t('checkout.postalCode')" />
        <input v-model="address.country" class="input" :placeholder="t('checkout.country')" />
      </div>
      <button @click="currentStep = 2" :disabled="!addressValid" class="btn-brand disabled:opacity-40">
        {{ t('checkout.continueArrow') }}
      </button>
    </div>

    <!-- Step 2: Payment -->
    <div v-else-if="currentStep === 2" class="card p-6 space-y-4">
      <h2 class="font-semibold text-gray-900">{{ t('checkout.paymentMethod') }}</h2>
      <p v-if="methodsLoading" class="text-sm text-gray-400">{{ t('common.loading') }}</p>
      <p v-else-if="!paymentMethods.length" class="text-sm text-red-600">{{ t('checkout.noPaymentMethods') }}</p>
      <div v-else class="space-y-2">
        <label v-for="m in paymentMethods" :key="m.value" class="flex items-start gap-3 cursor-pointer">
          <input type="radio" :value="m.value" v-model="paymentMethod" class="mt-1 accent-brand-500" />
          <span class="text-sm">
            {{ m.label }}
            <span class="block text-xs text-gray-400">{{ m.hint }}</span>
          </span>
        </label>
      </div>
      <div class="flex gap-3">
        <button @click="currentStep = 1" class="btn-outline">{{ t('checkout.backArrow') }}</button>
        <button @click="currentStep = 3" :disabled="!paymentMethod" class="btn-brand disabled:opacity-40">{{ t('checkout.reviewOrderArrow') }}</button>
      </div>
    </div>

    <!-- Step 3: Review & Place -->
    <div v-else class="card p-6 space-y-4">
      <h2 class="font-semibold text-gray-900">{{ t('checkout.reviewYourOrder') }}</h2>
      <div class="text-sm text-gray-600 space-y-1">
        <p><span class="font-medium">{{ t('checkout.shipTo') }}</span> {{ [address.street, address.ward, address.city, address.state, address.country].filter(Boolean).join(', ') }}</p>
        <p><span class="font-medium">{{ t('checkout.payment') }}</span> {{ paymentMethodLabel }}</p>
      </div>
      <div class="border-t pt-3 text-sm space-y-1">
        <div v-for="item in items" :key="item.productId" class="flex justify-between">
          <span>{{ item.productName }} × {{ item.quantity }}</span>
          <span>{{ fmt(item.unitPrice * item.quantity) }}</span>
        </div>
        <div class="flex justify-between pt-1 border-t">
          <span>{{ t('checkout.goods') }}</span>
          <span>{{ fmt(total) }}</span>
        </div>
        <div class="flex justify-between">
          <span>{{ t('checkout.deliveryFee') }}</span>
          <span v-if="feeLoading" class="text-gray-400">{{ t('common.loading') }}</span>
          <span v-else-if="feeError" class="text-amber-700">{{ t('checkout.feeUnavailable') }}</span>
          <span v-else-if="deliveryFee === 0" class="text-green-700">{{ t('checkout.freeDelivery') }}</span>
          <span v-else>{{ fmt(deliveryFee) }}</span>
        </div>
        <p v-if="!feeLoading && !feeError && !feeQuote?.isFree && feeQuote && feeQuote.freeShippingThreshold > 0 && deliveryFee > 0" class="text-xs text-gray-400">
          {{ t('checkout.freeFrom', { amount: fmt(feeQuote.freeShippingThreshold) }) }}
        </p>
        <div class="flex justify-between font-semibold pt-1 border-t">
          <span>{{ t('common.total') }}</span>
          <span>{{ fmt(total + deliveryFee) }}</span>
        </div>
      </div>
      <div class="flex gap-3">
        <button @click="currentStep = 2" class="btn-outline">{{ t('checkout.backArrow') }}</button>
        <button @click="placeOrder" :disabled="placing" class="btn-brand flex-1 disabled:opacity-40">
          {{ placing ? t('checkout.placingOrder') : isOnline ? t('checkout.placeOrderAndPay') : t('checkout.placeOrder') }}
        </button>
      </div>
      <p v-if="error" class="text-sm text-red-600">{{ error }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useCartStore } from '@/stores/cart.store'
import { useAuthStore } from '@/stores/auth.store'
import { ordersApi } from '@/api/orders.api'
import { paymentsApi } from '@/api/payments.api'
import { shippingApi, type ShippingFeeQuote } from '@/api/shipping.api'

const { t } = useI18n()
const cartStore = useCartStore()
const authStore = useAuthStore()
const router = useRouter()

const currentStep = ref(1)
const placing = ref(false)
const error = ref('')

const address = ref({ street: '', ward: '', city: '', state: '', postalCode: '', country: '' })
// The carrier calls this number: it is part of the delivery address.
const phone = ref('')
// What this shop really offers (cash on delivery + the gateways it has set up), not a fixed list.
const paymentMethod = ref('')
const methodsLoading = ref(true)
const offered = ref<{ code: string; isGateway: boolean }[]>([])
const paymentMethods = computed(() =>
  offered.value.map((m) => ({
    value: m.code,
    label: t(`checkout.methods.${m.code}`),
    hint: t(`checkout.methodHints.${m.code}`),
  })),
)
const isOnline = computed(() => offered.value.find((m) => m.code === paymentMethod.value)?.isGateway ?? false)

onMounted(async () => {
  try {
    offered.value = await paymentsApi.methods()
    paymentMethod.value = offered.value[0]?.code ?? ''
  } catch {
    offered.value = []
  } finally {
    methodsLoading.value = false
  }
})
const paymentMethodLabel = computed(
  () => paymentMethods.value.find((m) => m.value === paymentMethod.value)?.label ?? paymentMethod.value,
)

const items = computed(() => cartStore.items)
const total = computed(() => cartStore.total)

// What the shop charges for delivery: shown when the customer reviews the order. It is a preview - the server works the fee out again when the order is placed.
const feeQuote = ref<ShippingFeeQuote | null>(null)
const feeLoading = ref(false)
const feeError = ref(false)
const deliveryFee = computed(() => feeQuote.value?.fee ?? 0)
watch(currentStep, async (step) => {
  if (step !== 3) return
  feeLoading.value = true
  feeError.value = false
  try {
    feeQuote.value = await shippingApi.feeFor(
      total.value,
      address.value.state,
      items.value.map((i) => ({ productId: i.productId, quantity: i.quantity })),
    )
  } catch {
    feeQuote.value = null
    feeError.value = true
  } finally {
    feeLoading.value = false
  }
})
const phoneValid = computed(() => /^\+?[0-9 .()-]{8,20}$/.test(phone.value.trim()) && phone.value.replace(/\D/g, '').length >= 8)
const addressValid = computed(() =>
  address.value.street && address.value.city && address.value.country && phoneValid.value,
)

// Online gateways settle Vietnamese dong; the cart carries plain amounts, so show them as VND when an online method is chosen.
const fmt = (n: number) =>
  isOnline.value || paymentMethod.value === 'COD'
    ? new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(n)
    : new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)

async function placeOrder() {
  if (!authStore.user) return
  placing.value = true
  error.value = ''
  try {
    const order = await ordersApi.create({
      customerId: authStore.user.id,
      customerName: authStore.user.fullName || authStore.user.email,
      customerEmail: authStore.user.email,
      shippingAddress: address.value,
      customerPhone: phone.value.trim(),
      lineItems: items.value.map((i) => ({
        productId: i.productId,
        productName: i.productName,
        unitPrice: i.unitPrice,
        quantity: i.quantity,
      })),
      paymentMethod: paymentMethod.value,
    })
    cartStore.clear()

    if (isOnline.value) {
      // The order exists; now send the customer to the gateway. If the gateway cannot be reached the result page offers "Pay again".
      try {
        const { paymentUrl } = await paymentsApi.checkout(order.orderId)
        window.location.href = paymentUrl
        return
      } catch {
        router.push(`/payment/result?orderId=${order.orderId}&status=pending`)
        return
      }
    }
    router.push(`/order-confirmation/${order.orderId}`)
  } catch {
    error.value = t('checkout.placeOrderError')
  } finally {
    placing.value = false
  }
}

// Inline step indicator component
</script>

<script lang="ts">
import { defineComponent, h } from 'vue'

const StepIndicator = defineComponent({
  props: { step: Number, current: Number, label: String },
  setup(props) {
    return () => {
      const done = (props.current ?? 0) > (props.step ?? 0)
      const active = (props.current ?? 0) === (props.step ?? 0)
      return h('div', { class: 'flex flex-col items-center gap-1' }, [
        h(
          'div',
          {
            class: `flex h-8 w-8 items-center justify-center rounded-full text-sm font-medium ${
              done ? 'bg-green-500 text-white' : active ? 'bg-brand-500 text-white' : 'bg-gray-100 text-gray-400'
            }`,
          },
          done ? '✓' : String(props.step),
        ),
        h('span', { class: 'text-xs text-gray-500' }, props.label),
      ])
    }
  },
})

export { StepIndicator }
</script>
