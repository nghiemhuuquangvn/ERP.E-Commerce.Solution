import { useState, useCallback } from 'react'
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  FlatList,
  Alert,
  ScrollView,
  ListRenderItem,
} from 'react-native'
import { usePOSStore } from '@/stores/pos.store'
import type { POSItem } from '@/stores/pos.store'
import { apiClient } from '@/api/client'
import { useAuthStore } from '@/stores/auth.store'

const PAYMENT_METHODS = ['Cash', 'CreditCard', 'EWallet']

// Module-level formatter — created once per app lifecycle
const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export default function POSScreen() {
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
      if (!store.transactionId) {
        const { data: opened } = await apiClient.post('/v1/pos/transactions/open', {
          terminalId: 'TERM-001',
          cashierId: user?.id,
        })
        store.setTransactionId(opened.id)
      }
      for (const item of store.items) {
        await apiClient.post(`/v1/pos/transactions/${store.transactionId}/items`, {
          productId: item.productId,
          productName: item.productName,
          quantity: item.quantity,
          unitPrice: item.unitPrice,
          taxRate: item.taxRate,
        })
      }
      await apiClient.post(`/v1/pos/transactions/${store.transactionId}/complete`, {
        paymentMethod,
      })
      Alert.alert('Success', `Transaction complete. Total: ${fmt.format(store.total())}`, [
        { text: 'OK', onPress: () => store.reset() },
      ])
    } catch {
      Alert.alert('Error', 'Failed to complete transaction.')
    } finally {
      setCompleting(false)
    }
  }, [store, user, paymentMethod])

  // Stable renderItem — FlatList skips re-render of unchanged rows
  const renderItem = useCallback<ListRenderItem<POSItem>>(
    ({ item }) => (
      <View className="bg-white rounded-xl px-4 py-3 mb-2 flex-row items-center justify-between border border-gray-100">
        <View className="flex-1">
          <Text className="font-medium text-gray-800">{item.productName}</Text>
          <Text className="text-xs text-gray-500">
            {fmt.format(item.unitPrice)} × {item.quantity}
          </Text>
        </View>
        <Text className="font-semibold text-gray-900">
          {fmt.format(item.unitPrice * item.quantity)}
        </Text>
      </View>
    ),
    [],
  )

  return (
    <View className="flex-1 bg-gray-50">
      {/* Product Add Bar */}
      <View className="bg-white p-4 border-b border-gray-100">
        <Text className="text-sm font-semibold text-gray-700 mb-2">Add Product</Text>
        <View className="flex-row gap-2 mb-2">
          <TextInput
            className="flex-1 border border-gray-300 rounded-lg px-3 py-2 text-sm"
            placeholder="Product ID"
            value={productId}
            onChangeText={setProductId}
          />
          <TextInput
            className="flex-1 border border-gray-300 rounded-lg px-3 py-2 text-sm"
            placeholder="Name"
            value={productName}
            onChangeText={setProductName}
          />
        </View>
        <View className="flex-row gap-2">
          <TextInput
            className="flex-1 border border-gray-300 rounded-lg px-3 py-2 text-sm"
            placeholder="Unit Price"
            value={unitPrice}
            onChangeText={setUnitPrice}
            keyboardType="numeric"
          />
          <TouchableOpacity
            onPress={addItem}
            className="bg-primary-600 rounded-lg px-4 py-2 items-center justify-center"
          >
            <Text className="text-white font-medium">+ Add</Text>
          </TouchableOpacity>
        </View>
      </View>

      {/* Items List */}
      <FlatList
        data={store.items}
        keyExtractor={(i) => i.productId}
        className="flex-1 px-4 pt-3"
        renderItem={renderItem}
        ListEmptyComponent={
          <View className="py-12 items-center">
            <Text className="text-gray-400">No items added yet.</Text>
          </View>
        }
      />

      {/* Footer Summary */}
      <View className="bg-white border-t border-gray-100 p-4">
        <View className="flex-row justify-between mb-1">
          <Text className="text-sm text-gray-500">Subtotal</Text>
          <Text className="text-sm">{fmt.format(store.subtotal())}</Text>
        </View>
        <View className="flex-row justify-between mb-3">
          <Text className="text-sm text-gray-500">Tax</Text>
          <Text className="text-sm">{fmt.format(store.taxAmount())}</Text>
        </View>
        <View className="flex-row justify-between mb-4">
          <Text className="font-bold text-gray-900">Total</Text>
          <Text className="text-xl font-bold text-primary-600">{fmt.format(store.total())}</Text>
        </View>

        <ScrollView horizontal showsHorizontalScrollIndicator={false} className="mb-3">
          {PAYMENT_METHODS.map((m) => (
            <TouchableOpacity
              key={m}
              onPress={() => setPaymentMethod(m)}
              className={`mr-2 rounded-lg px-4 py-2 border ${
                paymentMethod === m
                  ? 'bg-primary-600 border-primary-600'
                  : 'bg-white border-gray-300'
              }`}
            >
              <Text className={paymentMethod === m ? 'text-white text-sm' : 'text-gray-700 text-sm'}>
                {m}
              </Text>
            </TouchableOpacity>
          ))}
        </ScrollView>

        <TouchableOpacity
          onPress={completeTransaction}
          disabled={completing || store.items.length === 0}
          className="bg-primary-600 rounded-xl py-4 items-center disabled:opacity-40"
        >
          <Text className="text-white font-bold text-base">
            {completing ? 'Processing…' : 'Complete Transaction'}
          </Text>
        </TouchableOpacity>
      </View>
    </View>
  )
}
