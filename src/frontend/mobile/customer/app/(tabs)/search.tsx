import { useState } from 'react'
import { FlatList, View } from 'react-native'
import { useRouter } from 'expo-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Package } from 'lucide-react-native'
import { colors, icon } from '@/theme/theme'
import { apiClient } from '@/api/client'
import { ProductTile, type ProductSummary } from '@/components/ProductTile'
import { Alert, Button, EmptyState, Screen, Skeleton, TextField } from '@/components/ui'

export default function SearchScreen() {
  const router = useRouter()
  const { t } = useTranslation()
  const [search, setSearch] = useState('')

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['products', 'search', search],
    queryFn: async () => {
      // The search route answers with an array, the list route with { total, skip, take, products }.
      if (search.trim()) {
        const { data } = await apiClient.get('/v1/products/search', { params: { searchTerm: search.trim() } })
        return data as ProductSummary[]
      }
      const { data } = await apiClient.get('/v1/products', { params: { take: 30 } })
      return (data.products ?? []) as ProductSummary[]
    },
    enabled: true,
  })

  return (
    <Screen scroll={false}>
      <View className="border-b border-line bg-surface px-4 pt-3">
        <TextField
          label={t('search.label')}
          placeholder={t('search.placeholder')}
          value={search}
          onChangeText={setSearch}
          returnKeyType="search"
          autoCapitalize="none"
        />
      </View>

      {isLoading ? (
        <View accessibilityLabel={t('common.loading')} accessibilityLiveRegion="polite" className="flex-row flex-wrap gap-3 p-3">
          {[0, 1, 2, 3].map((i) => <Skeleton key={i} className="h-48 w-[47%]" />)}
        </View>
      ) : isError ? (
        <View className="gap-3 p-4">
          <Alert tone="danger">{t('common.loadError')}</Alert>
          <Button variant="outline" onPress={() => refetch()}>{t('common.retry')}</Button>
        </View>
      ) : (
        <FlatList
          data={data ?? []}
          keyExtractor={(p) => p.id}
          numColumns={2}
          keyboardShouldPersistTaps="handled"
          contentContainerClassName="p-3"
          columnWrapperClassName="gap-3"
          ItemSeparatorComponent={() => <View className="h-3" />}
          renderItem={({ item }) => <ProductTile size="md" product={item} onPress={() => router.push(`/product/${item.id}`)} />}
          ListEmptyComponent={
            <EmptyState
              icon={<Package size={40} strokeWidth={icon.stroke} color={colors.inkMuted} />}
              title={t('search.noProducts')}
              hint={t('search.noProductsHint')}
              action={search ? <Button variant="outline" onPress={() => setSearch('')}>{t('search.clear')}</Button> : undefined}
            />
          }
        />
      )}
    </Screen>
  )
}
