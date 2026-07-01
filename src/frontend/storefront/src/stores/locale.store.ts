import { defineStore } from 'pinia'
import { ref } from 'vue'
import { i18n } from '@/i18n'

export type Locale = 'en' | 'vi'

export const useLocaleStore = defineStore(
  'locale',
  () => {
    const locale = ref<Locale>('en')

    function setLocale(newLocale: Locale) {
      locale.value = newLocale
      i18n.global.locale.value = newLocale
    }

    return { locale, setLocale }
  },
  { persist: true },
)
