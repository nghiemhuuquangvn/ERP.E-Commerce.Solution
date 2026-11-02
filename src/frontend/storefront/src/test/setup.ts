import '@testing-library/jest-dom/vitest'
import { config } from '@vue/test-utils'
import { expect } from 'vitest'
import * as axeMatchers from 'vitest-axe/matchers'
import { createI18n } from 'vue-i18n'
import en from '@/i18n/locales/en.json'
import vi from '@/i18n/locales/vi.json'

expect.extend(axeMatchers)

// Every mounted component sees the real English / Vietnamese messages, like the app.
export const testI18n = createI18n({ legacy: false, locale: 'en', fallbackLocale: 'en', messages: { en, vi } })
config.global.plugins = [testI18n]
