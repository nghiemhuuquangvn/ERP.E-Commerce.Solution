<template>
  <div class="mx-auto max-w-3xl px-4 py-10">
    <h1 ref="heading" tabindex="-1" class="mb-6 text-2xl font-bold text-ink outline-none">{{ t('checkout.title') }}</h1>

    <Stepper
      class="mb-8"
      :steps="[t('checkout.stepShipping'), t('checkout.stepPayment'), t('checkout.stepReview')]"
      :current="currentStep"
      :label="t('checkout.stepsLabel')"
    />

    <!-- Step 1: Shipping Address -->
    <form v-if="currentStep === 1" class="rounded-lg border border-line bg-surface shadow-sm space-y-4 p-6" novalidate @submit.prevent="goToPayment">
      <h2 class="font-semibold text-ink">{{ t('checkout.shippingAddress') }}</h2>
      <FormField :label="t('checkout.streetAddress')" required :error="err('street')">
        <Input v-model="address.street" autocomplete="street-address" />
      </FormField>
      <FormField :label="t('checkout.ward')">
        <Input v-model="address.ward" />
      </FormField>
      <FormField :label="t('checkout.phone')" required :error="err('phone')" :hint="t('checkout.phoneHint')">
        <Input v-model="phone" type="tel" autocomplete="tel" />
      </FormField>
      <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <FormField :label="t('checkout.city')" required :error="err('city')">
          <Input v-model="address.city" autocomplete="address-level2" />
        </FormField>
        <FormField :label="t('checkout.state')">
          <Input v-model="address.state" autocomplete="address-level1" />
        </FormField>
      </div>
      <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <FormField :label="t('checkout.postalCode')">
          <Input v-model="address.postalCode" autocomplete="postal-code" />
        </FormField>
        <FormField :label="t('checkout.country')" required :error="err('country')">
          <Input v-model="address.country" autocomplete="country-name" />
        </FormField>
      </div>
      <Button type="submit">{{ t('checkout.continueArrow') }}</Button>
    </form>

    <!-- Step 2: Payment -->
    <section v-else-if="currentStep === 2" class="rounded-lg border border-line bg-surface shadow-sm space-y-4 p-6">
      <h2 class="font-semibold text-ink">{{ t('checkout.paymentMethod') }}</h2>
      <p v-if="methodsLoading" role="status" class="flex items-center gap-2 text-sm text-ink-secondary"><Spinner :size="16" />{{ t('common.loading') }}</p>
      <Alert v-else-if="!paymentMethods.length" variant="warning">{{ t('checkout.noPaymentMethods') }}</Alert>
      <fieldset v-else class="space-y-2">
        <legend class="sr-only">{{ t('checkout.paymentMethod') }}</legend>
        <label v-for="m in paymentMethods" :key="m.value" class="flex cursor-pointer items-start gap-3 rounded-md border border-line p-3 has-[:checked]:border-brand-action has-[:checked]:bg-brand-subtle">
          <input v-model="paymentMethod" type="radio" name="payment-method" :value="m.value" class="mt-1 h-4 w-4 accent-brand-action" />
          <span class="text-sm text-ink">
            {{ m.label }}
            <span class="block text-xs text-ink-secondary">{{ m.hint }}</span>
          </span>
        </label>
      </fieldset>
      <div class="flex gap-3">
        <Button variant="outline" @click="currentStep = 1">{{ t('checkout.backArrow') }}</Button>
        <Button :disabled="!paymentMethod" @click="currentStep = 3">{{ t('checkout.reviewOrderArrow') }}</Button>
      </div>
    </section>

    <!-- Step 3: Review & Place -->
    <section v-else class="rounded-lg border border-line bg-surface shadow-sm space-y-4 p-6">
      <h2 class="font-semibold text-ink">{{ t('checkout.reviewYourOrder') }}</h2>
      <dl class="space-y-1 text-sm text-ink-secondary">
        <div class="flex gap-2"><dt class="font-medium text-ink">{{ t('checkout.shipTo') }}</dt><dd>{{ [address.street, address.ward, address.city, address.state, address.country].filter(Boolean).join(', ') }}</dd></div>
        <div class="flex gap-2"><dt class="font-medium text-ink">{{ t('checkout.payment') }}</dt><dd>{{ paymentMethodLabel }}</dd></div>
      </dl>
      <div class="space-y-1 border-t border-line pt-3 text-sm text-ink">
        <div v-for="item in items" :key="item.productId" class="flex justify-between gap-3">
          <span>{{ item.productName }} × {{ item.quantity }}</span>
          <span class="tabular-nums">{{ fmt(item.unitPrice * item.quantity) }}</span>
        </div>
        <div class="flex justify-between border-t border-line pt-1">
          <span>{{ t('checkout.goods') }}</span>
          <span class="tabular-nums">{{ fmt(total) }}</span>
        </div>
        <div class="flex justify-between">
          <span>{{ t('checkout.deliveryFee') }}</span>
          <span v-if="feeLoading" class="text-ink-secondary" role="status">{{ t('common.loading') }}</span>
          <span v-else-if="feeError" class="text-warning-soft-text">{{ t('checkout.feeUnavailable') }}</span>
          <span v-else-if="deliveryFee === 0" class="font-medium text-success-soft-text">{{ t('checkout.freeDelivery') }}</span>
          <span v-else class="tabular-nums">{{ fmt(deliveryFee) }}</span>
        </div>
        <p v-if="!feeLoading && !feeError && !feeQuote?.isFree && feeQuote && feeQuote.freeShippingThreshold > 0 && deliveryFee > 0" class="text-xs text-ink-secondary">
          {{ t('checkout.freeFrom', { amount: fmt(feeQuote.freeShippingThreshold) }) }}
        </p>
        <div class="flex justify-between border-t border-line pt-1 font-semibold">
          <span>{{ t('common.total') }}</span>
          <span class="tabular-nums">{{ fmt(total + deliveryFee) }}</span>
        </div>
      </div>
      <Alert v-if="error" variant="danger">{{ error }}</Alert>
      <div class="flex gap-3">
        <Button variant="outline" @click="currentStep = 2">{{ t('checkout.backArrow') }}</Button>
        <Button class="flex-1" size="lg" :loading="placing" @click="placeOrder">
          {{ placing ? t('checkout.placingOrder') : placeLabel }}
        </Button>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, nextTick, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import Alert from '@/components/ui/Alert.vue'
