import { Tabs } from 'expo-router'

export default function TabsLayout() {
  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: '#0284c7',
        tabBarInactiveTintColor: '#9ca3af',
        tabBarStyle: { borderTopColor: '#e5e7eb' },
        headerShown: true,
        headerStyle: { backgroundColor: '#fff' },
        headerTintColor: '#111827',
      }}
    >
      <Tabs.Screen
        name="index"
        options={{ title: 'Dashboard', tabBarIcon: ({ color }) => <TabIcon icon="⊞" color={color} /> }}
      />
      <Tabs.Screen
        name="pos"
        options={{ title: 'POS', tabBarIcon: ({ color }) => <TabIcon icon="🛒" color={color} /> }}
      />
      <Tabs.Screen
        name="inventory"
        options={{ title: 'Inventory', tabBarIcon: ({ color }) => <TabIcon icon="📦" color={color} /> }}
      />
      <Tabs.Screen
        name="deliveries"
        options={{ title: 'Deliveries', tabBarIcon: ({ color }) => <TabIcon icon="🚚" color={color} /> }}
      />
    </Tabs>
  )
}

function TabIcon({ icon, color }: { icon: string; color: string }) {
  const { Text } = require('react-native')
  return <Text style={{ fontSize: 18, color }}>{icon}</Text>
}
