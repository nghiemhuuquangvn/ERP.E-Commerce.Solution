<template>
  <div class="flex min-h-screen items-center justify-center bg-page px-4">
    <div class="w-full max-w-sm rounded-xl border border-line bg-surface p-8 shadow-sm">
      <RouterLink to="/" class="mb-2 block text-center text-sm font-semibold text-brand-action">{{ t('nav.brand') }}</RouterLink>
      <h1 class="mb-6 text-center text-2xl font-bold text-ink">{{ t('auth.createAccount') }}</h1>

      <Alert v-if="error" variant="danger" class="mb-4">{{ error }}</Alert>

      <form class="space-y-4" @submit.prevent="submit">
        <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <FormField :label="t('auth.firstName')" required>
            <Input v-model="firstName" required autocomplete="given-name" />
          </FormField>
          <FormField :label="t('auth.lastName')" required>
            <Input v-model="lastName" required autocomplete="family-name" />
          </FormField>
        </div>
        <FormField :label="t('auth.email')" required>
          <Input v-model="email" type="email" required autocomplete="username" />
        </FormField>
        <FormField :label="t('auth.password')" required :hint="t('auth.passwordHint')">
          <Input v-model="password" type="password" required minlength="8" autocomplete="new-password" />
        </FormField>
        <Button type="submit" full-width :loading="loading">{{ loading ? t('auth.creatingAccount') : t('auth.createAccount') }}</Button>
      </form>

      <p class="mt-4 text-center text-sm text-ink-secondary">
        {{ t('auth.alreadyHaveAccount') }}
        <RouterLink to="/auth/login" class="font-medium text-brand-action hover:underline">{{ t('auth.signInLink') }}</RouterLink>
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, RouterLink } from 'vue-router'
import { useI18n } from 'vue-i18n'
import Alert from '@/components/ui/Alert.vue'
import Button from '@/components/ui/Button.vue'
import FormField from '@/components/ui/FormField.vue'
import Input from '@/components/ui/Input.vue'
import { useAuthStore } from '@/stores/auth.store'

const { t } = useI18n()
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
    error.value = t('auth.registrationFailed')
  } finally {
    loading.value = false
  }
}
</script>
