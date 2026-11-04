import { useState } from 'react'
import { Pressable, Text, View } from 'react-native'
import type { BottomTabBarProps } from '@react-navigation/bottom-tabs'
import { useSafeAreaInsets } from 'react-native-safe-area-context'
import { useTranslation } from 'react-i18next'
import { MoreHorizontal } from 'lucide-react-native'
import { BottomSheet } from '@/components/ui'
import { barFor, useVisibleModules, type BarItem } from '@/modules/visible'
import { useAuthStore } from '@/stores/auth.store'
import { useMenuStore } from '@/stores/menu.store'
import { colors, icon } from '@/theme/theme'

interface ItemProps {
  label: string
  selected: boolean
  onPress: () => void
  children: React.ReactNode
}

function TabItem({ label, selected, onPress, children }: ItemProps) {
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="tab"
      accessibilityLabel={label}
      accessibilityState={{ selected }}
      className="min-h-[56px] flex-1 items-center justify-center gap-0.5 px-1"
    >
      {children}
      <Text numberOfLines={1} className={`text-xs ${selected ? 'font-semibold text-brand-action' : 'text-ink-muted'}`}>{label}</Text>
    </Pressable>
  )
}

/**
 * The bottom bar. It changes with where the user is: on Home it lists the modules; inside a module it lists Home (the way back) and
 * that module's business functions (sections). What does not fit goes behind "More" (the side menu on Home, a list of the remaining
 * sections inside a module). Built from the module registry, so a new module or section needs no change here.
 */
export function ModuleTabBar({ state, navigation }: BottomTabBarProps) {
  const { t } = useTranslation()
  const insets = useSafeAreaInsets()
  const { visible } = useVisibleModules()
  const permissions = useAuthStore((s) => s.permissions)
  const openMenu = useMenuStore((s) => s.setOpen)
  const [moreOpen, setMoreOpen] = useState(false)

  const focused = state.routes[state.index]?.name ?? 'index'
  const bar = barFor(focused, visible, permissions)
  const overflowSelected = bar.overflow.some((o) => o.route === focused)
  const isSelected = (item: BarItem) =>
    item.route === focused || (bar.mode === 'modules' && visible.find((m) => m.id === item.key)?.sections?.some((s) => s.route === focused) === true)

  return (
    <View style={{ paddingBottom: insets.bottom }} accessibilityRole="tablist" className="flex-row border-t border-line bg-surface">
      {bar.items.map((item) => {
        const Icon = item.icon
        const selected = isSelected(item)
        return (
          <TabItem key={item.key} label={t(item.titleKey)} selected={selected} onPress={() => navigation.navigate(item.route)}>
            <Icon color={selected ? colors.brandAction : colors.inkMuted} size={24} strokeWidth={icon.stroke} />
          </TabItem>
        )
      })}
      {bar.hasMore ? (
        <TabItem
          label={t('tabs.more')}
          selected={overflowSelected}
          onPress={() => (bar.mode === 'modules' ? openMenu(true) : setMoreOpen(true))}
        >
          <MoreHorizontal color={overflowSelected ? colors.brandAction : colors.inkMuted} size={24} strokeWidth={icon.stroke} />
        </TabItem>
      ) : null}

      <BottomSheet visible={moreOpen} title={bar.module ? t(bar.module.titleKey) : t('tabs.more')} onClose={() => setMoreOpen(false)} closeLabel={t('common.close')}>
        <View className="gap-1">
          {bar.overflow.map((item) => {
            const Icon = item.icon
            const selected = item.route === focused
            return (
              <Pressable
                key={item.key}
                onPress={() => { setMoreOpen(false); navigation.navigate(item.route) }}
                accessibilityRole="button"
                accessibilityLabel={t(item.titleKey)}
                accessibilityState={{ selected }}
                className={`min-h-[48px] flex-row items-center gap-3 rounded-control px-3 ${selected ? 'bg-brand-soft' : ''}`}
              >
                <Icon color={selected ? colors.brandSoftText : colors.inkSecondary} size={22} strokeWidth={icon.stroke} />
                <Text className={`flex-1 text-base ${selected ? 'font-semibold text-brand-soft-text' : 'text-ink'}`}>{t(item.titleKey)}</Text>
              </Pressable>
            )
          })}
        </View>
      </BottomSheet>
    </View>
  )
}
