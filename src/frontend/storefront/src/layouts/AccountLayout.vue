<template>
  <div class="mx-auto grid max-w-5xl gap-8 px-4 py-10 md:grid-cols-[14rem_1fr]">
    <nav :aria-label="t('account.menu')" class="md:border-r md:border-line md:pr-6">
      <ul class="flex gap-2 md:flex-col">
        <li v-for="link in LINKS" :key="link.to">
          <RouterLink
            :to="link.to"
            class="flex items-center gap-2 rounded-md px-3 py-2 text-sm font-medium text-ink-secondary hover:bg-surface-muted hover:text-ink"
            active-class="!bg-brand-subtle !text-brand-action"
          >
            <component :is="link.icon" :size="18" :stroke-width="1.75" aria-hidden="true" />{{ t(link.label) }}
          </RouterLink>
        </li>
      </ul>
    </nav>
    <div><RouterView /></div>
  </div>
</template>

<script setup lang="ts">
import { ClipboardList, UserRound } from 'lucide-vue-next'
import { RouterLink, RouterView } from 'vue-router'
import { useI18n } from 'vue-i18n'

const { t } = useI18n()
const LINKS = [
  { to: '/account/orders', label: 'account.myOrders', icon: ClipboardList },
  { to: '/account/profile', label: 'account.myProfile', icon: UserRound },
]
</script>
