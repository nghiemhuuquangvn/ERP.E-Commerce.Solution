import '@testing-library/jest-dom/vitest'
import { afterEach, expect } from 'vitest'
import { cleanup } from '@testing-library/react'
import * as axeMatchers from 'vitest-axe/matchers'
import i18next from 'i18next'
import { initReactI18next } from 'react-i18next'
import en from '@/i18n/locales/en.json'

expect.extend(axeMatchers)

// Components that call useTranslation render with the English resources, without the app store.
i18next.use(initReactI18next).init({
  lng: 'en',
  fallbackLng: 'en',
  resources: { en: { translation: en } },
  interpolation: { escapeValue: false },
})

afterEach(() => cleanup())
