import { useTranslation } from 'react-i18next'
import { Menu } from 'lucide-react'
import { useAuthStore } from '@/stores/auth.store'
import { useUiStore, type Locale } from '@/stores/ui.store'
import { useLogout } from '@/hooks/useAuth'
import { NotificationBell } from './NotificationBell'

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
    <header className="flex h-16 items-center justify-between border-b border-line bg-surface px-6">
      <button
        onClick={toggleSidebar}
        className="rounded-md p-2 text-ink-muted hover:bg-surface-muted"
        aria-label="Toggle sidebar"
      >
        <Menu size={20} strokeWidth={1.75} aria-hidden="true" />
      </button>

      <div className="flex items-center gap-4">
        <NotificationBell />
        <select
          value={locale}
          onChange={handleLocaleChange}
          className="rounded-md border border-line-strong bg-surface px-2 py-1 text-sm text-ink-secondary"
          aria-label="Language"
        >
          <option value="en">EN</option>
          <option value="vi">VI</option>
        </select>
        <span className="text-sm text-ink-secondary">{user?.fullName ?? user?.email}</span>
        <button
          onClick={logout}
          className="rounded-md px-3 py-1.5 text-sm text-ink-secondary hover:bg-surface-muted"
        >
          {t('common.signOut')}
        </button>
      </div>
    </header>
  )
}
