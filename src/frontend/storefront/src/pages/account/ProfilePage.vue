<template>
  <div class="mx-auto max-w-lg px-4 py-10">
    <h1 class="mb-6 text-2xl font-bold text-ink">My Profile</h1>
    <div class="rounded-lg border border-line bg-surface p-6 space-y-4">
      <div>
        <label class="mb-1 block text-sm font-medium text-ink-secondary">Full Name</label>
        <p class="text-ink">{{ authStore.fullName }}</p>
      </div>
      <div>
        <label class="mb-1 block text-sm font-medium text-ink-secondary">Email</label>
        <p class="text-ink">{{ authStore.user?.email }}</p>
      </div>
      <button @click="authStore.logout(); $router.push('/')" class="btn-outline text-danger-soft-text border-danger/30 hover:bg-danger-soft">
        Sign Out
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useAuthStore } from '@/stores/auth.store'

const authStore = useAuthStore()
</script>
