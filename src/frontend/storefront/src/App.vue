<template>
  <RouterView />
  <ChatWidget />
</template>

<script setup lang="ts">
import { RouterView } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useLocaleStore } from '@/stores/locale.store'
import ChatWidget from '@/components/chat/ChatWidget.vue'

// Sync vue-i18n's active locale with the persisted store on app start
// (the store's `locale` ref is restored from localStorage before this runs,
// but i18n's own global locale needs to be set explicitly).
const { locale } = useI18n()
const localeStore = useLocaleStore()
locale.value = localeStore.locale
</script>
