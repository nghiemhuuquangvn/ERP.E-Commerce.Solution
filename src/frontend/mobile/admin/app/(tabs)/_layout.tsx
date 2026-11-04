import { View } from 'react-native'
import { Tabs } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { MenuButton } from '@/components/MenuButton'
import { ModuleTabBar } from '@/components/ModuleTabBar'
import { NotificationBell } from '@/components/NotificationBell'
import { TenantChip } from '@/components/TenantChip'
import { MODULES } from '@/modules'
import { colors } from '@/theme/theme'

/** Every screen file the registry names: the module routes and the routes of their sections (one navigator, one file each). */
const SCREENS = MODULES.flatMap((m) => [...(m.sections ?? []).map((s) => ({ route: s.route, titleKey: s.titleKey })), { route: m.route, titleKey: m.titleKey }])
  .filter((s, i, all) => all.findIndex((x) => x.route === s.route) === i)

/**
 * One tab navigator holds every screen of every module; the visible bar is the custom ModuleTabBar, built from the module registry
 * (modules/index.ts): Home lists the modules, inside a module it lists that module's business functions. A screen the user may not
 * open is never in the bar. The header carries the menu button, the notification bell and the tenant chip.
 */
export default function TabsLayout() {
  const { t } = useTranslation()

  return (
    <Tabs
      tabBar={(props) => <ModuleTabBar {...props} />}
      screenOptions={{
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
      {SCREENS.map((s) => (
        <Tabs.Screen key={s.route} name={s.route} options={{ title: t(s.titleKey) }} />
      ))}
    </Tabs>
  )
}
