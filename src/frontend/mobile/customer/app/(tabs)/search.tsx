import { useState } from 'react'
import { View, Text, TextInput, FlatList, TouchableOpacity, Image, ActivityIndicator } from 'react-native'
import { useRouter } from 'expo-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Package } from 'lucide-react-native'
import { colors, icon } from '@/theme/theme'
import { apiClient } from '@/api/client'

interface Product {
  id: string
  name: string
  price: number
  imageUrl?: string
  categoryName: string
  stockQuantity: number
}

export default function SearchScreen() {
  const router = useRouter()
  const { t } = useTranslation()
  const [search, setSearch] = useState('')

  const { data, isLoading } = useQuery({
    queryKey: ['products', 'search', search],
    queryFn: async () => {
      // The search route answers with an array, the list route with { total, skip, take, products }.
      if (search.trim()) {
        const { data } = await apiClient.get('/v1/products/search', { params: { searchTerm: search.trim() } })
        return data as Product[]
      }
      const { data } = await apiClient.get('/v1/products', { params: { take: 30 } })
      return (data.products ?? []) as Product[]
    },
    enabled: true,
  })

  const fmt = (n: number) =>
    new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)

  return (
    <View className="flex-1 bg-white">
      <View className="px-4 py-3 border-b border-gray-100">
        <TextInput
          className="bg-gray-100 rounded-xl px-4 py-2.5 text-sm"
          placeholder={t('search.placeholder')}
          value={search}
          onChangeText={setSearch}
          returnKeyType="search"
        />
      </View>

      {isLoading ? (
        <ActivityIndicator className="mt-10" color={colors.brandAction} />
      ) : (
        <FlatList
          data={data ?? []}
          keyExtractor={(p) => p.id}
          numColumns={2}
          contentContainerClassName="p-3"
          columnWrapperClassName="gap-3"
          ItemSeparatorComponent={() => <View className="h-3" />}
          renderItem={({ item }) => (
            <TouchableOpacity
              className="flex-1"
              onPress={() => router.push(`/product/${item.id}`)}
            >
              <View className="h-36 rounded-xl bg-gray-100 overflow-hidden mb-2 items-center justify-center">
                {item.imageUrl ? (
                  <Image source={{ uri: item.imageUrl }} className="w-full h-full" resizeMode="cover" />
                ) : (
                  <Package size={32} strokeWidth={icon.stroke} color={colors.inkMuted} />
                )}
              </View>
              <Text className="text-sm font-medium text-gray-800 line-clamp-2">{item.name}</Text>
              <Text className="text-brand-500 font-semibold text-sm">{fmt(item.price)}</Text>
              {item.stockQuantity === 0 && (
                <Text className="text-xs text-red-500">{t('search.outOfStock')}</Text>
              )}
            </TouchableOpacity>
          )}
          ListEmptyComponent={
            <View className="py-16 items-center">
              <Text className="text-gray-400">{t('search.noProducts')}</Text>
            </View>
          }
        />
      )}
    </View>
  )
}
