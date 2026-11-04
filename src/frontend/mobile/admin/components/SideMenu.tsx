import { useEffect, useRef, useState } from 'react'
import { Animated, Modal, Pressable, ScrollView, Text, View, useWindowDimensions } from 'react-native'
import { usePathname, useRouter } from 'expo-router'
import { useTranslation } from 'react-i18next'
import Constants from 'expo-constants'
import { Bell, X } from 'lucide-react-native'
import { Alert, BottomSheet, Button } from '@/components/ui'
import { DetailRows } from '@/components/DetailRows'
import { SegmentedControl } from '@/components/SegmentedControl'
import { TenantChip } from '@/components/TenantChip'
import { badgeText } from '@/components/NotificationBell'
import { useCanSeeNotifications, useUnreadCount } from '@/features/notifications/useUnreadCount'
import { useVisibleModules } from '@/modules/visible'
import { tenantConfig } from '@/lib/tenant'
import { useAuthStore } from '@/stores/auth.store'
import { useLocaleStore, type Locale } from '@/stores/locale.store'
import { useMenuStore } from '@/stores/menu.store'
import { colors, icon } from '@/theme/theme'

const SLIDE_MS = 200

/** First path segment of the current screen, as the module routes name it ('/' is the Home tab, file "index"). */
export const activeRoute = (pathname: string) => pathname.split('/').filter(Boolean)[0] ?? 'index'

function Row({ label, selected, onPress, children, trailing }: { label: string; selected?: boolean; onPress: () => void; children: React.ReactNode; trailing?: React.ReactNode }) {
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ selected: !!selected }}
      className={`min-h-[48px] flex-row items-center gap-3 rounded-control px-3 ${selected ? 'bg-brand-soft' : ''}`}
    >
      {children}
      <Text className={`flex-1 text-base ${selected ? 'font-semibold text-brand-soft-text' : 'text-ink'}`}>{label}</Text>
      {trailing}
    </Pressable>
  )
}

function MenuPanel({ onClose, onSignOut }: { onClose: () => void; onSignOut: () => void }) {
  const { t } = useTranslation()
  const router = useRouter()
  const pathname = usePathname()
  const { width } = useWindowDimensions()
  const panelWidth = Math.min(width * 0.84, 340)
  const slide = useRef(new Animated.Value(-panelWidth)).current
  const user = useAuthStore((s) => s.user)
  const { visible } = useVisibleModules()
  const canNotify = useCanSeeNotifications()
  const unread = useUnreadCount()
  const { locale, setLocale } = useLocaleStore()
  const current = activeRoute(pathname)

  useEffect(() => {
    Animated.timing(slide, { toValue: 0, duration: SLIDE_MS, useNativeDriver: true }).start()
  }, [slide])

  function go(path: string) {
    onClose()
    router.navigate(path as never)
  }

  return (
    <View className="flex-1 flex-row">
      <Animated.View style={{ width: panelWidth, transform: [{ translateX: slide }] }} className="bg-surface">
        <ScrollView contentContainerClassName="gap-4 p-4 pb-8" accessibilityViewIsModal>
          <View className="flex-row items-start gap-2">
            <View className="flex-1 gap-1">
              <Text accessibilityRole="header" className="text-lg font-semibold text-ink">{user?.name}</Text>
              <Text className="text-sm text-ink-secondary">{user?.email}</Text>
              <View className="mt-1 flex-row"><TenantChip /></View>
            </View>
            <Pressable onPress={onClose} accessibilityRole="button" accessibilityLabel={t('menu.close')} className="h-11 w-11 items-center justify-center">
              <X color={colors.inkSecondary} size={22} strokeWidth={icon.stroke} />
            </Pressable>
          </View>

          <View accessibilityRole="menu" className="gap-1">
            {visible.map((m) => (
              <Row key={m.id} label={t(m.titleKey)} selected={m.route === current} onPress={() => go(m.route === 'index' ? '/(tabs)' : `/(tabs)/${m.route}`)}>
                <m.icon color={m.route === current ? colors.brandSoftText : colors.inkSecondary} size={22} strokeWidth={icon.stroke} />
              </Row>
            ))}
            {canNotify ? (
              <Row
                label={t('menu.notifications')}
                selected={current === 'notifications'}
                onPress={() => go('/notifications')}
                trailing={unread ? <Text accessibilityLabel={t('menu.unreadCount', { count: unread })} className="text-sm font-semibold text-danger">{badgeText(unread)}</Text> : null}
              >
                <Bell color={current === 'notifications' ? colors.brandSoftText : colors.inkSecondary} size={22} strokeWidth={icon.stroke} />
              </Row>
            ) : null}
          </View>

          <View className="gap-2">
            <Text accessibilityRole="header" className="font-semibold text-ink">{t('common.language')}</Text>
            <SegmentedControl<Locale>
              segments={[{ value: 'en', label: t('common.english') }, { value: 'vi', label: t('common.vietnamese') }]}
              value={locale}
              onChange={setLocale}
            />
          </View>

          <DetailRows
            rows={[
              { label: t('more.tenant'), value: tenantConfig.code },
              { label: t('more.server'), value: tenantConfig.apiUrl },
              { label: t('more.version'), value: Constants.expoConfig?.version ?? '' },
            ]}
          />

          <Button variant="outline" onPress={onSignOut}>{t("more.signOut")}</Button>
        </ScrollView>
      </Animated.View>
      <Pressable onPress={onClose} accessibilityRole="button" accessibilityLabel={t('menu.close')} className="flex-1 bg-ink/50" />
    </View>
  )
}

/** The left menu: every module the user may open, the inbox, language, sign out. Mounted once, opened by the header button. */
export function SideMenu() {
  const { t } = useTranslation()
  const open = useMenuStore((s) => s.open)
  const setOpen = useMenuStore((s) => s.setOpen)
  const logout = useAuthStore((s) => s.logout)
  const [confirm, setConfirm] = useState(false)

  return (
    <>
      <Modal visible={open} transparent animationType="none" onRequestClose={() => setOpen(false)} accessibilityViewIsModal>
        <MenuPanel onClose={() => setOpen(false)} onSignOut={() => { setOpen(false); setConfirm(true) }} />
      </Modal>
      <BottomSheet visible={confirm} title={t('more.signOutTitle')} onClose={() => setConfirm(false)} closeLabel={t('common.close')}>
        <View className="gap-3">
          <Alert tone="info">{t('more.signOutHint')}</Alert>
          <View className="flex-row gap-2">
            <View className="flex-1"><Button variant="outline" onPress={() => setConfirm(false)}>{t('common.back')}</Button></View>
            <View className="flex-1"><Button variant="danger" onPress={() => { setConfirm(false); void logout() }}>{t('more.signOut')}</Button></View>
          </View>
        </View>
      </BottomSheet>
    </>
  )
}
