<template>
  <div class="flex min-h-screen items-center justify-center bg-gray-50 px-4">
    <div class="w-full max-w-sm rounded-xl border border-gray-200 bg-white p-8 shadow-sm">
      <h1 class="mb-6 text-center text-2xl font-bold text-gray-900">Sign In</h1>

      <p v-if="error" class="mb-4 rounded bg-red-50 p-3 text-sm text-red-700">{{ error }}</p>

      <form @submit.prevent="submit" class="space-y-4">
        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700">Email</label>
          <input v-model="email" type="email" class="input" required placeholder="you@example.com" />
        </div>
        <div>
          <label class="mb-1 block text-sm font-medium text-gray-700">Password</label>
          <input v-model="password" type="password" class="input" required placeholder="••••••••" />
        </div>
        <button type="submit" :disabled="loading" class="w-full btn-brand disabled:opacity-40">
          {{ loading ? 'Signing in…' : 'Sign In' }}
        </button>
      </form>

      <p class="mt-4 text-center text-sm text-gray-500">
        Don't have an account?
        <RouterLink to="/auth/register" class="text-brand-500 hover:underline">Register</RouterLink>
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, useRoute, RouterLink } from 'vue-router'
import { useAuthStore } from '@/stores/auth.store'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const email = ref('')
const password = ref('')
const loading = ref(false)
const error = ref('')

async function submit() {
  loading.value = true
  error.value = ''
  try {
    await authStore.login(email.value, password.value)
    const redirect = (route.query.redirect as string) ?? '/'
    router.push(redirect)
  } catch {
    error.value = 'Invalid email or password.'
  } finally {
    loading.value = false
  }
}
</script>
