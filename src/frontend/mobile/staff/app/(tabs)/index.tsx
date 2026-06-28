import { View, Text, ScrollView } from 'react-native'
import { useAuthStore } from '@/stores/auth.store'

function KpiCard({ label, value, color }: { label: string; value: string; color: string }) {
  return (
    <View className="bg-white rounded-xl p-4 flex-1 border border-gray-100 shadow-sm">
      <Text className="text-xs text-gray-500 mb-1">{label}</Text>
      <Text className={`text-2xl font-bold ${color}`}>{value}</Text>
    </View>
  )
}

export default function DashboardScreen() {
  const user = useAuthStore((s) => s.user)

  return (
    <ScrollView className="flex-1 bg-gray-50 p-4">
      <Text className="text-lg font-semibold text-gray-900 mb-4">
        Good morning, {user?.firstName} 👋
      </Text>

      <View className="flex-row gap-3 mb-4">
        <KpiCard label="Today's Sales" value="$0" color="text-primary-600" />
        <KpiCard label="Orders" value="0" color="text-gray-900" />
      </View>

      <View className="flex-row gap-3 mb-6">
        <KpiCard label="Low Stock" value="0" color="text-yellow-600" />
        <KpiCard label="Pending Shipments" value="0" color="text-purple-600" />
      </View>

      <View className="bg-white rounded-xl p-4 border border-gray-100">
        <Text className="font-semibold text-gray-700 mb-3">Quick Actions</Text>
        <Text className="text-sm text-gray-500">Use the tabs below to access POS, Inventory, and Deliveries.</Text>
      </View>
    </ScrollView>
  )
}
