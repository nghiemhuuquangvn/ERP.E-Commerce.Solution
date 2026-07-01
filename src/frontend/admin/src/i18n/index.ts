import i18next from 'i18next'
import { initReactI18next } from 'react-i18next'
import { useUiStore } from '@/stores/ui.store'
import en from './locales/en.json'
import vi from './locales/vi.json'

i18next.use(initReactI18next).init({
  resources: { en: { translation: en }, vi: { translation: vi } },
  lng: useUiStore.getState().locale,
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
})

// Keep i18next in sync if the persisted locale changes outside of setLocale (e.g.
// rehydration from localStorage after this module has already initialized).
useUiStore.subscribe((state) => {
  if (i18next.language !== state.locale) {
    i18next.changeLanguage(state.locale)
  }
})

export default i18next
