import { Tabs } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { Boxes, LayoutDashboard, Store, Truck } from 'lucide-react-native'
import { colors, icon } from '@/theme/theme'

const tabIcon = (Icon: typeof Store) =>
  function TabIcon({ color }: { color: string }) {
    return <Icon color={color} size={24} strokeWidth={icon.stroke} />
  }

export default function TabsLayout() {
  const { t } = useTranslation()
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
      }}
    >
      <Tabs.Screen name="index" options={{ title: t('tabs.dashboard'), tabBarAccessibilityLabel: t('tabs.dashboard'), tabBarIcon: tabIcon(LayoutDashboard) }} />
      <Tabs.Screen name="pos" options={{ title: t('tabs.pos'), tabBarAccessibilityLabel: t('tabs.pos'), tabBarIcon: tabIcon(Store) }} />
      <Tabs.Screen name="inventory" options={{ title: t('tabs.inventory'), tabBarAccessibilityLabel: t('tabs.inventory'), tabBarIcon: tabIcon(Boxes) }} />
      <Tabs.Screen name="deliveries" options={{ title: t('tabs.deliveries'), tabBarAccessibilityLabel: t('tabs.deliveries'), tabBarIcon: tabIcon(Truck) }} />
    </Tabs>
  )
}
