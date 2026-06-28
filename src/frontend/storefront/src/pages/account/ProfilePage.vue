<template>
  <div class="mx-auto max-w-lg px-4 py-10">
    <h1 class="mb-6 text-2xl font-bold text-gray-900">My Profile</h1>
    <div class="rounded-lg border border-gray-200 bg-white p-6 space-y-4">
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Full Name</label>
        <p class="text-gray-900">{{ authStore.fullName }}</p>
      </div>
      <div>
        <label class="mb-1 block text-sm font-medium text-gray-700">Email</label>
        <p class="text-gray-900">{{ authStore.user?.email }}</p>
      </div>
      <button @click="authStore.logout(); $router.push('/')" class="btn-outline text-red-600 border-red-200 hover:bg-red-50">
        Sign Out
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useAuthStore } from '@/stores/auth.store'

const authStore = useAuthStore()
</script>
