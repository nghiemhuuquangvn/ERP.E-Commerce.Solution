import { Tabs } from 'expo-router'
import { Home, Package, Search, ShoppingCart, User } from 'lucide-react-native'
import { useCartStore } from '@/stores/cart.store'
import { colors, icon } from '@/theme/theme'

const tabIcon = (Icon: typeof Home) =>
  function TabIcon({ color }: { color: string }) {
    return <Icon color={color} size={24} strokeWidth={icon.stroke} />
  }

export default function TabsLayout() {
  const cartCount = useCartStore((s) => s.itemCount())

  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: colors.brandAction,
        tabBarInactiveTintColor: colors.inkMuted,
        headerShown: true,
        headerStyle: { backgroundColor: colors.surface },
        headerTintColor: colors.ink,
      }}
    >
      <Tabs.Screen name="index" options={{ title: 'Home', tabBarIcon: tabIcon(Home) }} />
      <Tabs.Screen name="search" options={{ title: 'Search', tabBarIcon: tabIcon(Search) }} />
      <Tabs.Screen
        name="cart"
        options={{
          title: 'Cart',
          tabBarIcon: tabIcon(ShoppingCart),
          tabBarBadge: cartCount > 0 ? cartCount : undefined,
        }}
      />
      <Tabs.Screen name="orders" options={{ title: 'Orders', tabBarIcon: tabIcon(Package) }} />
      <Tabs.Screen name="profile" options={{ title: 'Profile', tabBarIcon: tabIcon(User) }} />
    </Tabs>
  )
}
