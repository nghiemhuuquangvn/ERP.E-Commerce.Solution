import { Tabs } from 'expo-router'
import { Boxes, LayoutDashboard, Store, Truck } from 'lucide-react-native'
import { colors, icon } from '@/theme/theme'

const tabIcon = (Icon: typeof Store) =>
  function TabIcon({ color }: { color: string }) {
    return <Icon color={color} size={24} strokeWidth={icon.stroke} />
  }

export default function TabsLayout() {
  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: colors.brandAction,
        tabBarInactiveTintColor: colors.inkMuted,
        tabBarStyle: { borderTopColor: colors.line },
        headerShown: true,
        headerStyle: { backgroundColor: colors.surface },
        headerTintColor: colors.ink,
      }}
    >
      <Tabs.Screen name="index" options={{ title: 'Dashboard', tabBarIcon: tabIcon(LayoutDashboard) }} />
      <Tabs.Screen name="pos" options={{ title: 'POS', tabBarIcon: tabIcon(Store) }} />
      <Tabs.Screen name="inventory" options={{ title: 'Inventory', tabBarIcon: tabIcon(Boxes) }} />
      <Tabs.Screen name="deliveries" options={{ title: 'Deliveries', tabBarIcon: tabIcon(Truck) }} />
    </Tabs>
  )
}
