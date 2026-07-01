import { useTranslation } from 'react-i18next'
import { useAuthStore } from '@/stores/auth.store'
import { useUiStore, type Locale } from '@/stores/ui.store'
import { useLogout } from '@/hooks/useAuth'

export function Header() {
  const { t, i18n } = useTranslation()
  const user = useAuthStore((s) => s.user)
  const { toggleSidebar, locale, setLocale } = useUiStore()
  const logout = useLogout()

  const handleLocaleChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const next = e.target.value as Locale
    setLocale(next)
    i18n.changeLanguage(next)
  }

  return (
    <header className="flex h-16 items-center justify-between border-b border-gray-200 bg-white px-6">
      <button
        onClick={toggleSidebar}
        className="rounded-md p-2 text-gray-500 hover:bg-gray-100"
        aria-label="Toggle sidebar"
      >
        ☰
      </button>

      <div className="flex items-center gap-4">
        <select
          value={locale}
          onChange={handleLocaleChange}
          className="rounded-md border border-gray-300 bg-white px-2 py-1 text-sm text-gray-600"
          aria-label="Language"
        >
          <option value="en">EN</option>
          <option value="vi">VI</option>
        </select>
        <span className="text-sm text-gray-600">{user?.fullName ?? user?.email}</span>
        <button
          onClick={logout}
          className="rounded-md px-3 py-1.5 text-sm text-gray-600 hover:bg-gray-100"
        >
          {t('common.signOut')}
        </button>
      </div>
    </header>
  )
}
