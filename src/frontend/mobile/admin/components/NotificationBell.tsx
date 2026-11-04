import { Pressable, Text, View } from 'react-native'
import { useRouter } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { Bell } from 'lucide-react-native'
import { useCanSeeNotifications, useUnreadCount } from '@/features/notifications/useUnreadCount'
import { colors, icon } from '@/theme/theme'

/** The unread number as shown on a badge: nothing at 0, "99+" beyond 99. */
export const badgeText = (n?: number) => (!n ? '' : n > 99 ? '99+' : String(n))

/** Header bell: opens the inbox; the badge shows how many notifications are unread (also spoken in the label). Hidden without notifications:view. */
export function NotificationBell() {
  const { t } = useTranslation()
  const router = useRouter()
  const allowed = useCanSeeNotifications()
  const unread = useUnreadCount()
  if (!allowed) return null
  return (
    <Pressable
      onPress={() => router.push('/notifications' as never)}
      accessibilityRole="button"
      accessibilityLabel={unread ? t('header.notificationsUnread', { count: unread }) : t('header.notifications')}
      className="h-11 w-11 items-center justify-center"
    >
      <Bell color={colors.ink} size={24} strokeWidth={icon.stroke} />
      {unread ? (
        <View className="absolute right-0.5 top-0.5 min-w-[18px] items-center rounded-full bg-danger px-1">
          <Text className="text-[11px] font-bold text-on-brand">{badgeText(unread)}</Text>
        </View>
      ) : null}
    </Pressable>
  )
}
