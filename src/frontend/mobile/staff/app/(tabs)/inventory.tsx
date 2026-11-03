import { useState } from 'react'
import { FlatList, Text, View } from 'react-native'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Boxes } from 'lucide-react-native'
import { colors, icon } from '@/theme/theme'
import { apiClient } from '@/api/client'
import { Alert, BottomSheet, Button, Card, EmptyState, Screen, Skeleton, TextField, useToast } from '@/components/ui'

interface InventoryItem {
  id: string
  productId: string
  productName: string
  sku: string
  quantityAvailable: number
  quantityOnHand: number
}

export default function InventoryScreen() {
  const { t } = useTranslation()
  const toast = useToast()
  const [search, setSearch] = useState('')
  const [adjusting, setAdjusting] = useState<InventoryItem | null>(null)
  const [value, setValue] = useState('')
  const [invalid, setInvalid] = useState(false)
  const [saving, setSaving] = useState(false)

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['inventory', search],
    queryFn: async () => {
      const { data } = await apiClient.get('/v1/inventory', {
        params: { search: search || undefined, pageSize: 50 },
      })
      return data.items as InventoryItem[]
    },
  })

  function open(item: InventoryItem) {
    setAdjusting(item)
    setValue('')
    setInvalid(false)
  }

  async function save() {
    if (!adjusting) return
    const qty = parseInt(value, 10)
    if (!value || isNaN(qty)) {
      setInvalid(true)
      return
    }
    setSaving(true)
    try {
      await apiClient.post('/v1/inventory/adjust', {
        productId: adjusting.productId,
        quantity: qty - adjusting.quantityOnHand,
        reason: 'Staff adjustment',
      })
      setAdjusting(null)
      toast.success(t('inventory.adjusted'))
      refetch()
    } catch {
      toast.error(t('inventory.errorAdjustingStock'))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Screen scroll={false}>
      <View className="border-b border-line bg-surface px-4 pt-3">
        <TextField
          label={t('inventory.searchLabel')}
          placeholder={t('inventory.searchPlaceholder')}
          value={search}
          onChangeText={setSearch}
          returnKeyType="search"
          autoCapitalize="none"
        />
      </View>

      {isLoading ? (
        <View accessibilityLabel={t('inventory.loading')} accessibilityLiveRegion="polite" className="gap-2 p-4">
          {[0, 1, 2, 3].map((i) => <Skeleton key={i} className="h-20 w-full" />)}
        </View>
      ) : isError ? (
        <View className="gap-3 p-4">
          <Alert tone="danger">{t('common.loadError')}</Alert>
          <Button size="lg" variant="outline" onPress={() => refetch()}>{t('common.retry')}</Button>
        </View>
      ) : (
        <FlatList
          data={data ?? []}
          keyExtractor={(i) => i.id}
          keyboardShouldPersistTaps="handled"
          contentContainerClassName="p-4 gap-2"
          renderItem={({ item }) => (
            <Card>
              <View className="flex-row items-center justify-between">
                <View className="flex-1 pr-3">
                  <Text className="font-medium text-ink">{item.productName}</Text>
                  <Text className="text-sm text-ink-secondary">{item.sku}</Text>
                </View>
                <Text
                  accessibilityLabel={`${t('inventory.available')}: ${item.quantityAvailable}`}
                  className={`text-lg font-bold ${item.quantityAvailable === 0 ? 'text-danger' : item.quantityAvailable < 5 ? 'text-warning' : 'text-success'}`}
                >
                  {item.quantityAvailable}
                </Text>
              </View>
              <View className="mt-3">
                <Button size="lg" variant="outline" accessibilityLabel={t('inventory.adjustFor', { name: item.productName })} onPress={() => open(item)}>
                  {t('inventory.adjust')}
                </Button>
              </View>
            </Card>
          )}
          ListEmptyComponent={
            <EmptyState
              icon={<Boxes size={40} strokeWidth={icon.stroke} color={colors.inkMuted} />}
              title={t('inventory.empty')}
              hint={t('inventory.emptyHint')}
              action={search ? <Button size="lg" variant="outline" onPress={() => setSearch('')}>{t('inventory.clear')}</Button> : undefined}
            />
          }
        />
      )}

      <BottomSheet visible={adjusting !== null} title={t('inventory.adjustStockTitle')} onClose={() => setAdjusting(null)} closeLabel={t('common.close')}>
        {adjusting && (
          <View>
            <Text className="mb-1 font-medium text-ink">{adjusting.productName}</Text>
            <Text className="mb-3 text-sm text-ink-secondary">{t('inventory.currentQuantity', { quantity: adjusting.quantityOnHand })}</Text>
            <TextField
              label={t('inventory.newQuantity')}
              value={value}
              onChangeText={(v) => { setValue(v); setInvalid(false) }}
              keyboardType="number-pad"
              returnKeyType="done"
              onSubmitEditing={save}
              required
              error={invalid ? t('inventory.invalidQuantity') : undefined}
            />
            <View className="mt-2 gap-2">
              <Button size="lg" onPress={save} loading={saving}>{t('common.save')}</Button>
              <Button size="lg" variant="outline" onPress={() => setAdjusting(null)}>{t('common.cancel')}</Button>
            </View>
          </View>
        )}
      </BottomSheet>
    </Screen>
  )
}
