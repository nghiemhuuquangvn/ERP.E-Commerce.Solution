import { useState, useCallback } from 'react'
import { View, Text, FlatList, Pressable, ScrollView, ListRenderItem } from 'react-native'
import { useTranslation } from 'react-i18next'
import { usePOSStore } from '@/stores/pos.store'
import type { POSItem } from '@/stores/pos.store'
import { apiClient } from '@/api/client'
import { useAuthStore } from '@/stores/auth.store'
import { formatMoney } from '@/lib/money'
import { Button, Card, EmptyState, Screen, TextField, useToast } from '@/components/ui'

const PAYMENT_METHODS = ['Cash', 'CreditCard', 'EWallet']

export default function POSScreen() {
  const { t } = useTranslation()
  const toast = useToast()
  const store = usePOSStore()
  const user = useAuthStore((s) => s.user)
  const [productId, setProductId] = useState('')
  const [productName, setProductName] = useState('')
  const [unitPrice, setUnitPrice] = useState('')
  const [paymentMethod, setPaymentMethod] = useState('Cash')
  const [completing, setCompleting] = useState(false)

  const addItem = useCallback(() => {
    if (!productId || !unitPrice) return
    store.addItem({
      productId,
      productName: productName || productId,
      unitPrice: parseFloat(unitPrice),
      taxRate: 10,
    })
    setProductId('')
    setProductName('')
    setUnitPrice('')
  }, [productId, productName, unitPrice, store])

  const completeTransaction = useCallback(async () => {
    if (store.items.length === 0) return
    setCompleting(true)
    try {
      // POSController: POST pos/transactions opens (there is no /open); use the id we just got, not the store snapshot taken before it.
      let transactionId = store.transactionId
      if (!transactionId) {
        const { data: opened } = await apiClient.post('/v1/pos/transactions', {
          terminalId: 'TERM-001',
          cashierId: user?.id,
        })
        transactionId = opened.id as string
        store.setTransactionId(transactionId)
      }
      for (const item of store.items) {
        await apiClient.post(`/v1/pos/transactions/${transactionId}/items`, {
          productId: item.productId,
          productName: item.productName,
          quantity: item.quantity,
          unitPrice: item.unitPrice,
          taxRate: item.taxRate,
        })
      }
      await apiClient.post(`/v1/pos/transactions/${transactionId}/complete`, {
        paymentMethod,
      })
      toast.success(t('pos.successMessage', { total: formatMoney(store.total()) }))
      store.reset()
    } catch {
      toast.error(t('pos.errorCompletingTransaction'))
    } finally {
      setCompleting(false)
    }
  }, [store, user, paymentMethod, toast, t])

  // Stable renderItem — FlatList skips re-render of unchanged rows
  const renderItem = useCallback<ListRenderItem<POSItem>>(
    ({ item }) => (
      <View className="mb-2">
        <Card>
          <View className="flex-row items-center justify-between">
            <View className="flex-1 pr-3">
              <Text className="font-medium text-ink">{item.productName}</Text>
              <Text className="text-sm text-ink-secondary">{formatMoney(item.unitPrice)} × {item.quantity}</Text>
            </View>
            <Text className="font-semibold text-ink">{formatMoney(item.unitPrice * item.quantity)}</Text>
          </View>
        </Card>
      </View>
    ),
    [],
  )

  return (
    <Screen scroll={false}>
      {/* Product Add Bar */}
      <View className="border-b border-line bg-surface p-4">
        <Text accessibilityRole="header" className="mb-2 text-sm font-semibold text-ink-secondary">{t('pos.addProduct')}</Text>
        <View className="flex-row gap-2">
          <View className="flex-1">
            <TextField label={t('pos.productIdLabel')} placeholder={t('pos.productIdPlaceholder')} value={productId} onChangeText={setProductId} autoCapitalize="none" returnKeyType="next" />
          </View>
          <View className="flex-1">
            <TextField label={t('pos.nameLabel')} placeholder={t('pos.namePlaceholder')} value={productName} onChangeText={setProductName} returnKeyType="next" />
          </View>
        </View>
        <View className="flex-row items-end gap-2">
          <View className="flex-1">
            <TextField label={t('pos.unitPriceLabel')} placeholder={t('pos.unitPricePlaceholder')} value={unitPrice} onChangeText={setUnitPrice} keyboardType="numeric" returnKeyType="done" onSubmitEditing={addItem} />
          </View>
          <View className="mb-3">
            <Button size="lg" fullWidth={false} accessibilityLabel={t('pos.addToSale')} onPress={addItem}>{t('pos.add')}</Button>
          </View>
        </View>
      </View>

      {/* Items List */}
      <FlatList
        data={store.items}
        keyExtractor={(i) => i.productId}
        className="flex-1 px-4 pt-3"
        renderItem={renderItem}
        ListEmptyComponent={<EmptyState title={t('pos.noItems')} hint={t('pos.noItemsHint')} />}
      />

      {/* Footer Summary */}
      <View className="border-t border-line bg-surface p-4">
        <View className="mb-1 flex-row justify-between">
          <Text className="text-sm text-ink-secondary">{t('pos.subtotal')}</Text>
          <Text className="text-sm text-ink">{formatMoney(store.subtotal())}</Text>
        </View>
        <View className="mb-3 flex-row justify-between">
          <Text className="text-sm text-ink-secondary">{t('pos.tax')}</Text>
          <Text className="text-sm text-ink">{formatMoney(store.taxAmount())}</Text>
        </View>
        <View className="mb-4 flex-row justify-between">
          <Text className="font-bold text-ink">{t('pos.total')}</Text>
          <Text className="text-xl font-bold text-brand-action">{formatMoney(store.total())}</Text>
        </View>

        <ScrollView horizontal showsHorizontalScrollIndicator={false} className="mb-3" accessibilityRole="radiogroup" accessibilityLabel={t('pos.paymentMethod')}>
          {PAYMENT_METHODS.map((m) => {
            const selected = paymentMethod === m
            return (
              <Pressable
                key={m}
                onPress={() => setPaymentMethod(m)}
                accessibilityRole="radio"
                accessibilityState={{ selected, checked: selected }}
                    aria-checked={selected}
                className={`mr-2 min-h-[52px] justify-center rounded-control border px-5 ${selected ? 'border-brand-action bg-brand-action' : 'border-line-strong bg-surface'}`}
              >
                <Text className={selected ? 'font-semibold text-on-brand' : 'text-ink'}>{t(`pos.paymentMethods.${m}`)}</Text>
              </Pressable>
            )
          })}
        </ScrollView>

        <Button size="lg" onPress={completeTransaction} loading={completing} disabled={store.items.length === 0}>
          {completing ? t('pos.processing') : t('pos.completeTransaction')}
        </Button>
      </View>
    </Screen>
  )
}
