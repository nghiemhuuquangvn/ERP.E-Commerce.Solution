import { useState, useCallback, useEffect } from 'react'
import {
  View,
  Text,
  FlatList,
  TouchableOpacity,
  Alert,
  Linking,
  ListRenderItem,
} from 'react-native'
import { useRouter } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { useCartStore } from '@/stores/cart.store'
import type { CartItem } from '@/stores/cart.store'
import { useAuthStore } from '@/stores/auth.store'
import { apiClient } from '@/api/client'
import { fetchPaymentMethods, startCheckout } from '@/api/payments'
import type { PaymentMethodOption, Order } from '@/api/payments'

// Module-level formatter — created once per app lifecycle
const fmt = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export default function CartScreen() {
  const router = useRouter()
  const { t } = useTranslation()
  const cartStore = useCartStore()
  const authStore = useAuthStore()
  const [placing, setPlacing] = useState(false)
  // What this shop really offers (cash on delivery + the gateways it set up).
  const [methods, setMethods] = useState<PaymentMethodOption[]>([])
  const [method, setMethod] = useState('')

  useEffect(() => {
    fetchPaymentMethods()
      .then((list) => {
        setMethods(list)
        setMethod((current) => current || list[0]?.code || '')
      })
      .catch(() => setMethods([]))
  }, [])

  const items = cartStore.items

  const checkout = useCallback(async () => {
    if (!authStore.isAuthenticated) {
      Alert.alert(t('cart.signInRequiredTitle'), t('cart.signInRequiredMessage'), [
        { text: t('common.cancel'), style: 'cancel' },
        { text: t('common.signIn'), onPress: () => router.push('/(auth)/login') },
      ])
      return
    }

    setPlacing(true)
    try {
      const { data: order } = await apiClient.post<Order>('/v1/orders', {
        customerId: authStore.user!.id,
        customerName: authStore.user!.fullName?.trim() || authStore.user!.email,
        customerEmail: authStore.user!.email,
        lineItems: cartStore.items.map((i) => ({
          productId: i.productId,
          productName: i.productName,
          unitPrice: i.unitPrice,
          quantity: i.quantity,
        })),
        paymentMethod: method || undefined,
      })
      cartStore.clear()
      // Online method: open the gateway's page now; the order screen shows the result when the customer comes back.
      if (methods.find((m) => m.code === method)?.isGateway) {
        try {
          const { paymentUrl } = await startCheckout(order.orderId)
          await Linking.openURL(paymentUrl)
        } catch {
          // The order screen offers "Pay now" again.
        }
      }
      router.push(`/order/${order.orderId}`)
    } catch {
      Alert.alert(t('common.error'), t('cart.errorPlacingOrder'))
    } finally {
      setPlacing(false)
    }
  }, [authStore, cartStore, router, t, method, methods])

  // Stable renderItem — FlatList re-renders only changed rows
  const renderItem = useCallback<ListRenderItem<CartItem>>(
    ({ item }) => (
      <View className="flex-row items-center gap-3 p-3 rounded-xl border border-gray-100 bg-gray-50">
        <View className="w-14 h-14 rounded-lg bg-gray-200 items-center justify-center">
          <Text className="text-2xl">📦</Text>
        </View>
        <View className="flex-1">
          <Text className="font-medium text-gray-800 text-sm">{item.productName}</Text>
          <Text className="text-gray-500 text-xs">{fmt.format(item.unitPrice)} {t('cart.each')}</Text>
        </View>
        <View className="flex-row items-center gap-2">
          <TouchableOpacity
            onPress={() => cartStore.updateQty(item.productId, item.quantity - 1)}
            className="w-7 h-7 rounded-full bg-gray-200 items-center justify-center"
          >
            <Text className="text-gray-700">−</Text>
          </TouchableOpacity>
          <Text className="w-5 text-center text-sm font-medium">{item.quantity}</Text>
          <TouchableOpacity
            onPress={() => cartStore.updateQty(item.productId, item.quantity + 1)}
            className="w-7 h-7 rounded-full bg-gray-200 items-center justify-center"
          >
            <Text className="text-gray-700">+</Text>
          </TouchableOpacity>
        </View>
        <Text className="w-16 text-right font-semibold text-sm">
          {fmt.format(item.unitPrice * item.quantity)}
        </Text>
      </View>
    ),
    [cartStore, t],
  )

  if (items.length === 0) {
    return (
      <View className="flex-1 bg-white items-center justify-center px-6">
        <Text className="text-4xl mb-4">🛒</Text>
        <Text className="text-lg font-semibold text-gray-700 mb-2">{t('cart.emptyTitle')}</Text>
        <TouchableOpacity
          onPress={() => router.push('/(tabs)/search')}
          className="bg-brand-500 rounded-xl px-6 py-3"
        >
          <Text className="text-white font-semibold">{t('cart.browseProducts')}</Text>
        </TouchableOpacity>
      </View>
    )
  }

  return (
    <View className="flex-1 bg-white">
      <FlatList
        data={items}
        keyExtractor={(i) => i.productId}
        contentContainerClassName="p-4 gap-3"
        renderItem={renderItem}
      />

      <View className="border-t border-gray-100 p-4">
        <View className="flex-row justify-between mb-1">
          <Text className="text-gray-500">{t('cart.subtotal')}</Text>
          <Text>{fmt.format(cartStore.subtotal())}</Text>
        </View>
        <View className="flex-row justify-between mb-4">
          <Text className="font-bold text-gray-900 text-lg">{t('cart.total')}</Text>
          <Text className="font-bold text-brand-500 text-lg">{fmt.format(cartStore.total())}</Text>
        </View>
        {methods.length > 0 && (
          <View className="mb-4">
            <Text className="mb-2 text-xs uppercase text-gray-400">{t('cart.paymentMethod')}</Text>
            <View className="flex-row flex-wrap gap-2">
              {methods.map((m) => (
                <TouchableOpacity
                  key={m.code}
                  onPress={() => setMethod(m.code)}
                  className={`rounded-full border px-4 py-2 ${method === m.code ? 'border-brand-500 bg-brand-500' : 'border-gray-200 bg-white'}`}
                >
                  <Text className={method === m.code ? 'font-semibold text-white' : 'text-gray-700'}>{t(`cart.methods.${m.code}`)}</Text>
                </TouchableOpacity>
              ))}
            </View>
          </View>
        )}
        <TouchableOpacity
          onPress={checkout}
          disabled={placing || !method}
          className="bg-brand-500 rounded-xl py-4 items-center"
        >
          <Text className="text-white font-bold text-base">
            {placing ? t('cart.placingOrder') : t('cart.placeOrder')}
          </Text>
        </TouchableOpacity>
      </View>
    </View>
  )
}
