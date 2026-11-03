import { useEffect, useState } from 'react'
import { Pressable, Text, View } from 'react-native'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { searchProducts, type PosProduct } from '@/lib/posProducts'
import { formatMoney } from '@/lib/money'
import { Alert, Button, Skeleton, TextField } from '@/components/ui'

const MIN_CHARS = 2
const DEBOUNCE_MS = 350

/** Search by name or SKU; a tap on a result puts one unit on the sale. A product with no stock is shown but cannot be picked. */
export function ProductPicker({ onPick }: { onPick: (p: PosProduct) => void }) {
  const { t } = useTranslation()
  const [text, setText] = useState('')
  const [term, setTerm] = useState('')

  useEffect(() => {
    const timer = setTimeout(() => setTerm(text.trim()), DEBOUNCE_MS)
    return () => clearTimeout(timer)
  }, [text])

  const active = term.length >= MIN_CHARS
  const { data, isFetching, isError, refetch } = useQuery({
    queryKey: ['pos-product-search', term],
    enabled: active,
    retry: false,
    queryFn: () => searchProducts(term),
  })

  return (
    <View>
      <TextField
        label={t('pos.searchLabel')}
        placeholder={t('pos.searchPlaceholder')}
        value={text}
        onChangeText={setText}
        autoCapitalize="none"
        returnKeyType="search"
        onSubmitEditing={() => setTerm(text.trim())}
        hint={text.trim().length > 0 && text.trim().length < MIN_CHARS ? t('pos.searchMin', { count: MIN_CHARS }) : undefined}
      />
      {active && (
        isFetching && !data ? (
          <View accessibilityLabel={t('common.loading')} accessibilityLiveRegion="polite" className="gap-2"><Skeleton className="h-14 w-full" /><Skeleton className="h-14 w-full" /></View>
        ) : isError ? (
          <View className="gap-2"><Alert tone="danger">{t('common.loadError')}</Alert><Button size="lg" variant="outline" onPress={() => refetch()}>{t('common.retry')}</Button></View>
        ) : (data ?? []).length === 0 ? (
          <Text className="py-2 text-sm text-ink-secondary">{t('pos.searchNone')}</Text>
        ) : (
          <View className="gap-2">
            {(data ?? []).slice(0, 6).map((p) => {
              const out = p.stockQuantity === 0
              return (
                <Pressable
                  key={p.productId}
                  disabled={out}
                  onPress={() => { onPick(p); setText(''); setTerm('') }}
                  accessibilityRole="button"
                  accessibilityLabel={`${t('pos.addProductNamed', { name: p.name })}, ${formatMoney(p.price)}${out ? `, ${t('pos.outOfStock')}` : ''}`}
                  accessibilityState={{ disabled: out }}
                  className={`min-h-[52px] flex-row items-center justify-between rounded-control border border-line bg-surface px-4 py-2 ${out ? 'opacity-50' : ''}`}
                >
                  <View className="flex-1 pr-3">
                    <Text className="font-medium text-ink">{p.name}</Text>
                    <Text className="text-sm text-ink-secondary">{p.sku}{out ? ` · ${t('pos.outOfStock')}` : ''}</Text>
                  </View>
                  <Text className="font-semibold text-ink">{formatMoney(p.price)}</Text>
                </Pressable>
              )
            })}
          </View>
        )
      )}
    </View>
  )
}
