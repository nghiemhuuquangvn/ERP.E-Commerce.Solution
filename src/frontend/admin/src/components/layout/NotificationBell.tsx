import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Bell } from 'lucide-react'
import { useAuthStore } from '@/stores/auth.store'
import { useNotificationsStore } from '@/stores/notifications.store'
import { notificationSignalR } from '@/services/notificationSignalR'

export function NotificationBell() {
  const { t, i18n } = useTranslation()
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const items = useNotificationsStore((s) => s.items)
  const unreadCount = useNotificationsStore((s) => s.unreadCount)
  const loadInitial = useNotificationsStore((s) => s.loadInitial)
  const markRead = useNotificationsStore((s) => s.markRead)

  useEffect(() => {
    if (!isAuthenticated) return
    void loadInitial()
    void notificationSignalR.connect()
    return () => {
      void notificationSignalR.disconnect()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated])

  useEffect(() => {
    if (!open) return
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [open])

  if (!isAuthenticated) return null

  return (
    <div className="relative" ref={containerRef}>
      <button
        onClick={() => setOpen((v) => !v)}
        className="relative rounded-md p-2 text-ink-muted hover:bg-surface-muted"
        aria-label={t('notifications.title')}
      >
        <Bell size={20} strokeWidth={1.75} aria-hidden="true" />
        {unreadCount > 0 && (
          <span className="absolute -right-1 -top-1 flex h-5 min-w-5 items-center justify-center rounded-full bg-danger px-1 text-xs font-semibold text-on-brand">
            {unreadCount > 99 ? '99+' : unreadCount}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 z-50 mt-2 w-80 max-w-[90vw] rounded-md border border-line bg-surface shadow-lg">
          <div className="border-b border-line px-4 py-2 text-sm font-semibold text-ink">
            {t('notifications.title')}
          </div>
          <div className="max-h-96 overflow-y-auto">
            {items.length === 0 ? (
              <p className="px-4 py-6 text-center text-sm text-ink-muted">{t('notifications.emptyState')}</p>
            ) : (
              items.map((n) => (
                <button
                  key={n.id}
                  onClick={() => void markRead(n.id)}
                  className={`block w-full border-b border-line px-4 py-3 text-left text-sm hover:bg-surface-muted ${
                    n.isRead ? 'bg-surface' : 'bg-info-soft'
                  }`}
                >
                  <p className="font-medium text-ink">{n.subject}</p>
                  <p className="mt-0.5 text-ink-secondary">{n.body}</p>
                  <p className="mt-1 text-xs text-ink-muted">
                    {new Date(n.createdAt).toLocaleString(i18n.language)}
                  </p>
                </button>
              ))
            )}
          </div>
        </div>
      )}
    </div>
  )
}
