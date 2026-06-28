import { View, Text, FlatList, TouchableOpacity, Alert } from 'react-native'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiClient } from '@/api/client'

interface Shipment {
  id: string
  orderId: string
  status: string
  trackingNumber: string
  estimatedDelivery: string
}

export default function DeliveriesScreen() {
  const queryClient = useQueryClient()

  const { data, isLoading } = useQuery({
    queryKey: ['shipments'],
    queryFn: async () => {
      const { data } = await apiClient.get('/v1/shipments', { params: { pageSize: 50 } })
      return data.items as Shipment[]
    },
  })

  const addTracking = useMutation({
    mutationFn: async ({ id, status, location }: { id: string; status: string; location: string }) =>
      apiClient.post(`/v1/shipments/${id}/tracking`, { status, location, description: `Updated by staff` }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['shipments'] }),
    onError: () => Alert.alert('Error', 'Failed to update tracking.'),
  })

  const STATUS_COLOR: Record<string, string> = {
    Pending: 'text-gray-500',
    PickedUp: 'text-blue-500',
    InTransit: 'text-purple-500',
    Delivered: 'text-green-600',
    Failed: 'text-red-500',
  }

  return (
    <View className="flex-1 bg-gray-50">
      {isLoading ? (
        <View className="flex-1 items-center justify-center">
          <Text className="text-gray-400">Loading…</Text>
        </View>
      ) : (
        <FlatList
          data={data ?? []}
          keyExtractor={(s) => s.id}
          contentContainerClassName="p-4 gap-3"
          renderItem={({ item }) => (
            <View className="bg-white rounded-xl p-4 border border-gray-100">
              <View className="flex-row justify-between mb-2">
                <Text className="font-medium text-gray-800">#{item.trackingNumber}</Text>
                <Text className={`text-sm font-medium ${STATUS_COLOR[item.status] ?? 'text-gray-500'}`}>
                  {item.status}
                </Text>
              </View>
              <Text className="text-xs text-gray-400 mb-3">Order: {item.orderId.slice(0, 8)}…</Text>
              {item.status !== 'Delivered' && (
                <TouchableOpacity
                  onPress={() =>
                    addTracking.mutate({ id: item.id, status: 'InTransit', location: 'Warehouse' })
                  }
                  className="bg-primary-50 rounded-lg py-2 items-center"
                >
                  <Text className="text-primary-600 text-sm font-medium">Mark In Transit</Text>
                </TouchableOpacity>
              )}
            </View>
          )}
          ListEmptyComponent={
            <View className="py-16 items-center">
              <Text className="text-gray-400">No active shipments.</Text>
            </View>
          }
        />
      )}
    </View>
  )
}
