import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export type Locale = 'en' | 'vi'

interface UiState {
  sidebarCollapsed: boolean
  theme: 'light' | 'dark'
  locale: Locale
  toggleSidebar: () => void
  setSidebarCollapsed: (collapsed: boolean) => void
  setTheme: (theme: 'light' | 'dark') => void
  setLocale: (locale: Locale) => void
}

export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      sidebarCollapsed: false,
      theme: 'light',
      locale: 'en',
      toggleSidebar: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),
      setSidebarCollapsed: (collapsed) => set({ sidebarCollapsed: collapsed }),
      setTheme: (theme) => set({ theme }),
      setLocale: (locale) => set({ locale }),
    }),
    { name: 'erp-ui' },
  ),
)
