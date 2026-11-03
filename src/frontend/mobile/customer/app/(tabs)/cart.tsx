import { useState, useCallback, useEffect } from 'react'
import { View, Text, FlatList, Pressable, Alert, Linking, ListRenderItem } from 'react-native'
import { useRouter } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { Package, ShoppingCart } from 'lucide-react-native'
import { colors, icon } from '@/theme/theme'
import { useCartStore } from '@/stores/cart.store'
import type { CartItem } from '@/stores/cart.store'
import { useAuthStore } from '@/stores/auth.store'
import { apiClient } from '@/api/client'
import { fetchPaymentMethods, startCheckout } from '@/api/payments'
import type { PaymentMethodOption, Order } from '@/api/payments'
import { formatMoney } from '@/lib/money'
import { Button, Card, EmptyState, QuantityStepper, Screen, useToast } from '@/components/ui'

export default function CartScreen() {
  const router = useRouter()
  const { t } = useTranslation()
  const toast = useToast()
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
      toast.error(t('cart.errorPlacingOrder'))
    } finally {
      setPlacing(false)
    }
  }, [authStore, cartStore, router, t, method, methods, toast])

  // Stable renderItem — FlatList re-renders only changed rows
  const renderItem = useCallback<ListRenderItem<CartItem>>(
    ({ item }) => (
      <Card>
        <View className="flex-row items-center gap-3">
          <View className="h-14 w-14 items-center justify-center rounded-control bg-surface-muted" accessibilityElementsHidden>
            <Package size={24} strokeWidth={icon.stroke} color={colors.inkMuted} />
          </View>
          <View className="flex-1">
            <Text className="text-sm font-medium text-ink">{item.productName}</Text>
            <Text className="text-sm text-ink-secondary">{formatMoney(item.unitPrice)} {t('cart.each')}</Text>
          </View>
          <Text className="text-right text-sm font-semibold text-ink">{formatMoney(item.unitPrice * item.quantity)}</Text>
        </View>
        <View className="mt-3 flex-row items-center justify-between">
          <QuantityStepper
            value={item.quantity}
            name={item.productName}
            min={0}
            decreaseLabel={t('cart.decrease')}
            increaseLabel={t('cart.increase')}
            onChange={(next) => cartStore.updateQty(item.productId, next)}
          />
          <Button
            variant="ghost"
            fullWidth={false}
            accessibilityLabel={`${t('cart.remove')} ${item.productName}`}
            onPress={() => cartStore.removeItem(item.productId)}
          >
            {t('cart.remove')}
          </Button>
        </View>
      </Card>
    ),
    [cartStore, t],
  )

  if (items.length === 0) {
    return (
      <Screen scroll={false}>
        <EmptyState
          icon={<ShoppingCart size={40} strokeWidth={icon.stroke} color={colors.inkMuted} />}
          title={t('cart.emptyTitle')}
          action={<Button onPress={() => router.push('/(tabs)/search')}>{t('cart.browseProducts')}</Button>}
        />
      </Screen>
    )
  }

  return (
    <Screen scroll={false}>
      <FlatList data={items} keyExtractor={(i) => i.productId} contentContainerClassName="p-4 gap-3" renderItem={renderItem} />

      <View className="border-t border-line bg-surface p-4">
        <Text accessibilityRole="header" className="mb-2 text-sm font-semibold text-ink-secondary">{t('cart.summary')}</Text>
        <View className="mb-1 flex-row justify-between">
          <Text className="text-ink-secondary">{t('cart.subtotal')}</Text>
          <Text className="text-ink">{formatMoney(cartStore.subtotal())}</Text>
        </View>
        <View className="mb-4 flex-row justify-between">
          <Text className="text-lg font-bold text-ink">{t('cart.total')}</Text>
          <Text className="text-lg font-bold text-brand-action">{formatMoney(cartStore.total())}</Text>
        </View>
        {methods.length > 0 && (
          <View className="mb-4" accessibilityRole="radiogroup" accessibilityLabel={t('cart.paymentMethod')}>
            <Text className="mb-2 text-sm text-ink-secondary">{t('cart.paymentMethod')}</Text>
            <View className="flex-row flex-wrap gap-2">
              {methods.map((m) => {
                const selected = method === m.code
                return (
                  <Pressable
                    key={m.code}
                    onPress={() => setMethod(m.code)}
                    accessibilityRole="radio"
                    accessibilityState={{ selected, checked: selected }}
                    aria-checked={selected}
                    className={`min-h-[44px] justify-center rounded-full border px-4 ${selected ? 'border-brand-action bg-brand-action' : 'border-line-strong bg-surface'}`}
                  >
                    <Text className={selected ? 'font-semibold text-on-brand' : 'text-ink'}>{t(`cart.methods.${m.code}`)}</Text>
                  </Pressable>
                )
              })}
            </View>
          </View>
        )}
        <Button onPress={checkout} loading={placing} disabled={!method}>
          {placing ? t('cart.placingOrder') : `${t('cart.placeOrder')} · ${formatMoney(cartStore.total())}`}
        </Button>
      </View>
    </Screen>
  )
}
