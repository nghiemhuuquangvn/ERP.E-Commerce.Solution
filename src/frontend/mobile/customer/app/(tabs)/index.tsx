import { View, Text, ScrollView, TouchableOpacity, Image, FlatList } from 'react-native'
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
}

export default function HomeScreen() {
  const router = useRouter()
  const { t } = useTranslation()
  const { data: featured } = useQuery({
    queryKey: ['products', 'featured'],
    queryFn: async () => {
      const { data } = await apiClient.get('/v1/products/featured')
      return data as Product[]
    },
  })

  const fmt = (n: number) =>
    new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)

  return (
    <ScrollView className="flex-1 bg-white">
      {/* Hero Banner */}
      <View className="bg-brand-500 px-6 py-10">
        <Text className="text-white text-2xl font-bold mb-1">{t('home.welcome')}</Text>
        <Text className="text-rose-200 text-sm mb-4">{t('home.discover')}</Text>
        <TouchableOpacity
          onPress={() => router.push('/(tabs)/search')}
          className="bg-white rounded-lg px-5 py-2.5 self-start"
        >
          <Text className="text-brand-500 font-semibold text-sm">{t('home.shopNow')}</Text>
        </TouchableOpacity>
      </View>

      {/* Featured Products */}
      <View className="px-4 pt-6">
        <Text className="text-lg font-bold text-gray-900 mb-4">{t('home.featuredProducts')}</Text>
        <FlatList
          data={featured ?? []}
          keyExtractor={(p) => p.id}
          horizontal
          showsHorizontalScrollIndicator={false}
          ItemSeparatorComponent={() => <View className="w-3" />}
          renderItem={({ item }) => (
            <TouchableOpacity
              onPress={() => router.push(`/product/${item.id}`)}
              className="w-40"
            >
              <View className="h-40 rounded-xl bg-gray-100 mb-2 overflow-hidden items-center justify-center">
                {item.imageUrl ? (
                  <Image source={{ uri: item.imageUrl }} className="w-full h-full" resizeMode="cover" />
                ) : (
                  <Package size={40} strokeWidth={icon.stroke} color={colors.inkMuted} />
                )}
              </View>
              <Text className="text-sm font-medium text-gray-800 line-clamp-2">{item.name}</Text>
              <Text className="text-brand-500 font-semibold mt-0.5">{fmt(item.price)}</Text>
            </TouchableOpacity>
          )}
        />
      </View>
    </ScrollView>
  )
}
