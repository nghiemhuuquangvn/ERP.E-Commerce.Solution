import { View } from 'react-native'
import { Tabs } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { MenuButton } from '@/components/MenuButton'
import { NotificationBell } from '@/components/NotificationBell'
import { TenantChip } from '@/components/TenantChip'
import { MODULES } from '@/modules'
import { useVisibleModules } from '@/modules/visible'
import { colors, icon } from '@/theme/theme'

/**
 * The tab bar is built from the module registry (modules/index.ts): a module the user may open and that fits in the bar is a tab;
 * the others are reached from the side menu (components/SideMenu). Every other tab file is hidden with href: null, so a user
 * never sees a tab they cannot use. The header carries the menu button, the notification bell and the tenant chip.
 */
export default function TabsLayout() {
  const { t } = useTranslation()
  const { tabs } = useVisibleModules()
  const inBar = new Set(tabs.map((m) => m.route))

  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: colors.brandAction,
        tabBarInactiveTintColor: colors.inkMuted,
        tabBarStyle: { borderTopColor: colors.line },
        tabBarLabelStyle: { fontSize: 12 },
        headerShown: true,
        headerStyle: { backgroundColor: colors.surface },
        headerTintColor: colors.ink,
        headerLeft: () => <MenuButton />,
        headerLeftContainerStyle: { paddingLeft: 8 },
        headerRight: () => (
          <View className="flex-row items-center gap-1">
            <NotificationBell />
            <TenantChip />
          </View>
        ),
        headerRightContainerStyle: { paddingRight: 16 },
      }}
    >
      {MODULES.map((m) => {
        const Icon = m.icon
        return (
          <Tabs.Screen
            key={m.id}
            name={m.route}
            options={{
              title: t(m.titleKey),
              tabBarAccessibilityLabel: t(m.titleKey),
              href: inBar.has(m.route) ? undefined : null,
              tabBarIcon: ({ color }: { color: string }) => <Icon color={color} size={24} strokeWidth={icon.stroke} />,
            }}
          />
        )
      })}
    </Tabs>
  )
}
