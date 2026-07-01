<template>
  <div class="flex min-h-screen flex-col bg-white">
    <header class="sticky top-0 z-40 border-b border-gray-200 bg-white">
      <div class="mx-auto flex h-16 max-w-7xl items-center justify-between px-4">
        <RouterLink to="/" class="text-xl font-bold text-brand-500">{{ t('nav.brand') }}</RouterLink>

        <nav class="hidden md:flex items-center gap-6 text-sm font-medium text-gray-600">
          <RouterLink to="/products" class="hover:text-gray-900">{{ t('nav.products') }}</RouterLink>
        </nav>

        <div class="flex items-center gap-4">
          <select
            :value="localeStore.locale"
            @change="onLocaleChange"
            class="rounded-md border border-gray-200 bg-white px-2 py-1 text-xs font-medium text-gray-600 hover:border-gray-300"
            :aria-label="t('common.language')"
          >
            <option value="en">EN</option>
            <option value="vi">VI</option>
          </select>

          <RouterLink to="/cart" class="relative text-gray-600 hover:text-gray-900">
            🛒
            <span
              v-if="cartCount > 0"
              class="absolute -right-2 -top-2 flex h-4 w-4 items-center justify-center rounded-full bg-brand-500 text-xs text-white"
            >{{ cartCount }}</span>
          </RouterLink>

          <RouterLink v-if="!isAuthenticated" to="/auth/login" class="text-sm text-gray-600 hover:text-gray-900">
            {{ t('nav.signIn') }}
          </RouterLink>
          <button v-else @click="handleLogout" class="text-sm text-gray-600 hover:text-gray-900">
            {{ fullName }}
          </button>
        </div>
      </div>
    </header>

    <main class="flex-1">
      <RouterView />
    </main>

    <footer class="border-t border-gray-200 bg-gray-50 py-8 text-center text-sm text-gray-500">
      {{ t('common.footer', { year: new Date().getFullYear() }) }}
    </footer>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useCartStore } from '@/stores/cart.store'
import { useAuthStore } from '@/stores/auth.store'
import { useLocaleStore, type Locale } from '@/stores/locale.store'

const { t } = useI18n()
const cartStore = useCartStore()
const authStore = useAuthStore()
const localeStore = useLocaleStore()
const router = useRouter()

const cartCount = computed(() => cartStore.itemCount)
const isAuthenticated = computed(() => authStore.isAuthenticated)
const fullName = computed(() => authStore.fullName)

function handleLogout() {
  authStore.logout()
  router.push('/')
}

function onLocaleChange(event: Event) {
  const value = (event.target as HTMLSelectElement).value as Locale
  localeStore.setLocale(value)
}
</script>
