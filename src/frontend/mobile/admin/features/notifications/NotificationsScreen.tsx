import { useState } from 'react'
import { Pressable, Text, View } from 'react-native'
import { useQueryClient, useMutation, type InfiniteData } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Card } from '@/components/ui'
import { NoAccess } from '@/components/NoAccess'
import { PagedListView } from '@/components/PagedListView'
import { usePagedList, type Page } from '@/modules/usePagedList'
import { formatDate } from '@/lib/dates'
import { useAuthStore } from '@/stores/auth.store'
import { NOTIFICATIONS_PERMISSION, notificationsApi, type AppNotification } from './api'
import { inboxKey, unreadKey, useCanSeeNotifications } from './useUnreadCount'

/** The signed-in user's notifications, newest first. Without notifications:view nothing is requested. */
export default function NotificationsScreen() {
  return useCanSeeNotifications() ? <Inbox /> : <NoAccess permission={NOTIFICATIONS_PERMISSION} />
}

/** Opening an unread notification marks it read (badge and row change at once). */
function Inbox() {
  const { t } = useTranslation()
  const userId = useAuthStore((s) => s.user?.id)
  const qc = useQueryClient()
  const [openId, setOpenId] = useState<string | null>(null)
  const query = usePagedList(inboxKey(userId), (page) => notificationsApi.history(userId as string, page))

  const markRead = useMutation({
    mutationFn: (id: string) => notificationsApi.markRead(id),
    onMutate: (id) => {
      qc.setQueryData<InfiniteData<Page<AppNotification>>>(inboxKey(userId), (old) =>
        old && { ...old, pages: old.pages.map((p) => ({ ...p, items: p.items.map((n) => (n.id === id ? { ...n, isRead: true } : n)) })) })
      qc.setQueryData<number>(unreadKey(userId), (n) => (n === undefined ? n : Math.max(0, n - 1)))
    },
    // A refused call: the server is the truth, so both numbers are read again.
    onError: () => void qc.invalidateQueries({ queryKey: ['notifications'] }),
  })

  function press(n: AppNotification) {
    setOpenId((cur) => (cur === n.id ? null : n.id))
    if (!n.isRead) markRead.mutate(n.id)
  }

  return (
    <PagedListView
      query={query}
      keyOf={(n) => n.id}
      emptyTitle={t('notifications.empty')}
      emptyHint={t('notifications.emptyHint')}
      permission={NOTIFICATIONS_PERMISSION}
      renderItem={(n) => {
        const open = openId === n.id
        return (
          <Pressable
            onPress={() => press(n)}
            accessibilityRole="button"
            accessibilityState={{ expanded: open }}
            accessibilityLabel={`${n.isRead ? t('notifications.read') : t('notifications.unread')}: ${n.subject}`}
          >
            <Card>
              <View className="flex-row items-start gap-3">
                <View className={`mt-1.5 h-2.5 w-2.5 rounded-full ${n.isRead ? 'bg-transparent' : 'bg-brand-action'}`} />
                <View className="flex-1 gap-1">
                  <Text className={`text-base text-ink ${n.isRead ? '' : 'font-semibold'}`}>{n.subject || t('notifications.noSubject')}</Text>
                  {n.body ? <Text numberOfLines={open ? undefined : 2} className="text-sm text-ink-secondary">{n.body}</Text> : null}
                  <Text className="text-xs text-ink-muted">{formatDate(n.createdAt)}</Text>
                </View>
              </View>
            </Card>
          </Pressable>
        )
      }}
    />
  )
}
