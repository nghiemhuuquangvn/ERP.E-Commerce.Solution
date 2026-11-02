<template>
  <div class="flex min-h-screen items-center justify-center bg-surface-muted px-4">
    <div class="w-full max-w-sm rounded-xl border border-line bg-surface p-8 shadow-sm">
      <h1 class="mb-6 text-center text-2xl font-bold text-ink">Create Account</h1>

      <p v-if="error" class="mb-4 rounded bg-danger-soft p-3 text-sm text-danger-soft-text">{{ error }}</p>

      <form @submit.prevent="submit" class="space-y-4">
        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="mb-1 block text-sm font-medium text-ink-secondary">First Name</label>
            <input v-model="firstName" class="input" required />
          </div>
          <div>
            <label class="mb-1 block text-sm font-medium text-ink-secondary">Last Name</label>
            <input v-model="lastName" class="input" required />
          </div>
        </div>
        <div>
          <label class="mb-1 block text-sm font-medium text-ink-secondary">Email</label>
          <input v-model="email" type="email" class="input" required />
        </div>
        <div>
          <label class="mb-1 block text-sm font-medium text-ink-secondary">Password</label>
          <input v-model="password" type="password" class="input" required minlength="8" />
        </div>
        <button type="submit" :disabled="loading" class="w-full btn-brand disabled:opacity-40">
          {{ loading ? 'Creating account…' : 'Create Account' }}
        </button>
      </form>

      <p class="mt-4 text-center text-sm text-ink-muted">
        Already have an account?
        <RouterLink to="/auth/login" class="text-brand-action hover:underline">Sign in</RouterLink>
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, RouterLink } from 'vue-router'
import { useAuthStore } from '@/stores/auth.store'

const router = useRouter()
const authStore = useAuthStore()

const firstName = ref('')
const lastName = ref('')
const email = ref('')
const password = ref('')
const loading = ref(false)
const error = ref('')

async function submit() {
  loading.value = true
  error.value = ''
  try {
    await authStore.register(firstName.value, lastName.value, email.value, password.value)
    router.push('/')
  } catch {
    error.value = 'Registration failed. Please try again.'
  } finally {
    loading.value = false
  }
}
</script>
