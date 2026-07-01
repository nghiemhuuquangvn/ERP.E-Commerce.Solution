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
      <div class="space-y-2">
        <label v-for="m in paymentMethods" :key="m.value" class="flex items-center gap-3 cursor-pointer">
          <input type="radio" :value="m.value" v-model="paymentMethod" class="accent-brand-500" />
          <span class="text-sm">{{ m.label }}</span>
        </label>
      </div>
      <div class="flex gap-3">
        <button @click="currentStep = 1" class="btn-outline">{{ t('checkout.backArrow') }}</button>
        <button @click="currentStep = 3" class="btn-brand">{{ t('checkout.reviewOrderArrow') }}</button>
      </div>
    </div>

    <!-- Step 3: Review & Place -->
    <div v-else class="card p-6 space-y-4">
      <h2 class="font-semibold text-gray-900">{{ t('checkout.reviewYourOrder') }}</h2>
      <div class="text-sm text-gray-600 space-y-1">
        <p><span class="font-medium">{{ t('checkout.shipTo') }}</span> {{ address.street }}, {{ address.city }}, {{ address.country }}</p>
        <p><span class="font-medium">{{ t('checkout.payment') }}</span> {{ paymentMethodLabel }}</p>
      </div>
      <div class="border-t pt-3 text-sm space-y-1">
        <div v-for="item in items" :key="item.productId" class="flex justify-between">
          <span>{{ item.productName }} × {{ item.quantity }}</span>
          <span>{{ fmt(item.unitPrice * item.quantity) }}</span>
        </div>
        <div class="flex justify-between font-semibold pt-1 border-t">
          <span>{{ t('common.total') }}</span>
          <span>{{ fmt(total) }}</span>
        </div>
      </div>
      <div class="flex gap-3">
        <button @click="currentStep = 2" class="btn-outline">{{ t('checkout.backArrow') }}</button>
        <button @click="placeOrder" :disabled="placing" class="btn-brand flex-1 disabled:opacity-40">
          {{ placing ? t('checkout.placingOrder') : t('checkout.placeOrder') }}
        </button>
      </div>
      <p v-if="error" class="text-sm text-red-600">{{ error }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useCartStore } from '@/stores/cart.store'
import { useAuthStore } from '@/stores/auth.store'
import { ordersApi } from '@/api/orders.api'

const { t } = useI18n()
const cartStore = useCartStore()
const authStore = useAuthStore()
const router = useRouter()

const currentStep = ref(1)
const placing = ref(false)
const error = ref('')

const address = ref({ street: '', city: '', state: '', postalCode: '', country: '' })
const paymentMethod = ref('CreditCard')
const paymentMethods = computed(() => [
  { value: 'CreditCard', label: t('checkout.creditCard') },
  { value: 'BankTransfer', label: t('checkout.bankTransfer') },
  { value: 'EWallet', label: t('checkout.eWallet') },
])
const paymentMethodLabel = computed(
  () => paymentMethods.value.find((m) => m.value === paymentMethod.value)?.label ?? paymentMethod.value,
)

const items = computed(() => cartStore.items)
const total = computed(() => cartStore.total)
const addressValid = computed(() =>
  address.value.street && address.value.city && address.value.country,
)

const fmt = (n: number) =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)

async function placeOrder() {
  if (!authStore.user) return
  placing.value = true
  error.value = ''
  try {
    const order = await ordersApi.create({
      customerId: authStore.user.id,
      shippingAddress: address.value,
      lines: items.value.map((i) => ({
        productId: i.productId,
        productName: i.productName,
        unitPrice: i.unitPrice,
        quantity: i.quantity,
      })),
    })
    cartStore.clear()
    router.push(`/order-confirmation/${order.id}`)
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
