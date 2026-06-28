import { useState } from 'react'
import { View, Text, TextInput, FlatList, TouchableOpacity, Alert } from 'react-native'
import { useQuery } from '@tanstack/react-query'
import { apiClient } from '@/api/client'

interface InventoryItem {
  id: string
  productId: string
  productName: string
  sku: string
  quantityAvailable: number
  quantityOnHand: number
}

export default function InventoryScreen() {
  const [search, setSearch] = useState('')

  const { data, isLoading, refetch } = useQuery({
    queryKey: ['inventory', search],
    queryFn: async () => {
      const { data } = await apiClient.get('/v1/inventory', {
        params: { search: search || undefined, pageSize: 50 },
      })
      return data.items as InventoryItem[]
    },
  })

  function onAdjust(item: InventoryItem) {
    Alert.prompt(
      'Adjust Stock',
      `Current: ${item.quantityOnHand}. Enter new quantity:`,
      async (value) => {
        if (!value) return
        const qty = parseInt(value, 10)
        if (isNaN(qty)) return
        try {
          await apiClient.post('/v1/inventory/adjust', {
            productId: item.productId,
            quantity: qty - item.quantityOnHand,
            reason: 'Staff adjustment',
          })
          refetch()
        } catch {
          Alert.alert('Error', 'Failed to adjust stock.')
        }
      },
      'plain-text',
    )
  }

  return (
    <View className="flex-1 bg-gray-50">
      <View className="bg-white px-4 py-3 border-b border-gray-100">
        <TextInput
          className="border border-gray-300 rounded-lg px-3 py-2 text-sm"
          placeholder="Search products…"
          value={search}
          onChangeText={setSearch}
        />
      </View>

      {isLoading ? (
        <View className="flex-1 items-center justify-center">
          <Text className="text-gray-400">Loading…</Text>
        </View>
      ) : (
        <FlatList
          data={data ?? []}
          keyExtractor={(i) => i.id}
          contentContainerClassName="p-4 gap-2"
          renderItem={({ item }) => (
            <View className="bg-white rounded-xl p-4 border border-gray-100 flex-row items-center justify-between">
              <View className="flex-1">
                <Text className="font-medium text-gray-800">{item.productName}</Text>
                <Text className="text-xs text-gray-400">{item.sku}</Text>
              </View>
              <View className="items-end">
                <Text
                  className={`text-lg font-bold ${
                    item.quantityAvailable === 0
                      ? 'text-red-500'
                      : item.quantityAvailable < 5
                      ? 'text-yellow-500'
                      : 'text-green-600'
                  }`}
                >
                  {item.quantityAvailable}
                </Text>
                <TouchableOpacity onPress={() => onAdjust(item)}>
                  <Text className="text-xs text-primary-600">Adjust</Text>
                </TouchableOpacity>
              </View>
            </View>
          )}
        />
      )}
    </View>
  )
}
