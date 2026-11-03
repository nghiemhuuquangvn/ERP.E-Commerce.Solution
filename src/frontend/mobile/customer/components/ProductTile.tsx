import { Image, Pressable, Text, View } from 'react-native'
import { Package } from 'lucide-react-native'
import { useTranslation } from 'react-i18next'
import { colors, icon } from '@/theme/theme'
import { formatMoney } from '@/lib/money'

export interface ProductSummary {
  id: string
  name: string
  price: number
  imageUrl?: string
  stockQuantity?: number
}

/** One product: picture (or a placeholder), name, price. The whole tile is one button named after the product. */
export function ProductTile({ product, onPress, size = 'lg' }: { product: ProductSummary; onPress: () => void; size?: 'md' | 'lg' }) {
  const { t } = useTranslation()
  const out = product.stockQuantity === 0
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={`${product.name}, ${formatMoney(product.price)}${out ? `, ${t('search.outOfStock')}` : ''}`}
      className={size === 'lg' ? 'w-40' : 'flex-1'}
    >
      <View className={`${size === 'lg' ? 'h-40' : 'h-36'} mb-2 items-center justify-center overflow-hidden rounded-card bg-surface-muted`}>
        {product.imageUrl ? (
          <Image source={{ uri: product.imageUrl }} className="h-full w-full" resizeMode="cover" accessibilityElementsHidden />
        ) : (
          <Package size={size === 'lg' ? 40 : 32} strokeWidth={icon.stroke} color={colors.inkMuted} />
        )}
      </View>
      <Text numberOfLines={2} className="text-sm font-medium text-ink">{product.name}</Text>
      <Text className="mt-0.5 text-sm font-semibold text-brand-action">{formatMoney(product.price)}</Text>
      {out && <Text className="text-sm text-danger">{t('search.outOfStock')}</Text>}
    </Pressable>
  )
}