import Button from '@/components/ui/Button.vue'
import FormField from '@/components/ui/FormField.vue'
import Input from '@/components/ui/Input.vue'
import Spinner from '@/components/ui/Spinner.vue'
import Stepper from '@/components/ui/Stepper.vue'
import { useMoney } from '@/composables/useMoney'
import { useCartStore } from '@/stores/cart.store'
import { useAuthStore } from '@/stores/auth.store'
import { ordersApi } from '@/api/orders.api'
import { paymentsApi } from '@/api/payments.api'
import { shippingApi, type ShippingFeeQuote } from '@/api/shipping.api'

const { t } = useI18n()
const { usd, vnd } = useMoney()
const cartStore = useCartStore()
const authStore = useAuthStore()
const router = useRouter()

const currentStep = ref(1)
const placing = ref(false)
const error = ref('')
const heading = ref<HTMLElement | null>(null)

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
  await nextTick()
  heading.value?.focus()
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

// The fields that are needed, and what is wrong with each; shown under the field once the customer tried to continue.
const attempted = ref(false)
function err(field: 'street' | 'phone' | 'city' | 'country'): string | undefined {
  if (!attempted.value) return undefined
  if (field === 'phone') return phone.value.trim() ? (phoneValid.value ? undefined : t('checkout.phoneInvalid')) : t('common.required')
  return address.value[field] ? undefined : t('common.required')
}
function goToPayment() {
  attempted.value = true
  if (addressValid.value) currentStep.value = 2
}

// Online gateways settle Vietnamese dong; the cart carries plain amounts, so show them as VND when an online method is chosen.
const fmt = (n: number) => (isOnline.value || paymentMethod.value === 'COD' ? vnd(n) : usd(n))

// The pay button says what will be charged, once the delivery fee is known.
const placeLabel = computed(() => {
  const label = isOnline.value ? t('checkout.placeOrderAndPay') : t('checkout.placeOrder')
  return !feeLoading.value && !feeError.value ? `${label} · ${fmt(total.value + deliveryFee.value)}` : label
})

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
</script>
