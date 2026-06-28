<template>
  <div class="mx-auto max-w-5xl px-4 py-10">
    <div v-if="isLoading" class="text-gray-400">Loading...</div>
    <div v-else-if="product" class="grid grid-cols-1 gap-10 md:grid-cols-2">
      <!-- Image -->
      <div class="aspect-square overflow-hidden rounded-xl bg-gray-100">
        <img v-if="product.imageUrl" :src="product.imageUrl" :alt="product.name" class="h-full w-full object-cover" />
        <div v-else class="flex h-full items-center justify-center text-6xl text-gray-200">📦</div>
      </div>

      <!-- Info -->
      <div class="flex flex-col">
        <p class="text-sm text-gray-400">{{ product.categoryName }}</p>
        <h1 class="mt-1 text-2xl font-bold text-gray-900">{{ product.name }}</h1>
        <p class="mt-3 text-3xl font-semibold text-gray-900">{{ fmt(product.price) }}</p>
        <p class="mt-4 text-sm text-gray-600">{{ product.description }}</p>

        <div class="mt-6 flex items-center gap-3">
          <div class="flex items-center border border-gray-300 rounded-md">
            <button @click="qty = Math.max(1, qty - 1)" class="px-3 py-2 text-gray-600 hover:bg-gray-50">−</button>
            <span class="px-4 text-sm font-medium">{{ qty }}</span>
            <button @click="qty++" class="px-3 py-2 text-gray-600 hover:bg-gray-50">+</button>
          </div>
          <button
            @click="addToCart"
            :disabled="product.stockQuantity === 0"
            class="flex-1 btn-brand disabled:opacity-40"
          >
            {{ product.stockQuantity === 0 ? 'Out of Stock' : 'Add to Cart' }}
          </button>
        </div>

        <p v-if="added" class="mt-3 text-sm text-green-600">✓ Added to cart!</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRoute } from 'vue-router'
import { useQuery } from '@tanstack/vue-query'
import { productsApi } from '@/api/products.api'
import { useCartStore } from '@/stores/cart.store'

const route = useRoute()
const cartStore = useCartStore()
const qty = ref(1)
const added = ref(false)

const { data: product, isLoading } = useQuery({
  queryKey: ['products', route.params.id],
  queryFn: () => productsApi.get(route.params.id as string),
})

const fmt = (n: number) =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)

function addToCart() {
  if (!product.value) return
  cartStore.addItem(
    {
      productId: product.value.id,
      productName: product.value.name,
      unitPrice: product.value.price,
      imageUrl: product.value.imageUrl,
    },
    qty.value,
  )
  added.value = true
  setTimeout(() => (added.value = false), 2000)
}
</script>
