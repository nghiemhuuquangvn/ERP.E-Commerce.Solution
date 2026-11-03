import { FlatList, Text, View } from 'react-native'
import { useRouter } from 'expo-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { apiClient } from '@/api/client'
import { ProductTile, type ProductSummary } from '@/components/ProductTile'
import { Alert, Button, EmptyState, Screen, Skeleton } from '@/components/ui'

export default function HomeScreen() {
  const router = useRouter()
  const { t } = useTranslation()
  const { data: featured, isLoading, isError, refetch } = useQuery({
    queryKey: ['products', 'featured'],
    queryFn: async () => {
      const { data } = await apiClient.get('/v1/products/featured')
      return data as ProductSummary[]
    },
  })

  return (
    <Screen>
      <View className="-mx-4 -mt-4 bg-brand-subtle px-6 py-10">
        <Text accessibilityRole="header" className="mb-1 text-2xl font-bold text-ink">{t('home.welcome')}</Text>
        <Text className="mb-4 text-sm text-ink-secondary">{t('home.discover')}</Text>
        <Button fullWidth={false} onPress={() => router.push('/(tabs)/search')}>{t('home.shopNow')}</Button>
      </View>

      <View className="pt-6">
        <Text accessibilityRole="header" className="mb-4 text-lg font-bold text-ink">{t('home.featuredProducts')}</Text>
        {isLoading ? (
          <View accessibilityLabel={t('common.loading')} accessibilityLiveRegion="polite" className="flex-row gap-3">
            {[0, 1, 2].map((i) => <Skeleton key={i} className="h-52 w-40" />)}
          </View>
        ) : isError ? (
          <View className="gap-3">
            <Alert tone="danger">{t('common.loadError')}</Alert>
            <Button variant="outline" onPress={() => refetch()}>{t('common.retry')}</Button>
          </View>
        ) : (featured ?? []).length === 0 ? (
          <EmptyState title={t('search.noProducts')} />
        ) : (
          <FlatList
            data={featured}
            keyExtractor={(p) => p.id}
            horizontal
            showsHorizontalScrollIndicator={false}
            ItemSeparatorComponent={() => <View className="w-3" />}
            renderItem={({ item }) => <ProductTile product={item} onPress={() => router.push(`/product/${item.id}`)} />}
          />
        )}
      </View>
    </Screen>
  )
}
