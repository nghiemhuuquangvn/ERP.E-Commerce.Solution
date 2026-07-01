import { View, Text, FlatList, TouchableOpacity } from 'react-native'
import { useRouter } from 'expo-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { apiClient } from '@/api/client'
import { format } from 'date-fns'

interface Order {
  id: string
  orderNumber: string
  status: string
  totalAmount: number
  orderDate: string
  lines: Array<{ productName: string; quantity: number }>
}

const STATUS_COLOR: Record<string, string> = {
  Draft: 'bg-gray-100 text-gray-600',
  Confirmed: 'bg-blue-100 text-blue-700',
  Processing: 'bg-yellow-100 text-yellow-700',
  Shipped: 'bg-purple-100 text-purple-700',
  Delivered: 'bg-green-100 text-green-700',
  Cancelled: 'bg-red-100 text-red-700',
}

export default function OrdersScreen() {
  const router = useRouter()
  const { t } = useTranslation()

  const { data, isLoading } = useQuery({
    queryKey: ['my-orders'],
    queryFn: async () => {
      const { data } = await apiClient.get('/v1/orders/my')
      return data as Order[]
    },
  })

  const fmt = (n: number) =>
    new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)

  return (
    <View className="flex-1 bg-white">
      {isLoading ? (
        <View className="flex-1 items-center justify-center">
          <Text className="text-gray-400">{t('orders.loading')}</Text>
        </View>
      ) : (
        <FlatList
          data={data ?? []}
          keyExtractor={(o) => o.id}
          contentContainerClassName="p-4 gap-3"
          renderItem={({ item }) => {
            const colorClass = STATUS_COLOR[item.status] ?? 'bg-gray-100 text-gray-600'
            return (
              <TouchableOpacity
                className="rounded-xl border border-gray-100 p-4 bg-gray-50"
                onPress={() => router.push(`/order/${item.id}`)}
              >
                <View className="flex-row justify-between items-start mb-2">
                  <Text className="font-semibold text-gray-800">{item.orderNumber}</Text>
                  <View className={`rounded-full px-2.5 py-0.5 ${colorClass}`}>
                    <Text className="text-xs font-medium">{item.status}</Text>
                  </View>
                </View>
                <Text className="text-xs text-gray-400 mb-1">
                  {format(new Date(item.orderDate), 'dd MMM yyyy')}
                </Text>
                <Text className="text-sm text-gray-600 mb-2">
                  {item.lines.map((l) => `${l.productName} ×${l.quantity}`).join(', ')}
                </Text>
                <Text className="font-bold text-brand-500">{fmt(item.totalAmount)}</Text>
              </TouchableOpacity>
            )
          }}
          ListEmptyComponent={
            <View className="py-16 items-center">
              <Text className="text-gray-400">{t('orders.empty')}</Text>
            </View>
          }
        />
      )}
    </View>
  )
}
