<template>
  <div class="flex min-h-screen flex-col bg-white">
    <header class="sticky top-0 z-40 border-b border-gray-200 bg-white">
      <div class="mx-auto flex h-16 max-w-7xl items-center justify-between px-4">
        <RouterLink to="/" class="text-xl font-bold text-brand-500">ERP Shop</RouterLink>

        <nav class="hidden md:flex items-center gap-6 text-sm font-medium text-gray-600">
          <RouterLink to="/products" class="hover:text-gray-900">Products</RouterLink>
        </nav>

        <div class="flex items-center gap-4">
          <RouterLink to="/cart" class="relative text-gray-600 hover:text-gray-900">
            🛒
            <span
              v-if="cartCount > 0"
              class="absolute -right-2 -top-2 flex h-4 w-4 items-center justify-center rounded-full bg-brand-500 text-xs text-white"
            >{{ cartCount }}</span>
          </RouterLink>

          <RouterLink v-if="!isAuthenticated" to="/auth/login" class="text-sm text-gray-600 hover:text-gray-900">
            Sign in
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
      © {{ new Date().getFullYear() }} ERP Shop. All rights reserved.
    </footer>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { useCartStore } from '@/stores/cart.store'
import { useAuthStore } from '@/stores/auth.store'

const cartStore = useCartStore()
const authStore = useAuthStore()
const router = useRouter()

const cartCount = computed(() => cartStore.itemCount)
const isAuthenticated = computed(() => authStore.isAuthenticated)
const fullName = computed(() => authStore.fullName)

function handleLogout() {
  authStore.logout()
  router.push('/')
}
</script>
