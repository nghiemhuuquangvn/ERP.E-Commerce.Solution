import { useEffect, useState } from 'react'
import { Image, ScrollView, Text, View } from 'react-native'
import { useLocalSearchParams, useRouter } from 'expo-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Package } from 'lucide-react-native'
import { colors, icon } from '@/theme/theme'
import { apiClient } from '@/api/client'
import { formatMoney } from '@/lib/money'
import { useCartStore } from '@/stores/cart.store'
import { useLocaleStore } from '@/stores/locale.store'
import type { ProductDetail } from '@/components/ProductTile'
import { Alert, Button, EmptyState, QuantityStepper, Screen, Skeleton, useToast } from '@/components/ui'

/** One product with its stock and an Add to cart bar that stays at the bottom of the screen (the phone twin of the Storefront's detail page). */
export default function ProductScreen() {
  const { id } = useLocalSearchParams<{ id: string }>()
  const router = useRouter()
  const { t } = useTranslation()
  const toast = useToast()
  const locale = useLocaleStore((s) => s.locale)
  const addItem = useCartStore((s) => s.addItem)
  const [qty, setQty] = useState(1)

  const { data: product, isLoading, isError, refetch } = useQuery({
    queryKey: ['products', id, locale],
    queryFn: async () => (await apiClient.get<ProductDetail | null>(`/v1/products/${id}`, { params: { languageCode: locale } })).data ?? null,
  })

  // A different product (or a refreshed stock figure) must not keep a quantity that no longer fits.
  useEffect(() => setQty(1), [id])

  const back = (
    <View className="px-2 pt-2">
      <Button variant="ghost" fullWidth={false} onPress={() => router.back()}>{`‹ ${t('product.back')}`}</Button>
    </View>
  )

  if (isLoading) {
    return (
      <Screen scroll={false} edges={['top', 'bottom', 'left', 'right']}>
        {back}
        <View accessibilityLabel={t('product.loading')} accessibilityLiveRegion="polite" className="gap-3 p-4">
          <Skeleton className="h-64 w-full" />
          <Skeleton className="h-6 w-3/4" />
          <Skeleton className="h-6 w-1/3" />
          <Skeleton className="h-20 w-full" />
        </View>
      </Screen>
    )
  }

  if (isError) {
    return (
      <Screen scroll={false} edges={['top', 'bottom', 'left', 'right']}>
        {back}
        <View className="gap-3 p-4">
          <Alert tone="danger">{t('product.loadFailed')}</Alert>
          <Button variant="outline" onPress={() => refetch()}>{t('common.retry')}</Button>
        </View>
      </Screen>
    )
  }

  if (!product) {
    return (
      <Screen scroll={false} edges={['top', 'bottom', 'left', 'right']}>
        {back}
        <EmptyState title={t('product.notFound')} action={<Button variant="outline" onPress={() => router.replace('/(tabs)/search')}>{t('cart.browseProducts')}</Button>} />
      </Screen>
    )
  }

  const out = product.stockQuantity === 0

  function add() {
    if (!product || product.stockQuantity === 0) return
    addItem({ productId: product.id, productName: product.name, unitPrice: product.price, imageUrl: product.imageUrl }, qty)
    toast.success(t('product.addedToCart'))
    setQty(1)
  }

  return (
    <Screen scroll={false} edges={['top', 'bottom', 'left', 'right']}>
      {back}
      <ScrollView className="flex-1" contentContainerClassName="p-4">
        <View className="mb-4 aspect-square items-center justify-center overflow-hidden rounded-card bg-surface-muted">
          {product.imageUrl ? (
            <Image source={{ uri: product.imageUrl }} className="h-full w-full" resizeMode="cover" accessibilityLabel={product.name} />
          ) : (
            <View accessibilityElementsHidden importantForAccessibility="no-hide-descendants">
              <Package size={64} strokeWidth={icon.stroke} color={colors.inkMuted} />
            </View>
          )}
        </View>
        {!!product.categoryName && <Text className="text-sm text-ink-secondary">{product.categoryName}</Text>}
        <Text accessibilityRole="header" className="mt-1 text-2xl font-bold text-ink">{product.name}</Text>
        <Text className="mt-3 text-2xl font-bold text-brand-action">{formatMoney(product.price)}</Text>
        <Text className={`mt-2 text-sm ${out ? 'text-danger-soft-text' : 'text-success-soft-text'}`}>
          {out ? t('product.outOfStock') : t('product.inStock')}
        </Text>
        {!!product.description && <Text className="mt-4 text-base text-ink-secondary">{product.description}</Text>}
      </ScrollView>

      <View className="flex-row items-center gap-3 border-t border-line bg-surface px-4 py-3">
        <QuantityStepper
          value={qty}
          onChange={setQty}
          name={t('product.quantity')}
          min={1}
          max={product.stockQuantity > 0 ? product.stockQuantity : undefined}
          decreaseLabel={t('cart.decrease')}
          increaseLabel={t('cart.increase')}
        />
        <View className="flex-1">
          <Button disabled={out} onPress={add}>{out ? t('product.outOfStock') : t('product.addToCart')}</Button>
        </View>
      </View>
    </Screen>
  )
}
