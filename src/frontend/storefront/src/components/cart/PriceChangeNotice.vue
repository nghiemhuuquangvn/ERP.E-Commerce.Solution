<template>
  <Alert v-if="notice.changes.length" variant="warning" class="mb-6" data-testid="price-notice">
    <div class="flex items-start justify-between gap-3">
      <div>
        <p class="font-medium">{{ t('cart.priceChangedTitle') }}</p>
        <ul class="mt-1 list-disc pl-5">
          <li v-for="change in notice.changes" :key="change.productId">
            {{
              change.now > 0
                ? t('cart.priceChangedLine', { name: change.productName, was: usd(change.was), now: usd(change.now) })
                : t('cart.priceRemovedLine', { name: change.productName })
            }}
          </li>
        </ul>
        <p class="mt-1">{{ t('cart.priceChangedHint') }}</p>
      </div>
      <Button variant="link" size="sm" @click="notice.dismiss()">{{ t('cart.priceChangedDismiss') }}</Button>
    </div>
  </Alert>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import Alert from '@/components/ui/Alert.vue'
import Button from '@/components/ui/Button.vue'
import { useMoney } from '@/composables/useMoney'
import { usePriceNoticeStore } from '@/stores/priceNotice.store'

const { t } = useI18n()
const { usd } = useMoney()
const notice = usePriceNoticeStore()
</script>
