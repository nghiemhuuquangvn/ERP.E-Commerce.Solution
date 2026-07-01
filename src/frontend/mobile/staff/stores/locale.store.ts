import { create } from 'zustand'
import { persist, createJSONStorage } from 'zustand/middleware'
import AsyncStorage from '@react-native-async-storage/async-storage'
import i18next from '@/i18n'

export type Locale = 'en' | 'vi'

interface LocaleState {
  locale: Locale
  setLocale: (locale: Locale) => void
}

export const useLocaleStore = create<LocaleState>()(
  persist(
    (set) => ({
      locale: 'en',
      setLocale: (locale) => {
        i18next.changeLanguage(locale)
        set({ locale })
      },
    }),
    {
      name: 'erp-staff-locale',
      storage: createJSONStorage(() => AsyncStorage),
      onRehydrateStorage: () => (state) => {
        if (state?.locale) i18next.changeLanguage(state.locale)
      },
    },
  ),
)
