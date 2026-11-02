<template>
  <div class="max-w-lg">
    <h1 class="mb-6 text-2xl font-bold text-ink">{{ t('account.myProfile') }}</h1>
    <dl class="space-y-4 rounded-lg border border-line bg-surface p-6">
      <div>
        <dt class="mb-1 text-sm font-medium text-ink-secondary">{{ t('account.fullName') }}</dt>
        <dd class="text-ink">{{ authStore.fullName }}</dd>
      </div>
      <div>
        <dt class="mb-1 text-sm font-medium text-ink-secondary">{{ t('account.email') }}</dt>
        <dd class="text-ink">{{ authStore.user?.email }}</dd>
      </div>
    </dl>
    <Button variant="outline" class="mt-4" @click="signOut">{{ t('account.signOut') }}</Button>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import Button from '@/components/ui/Button.vue'
import { useAuthStore } from '@/stores/auth.store'

const { t } = useI18n()
const router = useRouter()
const authStore = useAuthStore()

function signOut() {
  authStore.logout()
  router.push('/')
}
</script>
