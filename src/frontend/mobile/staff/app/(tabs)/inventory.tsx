import { useState } from 'react'
import { FlatList, Pressable, Text, View } from 'react-native'
import axios from 'axios'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Boxes } from 'lucide-react-native'
import { colors, icon } from '@/theme/theme'
import { apiClient } from '@/api/client'
import { Alert, BottomSheet, Button, Card, EmptyState, Screen, Skeleton, TextField, useToast } from '@/components/ui'

// WareHouseService InventoryListItem (same contract as the Admin's inventory.api.ts): the product id is the key.
// WareHouseService AdjustmentReason: the request body carries the enum NUMBER (same mapping as the Admin's stockAdjustments.api.ts).
const REASONS = [
  { code: 'Damaged', value: 0 },
  { code: 'Lost', value: 1 },
  { code: 'Correction', value: 2 },
  { code: 'Other', value: 3 },
] as const
type ReasonCode = (typeof REASONS)[number]['code']

interface InventoryItem {
  productId: string
  productName: string
  productSku?: string
  availableQuantity: number
  totalQuantity: number
}

export default function InventoryScreen() {
  const { t } = useTranslation()
  const toast = useToast()
  const [search, setSearch] = useState('')
  const [adjusting, setAdjusting] = useState<InventoryItem | null>(null)
  const [value, setValue] = useState('')
  const [reason, setReason] = useState<ReasonCode>('Correction')
  const [note, setNote] = useState('')
  const [problem, setProblem] = useState<'' | 'invalid' | 'same'>('')
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
    setReason('Correction')
    setNote('')
    setProblem('')
  }

  async function save() {
    if (!adjusting) return
    const qty = parseInt(value, 10)
    if (!/^\d+$/.test(value.trim()) || isNaN(qty)) {
      setProblem('invalid')
      return
    }
    const quantityDelta = qty - adjusting.totalQuantity
    if (quantityDelta === 0) {
      setProblem('same')
      return
    }
    setSaving(true)
    try {
      // A request, not a change: the stock moves when a manager approves it (WareHouseService stock-adjustments).
      await apiClient.post('/v1/stock-adjustments', {
        productId: adjusting.productId,
        quantityDelta,
        reason: REASONS.find((r) => r.code === reason)!.value,
        notes: note.trim() || undefined,
      })
      setAdjusting(null)
      toast.success(t('inventory.requested'))
    } catch (e) {
      toast.error(axios.isAxiosError(e) && e.response?.status === 403 ? t('inventory.notAllowed') : t('inventory.errorAdjustingStock'))
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
          keyExtractor={(i) => i.productId}
          keyboardShouldPersistTaps="handled"
          contentContainerClassName="p-4 gap-2"
          renderItem={({ item }) => (
            <Card>
              <View className="flex-row items-center justify-between">
                <View className="flex-1 pr-3">
                  <Text className="font-medium text-ink">{item.productName}</Text>
                  <Text className="text-sm text-ink-secondary">{item.productSku}</Text>
                </View>
                <Text
                  accessibilityLabel={`${t('inventory.available')}: ${item.availableQuantity}`}
                  className={`text-lg font-bold ${item.availableQuantity === 0 ? 'text-danger' : item.availableQuantity < 5 ? 'text-warning' : 'text-success'}`}
                >
                  {item.availableQuantity}
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

      <BottomSheet visible={adjusting !== null} title={t('inventory.requestTitle')} onClose={() => setAdjusting(null)} closeLabel={t('common.close')}>
        {adjusting && (
          <View>
            <Text className="mb-1 font-medium text-ink">{adjusting.productName}</Text>
            <Text className="mb-3 text-sm text-ink-secondary">{t('inventory.currentQuantity', { quantity: adjusting.totalQuantity })}</Text>
            <TextField
              label={t('inventory.newQuantity')}
              value={value}
              onChangeText={(v) => { setValue(v); setProblem('') }}
              keyboardType="number-pad"
              returnKeyType="done"
              onSubmitEditing={save}
              required
              error={problem === 'invalid' ? t('inventory.invalidQuantity') : problem === 'same' ? t('inventory.nothingToChange') : undefined}
            />
            <Text className="mb-1 text-sm font-medium text-ink">{t('inventory.reason')}</Text>
            <View accessibilityRole="radiogroup" accessibilityLabel={t('inventory.reason')} className="mb-3 flex-row flex-wrap gap-2">
              {REASONS.map((r) => {
                const selected = reason === r.code
                return (
                  <Pressable
                    key={r.code}
                    onPress={() => setReason(r.code)}
                    accessibilityRole="radio"
                    accessibilityState={{ selected, checked: selected }}
                    aria-checked={selected}
                    className={`min-h-[52px] justify-center rounded-control border px-4 ${selected ? 'border-brand-action bg-brand-action' : 'border-line-strong bg-surface'}`}
                  >
                    <Text className={selected ? 'font-semibold text-on-brand' : 'text-ink'}>{t(`inventory.reasons.${r.code}`)}</Text>
                  </Pressable>
                )
              })}
            </View>
            <TextField label={t('inventory.note')} value={note} onChangeText={setNote} returnKeyType="done" />
            <View className="mt-2 gap-2">
              <Button size="lg" onPress={save} loading={saving}>{t('inventory.sendRequest')}</Button>
              <Button size="lg" variant="outline" onPress={() => setAdjusting(null)}>{t('common.cancel')}</Button>
            </View>
          </View>
        )}
      </BottomSheet>
    </Screen>
  )
}
