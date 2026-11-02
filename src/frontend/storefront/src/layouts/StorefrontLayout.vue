<template>
  <div class="flex min-h-screen flex-col bg-surface">
    <a href="#main" class="sr-only z-toast rounded-md bg-surface px-4 py-2 text-sm font-medium text-brand-action shadow-md focus:not-sr-only focus:fixed focus:left-4 focus:top-4">
      {{ t('nav.skipToContent') }}
    </a>

    <header class="sticky top-0 z-header border-b border-line bg-surface">
      <div class="mx-auto flex h-16 max-w-7xl items-center justify-between gap-4 px-4">
        <div class="flex items-center gap-3">
          <button
            type="button"
            class="rounded-md p-2 text-ink-secondary hover:bg-surface-muted md:hidden"
            :aria-label="t('nav.openMenu')"
            :aria-expanded="menuOpen"
            @click="menuOpen = true"
          >
            <Menu :size="24" :stroke-width="1.75" aria-hidden="true" />
          </button>
          <RouterLink to="/" class="text-xl font-bold text-brand-action">{{ t('nav.brand') }}</RouterLink>
        </div>

        <nav class="hidden items-center gap-6 text-sm font-medium text-ink-secondary md:flex" :aria-label="t('nav.main')">
          <RouterLink to="/products" class="hover:text-ink" active-class="text-brand-action">{{ t('nav.products') }}</RouterLink>
        </nav>

        <div class="flex items-center gap-3">
          <Select :model-value="localeStore.locale" class="!h-8 !w-auto !py-0 text-xs" :aria-label="t('common.language')" @update:model-value="onLocaleChange">
            <option value="en">EN</option>
            <option value="vi">VI</option>
          </Select>

          <RouterLink to="/cart" class="relative rounded-md p-2 text-ink-secondary hover:bg-surface-muted hover:text-ink" :aria-label="cartLabel">
            <ShoppingCart :size="24" :stroke-width="1.75" aria-hidden="true" />
            <span v-if="cartCount > 0" class="absolute -right-0.5 -top-0.5" aria-hidden="true"><Badge variant="brand">{{ cartCount }}</Badge></span>
          </RouterLink>

          <div class="hidden items-center gap-3 text-sm md:flex">
            <template v-if="isAuthenticated">
              <RouterLink to="/account/orders" class="flex items-center gap-1 text-ink-secondary hover:text-ink">
                <User :size="18" :stroke-width="1.75" aria-hidden="true" />{{ fullName }}
              </RouterLink>
              <button type="button" class="text-ink-secondary hover:text-ink" @click="handleLogout">{{ t('nav.signOut') }}</button>
            </template>
            <RouterLink v-else to="/auth/login" class="text-ink-secondary hover:text-ink">{{ t('nav.signIn') }}</RouterLink>
          </div>
        </div>
      </div>
    </header>

    <Drawer :open="menuOpen" :title="t('nav.menu')" :close-label="t('common.close')" @close="menuOpen = false">
      <nav class="flex flex-col gap-1 text-base" :aria-label="t('nav.main')" @click="menuOpen = false">
        <RouterLink to="/products" class="rounded-md px-3 py-2 text-ink hover:bg-surface-muted">{{ t('nav.products') }}</RouterLink>
        <RouterLink to="/cart" class="rounded-md px-3 py-2 text-ink hover:bg-surface-muted">{{ t('nav.cart') }}</RouterLink>
        <template v-if="isAuthenticated">
          <RouterLink to="/account/orders" class="rounded-md px-3 py-2 text-ink hover:bg-surface-muted">{{ t('account.myOrders') }}</RouterLink>
          <RouterLink to="/account/profile" class="rounded-md px-3 py-2 text-ink hover:bg-surface-muted">{{ t('account.myProfile') }}</RouterLink>
          <button type="button" class="rounded-md px-3 py-2 text-left text-ink hover:bg-surface-muted" @click="handleLogout">{{ t('nav.signOut') }}</button>
        </template>
        <template v-else>
          <RouterLink to="/auth/login" class="rounded-md px-3 py-2 text-ink hover:bg-surface-muted">{{ t('nav.signIn') }}</RouterLink>
          <RouterLink to="/auth/register" class="rounded-md px-3 py-2 text-ink hover:bg-surface-muted">{{ t('auth.register') }}</RouterLink>
        </template>
      </nav>
    </Drawer>

    <main id="main" tabindex="-1" class="flex-1 pb-20 outline-none">
      <RouterView />
    </main>

    <footer class="border-t border-line bg-surface-muted">
      <div class="mx-auto grid max-w-7xl gap-8 px-4 py-10 text-sm sm:grid-cols-3">
        <div>
          <p class="font-semibold text-ink">{{ t('footer.shop') }}</p>
          <ul class="mt-3 space-y-2 text-ink-secondary">
            <li><RouterLink to="/products" class="hover:text-ink">{{ t('nav.products') }}</RouterLink></li>
            <li><RouterLink to="/cart" class="hover:text-ink">{{ t('nav.cart') }}</RouterLink></li>
            <li><RouterLink :to="isAuthenticated ? '/account/orders' : '/auth/login'" class="hover:text-ink">{{ t('account.myOrders') }}</RouterLink></li>
          </ul>
        </div>
        <div>
          <p class="font-semibold text-ink">{{ t('footer.help') }}</p>
          <p class="mt-3 text-ink-secondary">{{ t('footer.helpText') }}</p>
        </div>
        <div>
          <p class="font-semibold text-ink">{{ t('footer.payments') }}</p>
          <ul class="mt-3 flex flex-wrap gap-2">
            <li v-for="m in PAYMENT_METHODS" :key="m"><Badge>{{ t(`checkout.methods.${m}`) }}</Badge></li>
          </ul>
        </div>
      </div>
      <p class="border-t border-line py-4 text-center text-xs text-ink-secondary">
        {{ t('common.footer', { year: new Date().getFullYear() }) }}
      </p>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { Menu, ShoppingCart, User } from 'lucide-vue-next'
import { computed, ref, watch } from 'vue'
import { RouterLink, RouterView, useRoute, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import Badge from '@/components/ui/Badge.vue'
import Drawer from '@/components/ui/Drawer.vue'
import Select from '@/components/ui/Select.vue'
import { useCartStore } from '@/stores/cart.store'
import { useAuthStore } from '@/stores/auth.store'
import { useLocaleStore, type Locale } from '@/stores/locale.store'

const PAYMENT_METHODS = ['COD', 'VNPay', 'MoMo', 'ZaloPay']

const { t } = useI18n()
const cartStore = useCartStore()
const authStore = useAuthStore()
const localeStore = useLocaleStore()
const router = useRouter()
const route = useRoute()

const menuOpen = ref(false)
const cartCount = computed(() => cartStore.itemCount)
const cartLabel = computed(() => (cartCount.value > 0 ? t('nav.cartWithCount', { count: cartCount.value }) : t('nav.cart')))
const isAuthenticated = computed(() => authStore.isAuthenticated)
const fullName = computed(() => authStore.fullName)

// Moving to another page closes the menu.
watch(
  () => route.fullPath,
  () => {
    menuOpen.value = false
  },
)

function handleLogout() {
  menuOpen.value = false
  authStore.logout()
  router.push('/')
}

function onLocaleChange(value: string) {
  localeStore.setLocale(value as Locale)
}
</script>
