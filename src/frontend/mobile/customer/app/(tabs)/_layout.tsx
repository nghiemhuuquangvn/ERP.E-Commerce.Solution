import { Tabs } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { Home, Package, Search, ShoppingCart, User } from 'lucide-react-native'
import { useCartStore } from '@/stores/cart.store'
import { colors, icon } from '@/theme/theme'

const tabIcon = (Icon: typeof Home) =>
  function TabIcon({ color }: { color: string }) {
    return <Icon color={color} size={24} strokeWidth={icon.stroke} />
  }

export default function TabsLayout() {
  const { t } = useTranslation()
  const cartCount = useCartStore((s) => s.itemCount())

  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: colors.brandAction,
        tabBarInactiveTintColor: colors.inkMuted,
        tabBarLabelStyle: { fontSize: 12 },
        headerShown: true,
        headerStyle: { backgroundColor: colors.surface },
        headerTintColor: colors.ink,
      }}
    >
      <Tabs.Screen name="index" options={{ title: t('tabs.home'), tabBarAccessibilityLabel: t('tabs.home'), tabBarIcon: tabIcon(Home) }} />
      <Tabs.Screen name="search" options={{ title: t('tabs.search'), tabBarAccessibilityLabel: t('tabs.search'), tabBarIcon: tabIcon(Search) }} />
      <Tabs.Screen
        name="cart"
        options={{
          title: t('tabs.cart'),
          tabBarAccessibilityLabel: cartCount > 0 ? `${t('tabs.cart')} (${cartCount})` : t('tabs.cart'),
          tabBarIcon: tabIcon(ShoppingCart),
          tabBarBadge: cartCount > 0 ? cartCount : undefined,
        }}
      />
      <Tabs.Screen name="orders" options={{ title: t('tabs.orders'), tabBarAccessibilityLabel: t('tabs.orders'), tabBarIcon: tabIcon(Package) }} />
      <Tabs.Screen name="profile" options={{ title: t('tabs.profile'), tabBarAccessibilityLabel: t('tabs.profile'), tabBarIcon: tabIcon(User) }} />
    </Tabs>
  )
}
