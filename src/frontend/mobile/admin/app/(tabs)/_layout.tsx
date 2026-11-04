import { Tabs } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { MoreHorizontal } from 'lucide-react-native'
import { TenantChip } from '@/components/TenantChip'
import { MODULES } from '@/modules'
import { useVisibleModules } from '@/modules/visible'
import { colors, icon } from '@/theme/theme'

/**
 * The tab bar is built from the module registry (modules/index.ts): a module the user may open and that fits in the bar is a tab;
 * the rest are listed on More. Every other tab file is hidden with href: null, so a user never sees a tab they cannot use.
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
        headerRight: () => <TenantChip />,
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
      <Tabs.Screen
        name="more"
        options={{
          title: t('tabs.more'),
          tabBarAccessibilityLabel: t('tabs.more'),
          tabBarIcon: ({ color }: { color: string }) => <MoreHorizontal color={color} size={24} strokeWidth={icon.stroke} />,
        }}
      />
    </Tabs>
  )
}
