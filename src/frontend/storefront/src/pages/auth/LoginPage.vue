<template>
  <div class="flex min-h-screen items-center justify-center bg-page px-4">
    <div class="w-full max-w-sm rounded-xl border border-line bg-surface p-8 shadow-sm">
      <RouterLink to="/" class="mb-2 block text-center text-sm font-semibold text-brand-action">{{ t('nav.brand') }}</RouterLink>
      <h1 class="mb-6 text-center text-2xl font-bold text-ink">{{ t('auth.signIn') }}</h1>

      <Alert v-if="error" variant="danger" class="mb-4">{{ error }}</Alert>

      <form class="space-y-4" @submit.prevent="submit">
        <FormField :label="t('auth.email')" required>
          <Input v-model="email" type="email" required autocomplete="username" placeholder="you@example.com" />
        </FormField>
        <FormField :label="t('auth.password')" required>
          <Input v-model="password" type="password" required autocomplete="current-password" placeholder="••••••••" />
        </FormField>
        <Button type="submit" full-width :loading="loading">{{ loading ? t('auth.signingIn') : t('auth.signIn') }}</Button>
      </form>

      <p class="mt-4 text-center text-sm text-ink-secondary">
        {{ t('auth.noAccount') }}
        <RouterLink to="/auth/register" class="font-medium text-brand-action hover:underline">{{ t('auth.register') }}</RouterLink>
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, useRoute, RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import Alert from '@/components/ui/Alert.vue'
import Button from '@/components/ui/Button.vue'
import FormField from '@/components/ui/FormField.vue'
import Input from '@/components/ui/Input.vue'
import { useAuthStore } from '@/stores/auth.store'

const { t } = useI18n()
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
    error.value = t('auth.invalidCredentials')
  } finally {
    loading.value = false
  }
}
</script>
