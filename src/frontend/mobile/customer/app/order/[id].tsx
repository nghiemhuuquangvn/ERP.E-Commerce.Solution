import { useEffect, useState } from 'react'
import { AppState, Linking, Text, View } from 'react-native'
import { useLocalSearchParams, useRouter } from 'expo-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { apiClient } from '@/api/client'
import { fetchPaymentStatus, startCheckout } from '@/api/payments'
import type { Order } from '@/api/payments'
import { formatOrderMoney } from '@/lib/money'
import { Alert, Button, Card, EmptyState, Screen, Spinner } from '@/components/ui'

const FINAL = ['Captured', 'Failed', 'Cancelled', 'Refunded', 'PartiallyRefunded']

/** One order with its payment. While the payment is pending it is polled, and again whenever the app comes back to the foreground (the customer returns from the gateway's page). */
export default function OrderScreen() {
  const { id } = useLocalSearchParams<{ id: string }>()
  const router = useRouter()
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [starting, setStarting] = useState(false)
  const [error, setError] = useState('')

  const { data: order, isLoading } = useQuery({
    queryKey: ['order', id],
    queryFn: async () => (await apiClient.get<Order>(`/v1/orders/${id}`)).data,
  })

  const { data: payment } = useQuery({
    queryKey: ['order-payment', id],
    queryFn: () => fetchPaymentStatus(id!).catch(() => null),
    refetchInterval: (query) => {
      const status = query.state.data?.status
      return status && FINAL.includes(status) ? false : 3000
    },
  })

  useEffect(() => {
    const sub = AppState.addEventListener('change', (state) => {
      if (state === 'active') queryClient.invalidateQueries({ queryKey: ['order-payment', id] })
    })
    return () => sub.remove()
  }, [id, queryClient])

  const pay = async () => {
    setStarting(true)
    setError('')
    try {
      const { paymentUrl } = await startCheckout(id!)
      await Linking.openURL(paymentUrl)
    } catch {
      setError(t('order.startFailed'))
    } finally {
      setStarting(false)
    }
  }

  if (isLoading) {
    return (
      <Screen scroll={false} edges={['top', 'bottom', 'left', 'right']}>
        <View className="flex-1 items-center justify-center"><Spinner label={t('common.loading')} /></View>
      </Screen>
    )
  }
  if (!order) {
    return (
      <Screen scroll={false} edges={['top', 'bottom', 'left', 'right']}>
        <EmptyState title={t('order.notFound')} action={<Button onPress={() => router.replace('/(tabs)/orders')}>{t('order.viewOrders')}</Button>} />
      </Screen>
    )
  }

  const money = (n: number) => formatOrderMoney(n, payment?.currency)
  const isCod = order.paymentMethod === 'COD'
  const waiting = payment?.status === 'Pending' && payment.provider !== 'COD'

  return (
    <Screen edges={['top', 'bottom', 'left', 'right']}>
      <Text accessibilityRole="header" className="text-xl font-bold text-ink">{order.orderNo}</Text>
      <Text className="mb-4 text-ink-secondary">{order.status}</Text>

      {order.lineItems.map((l) => (
        <View key={l.productId} className="mb-1 flex-row justify-between">
          <Text className="flex-1 text-sm text-ink">{l.productName} ×{l.quantity}</Text>
          <Text className="text-sm text-ink">{money(l.unitPrice * l.quantity)}</Text>
        </View>
      ))}
      <View className="mt-2 flex-row justify-between border-t border-line pt-2">
        <Text className="font-semibold text-ink">{t('cart.total')}</Text>
        <Text className="font-semibold text-ink">{money(order.totalAmount)}</Text>
      </View>

      <View className="mt-6">
        <Card>
          <Text accessibilityRole="header" className="text-sm text-ink-secondary">{t('order.payment')}</Text>
          {isCod ? (
            <Text className="mt-1 text-ink">{t('order.payOnDelivery')}</Text>
          ) : payment ? (
            <Text className="mt-1 font-medium text-ink">{payment.provider ?? ''} · {t(`order.status.${payment.status}`)}</Text>
          ) : (
            <Text className="mt-1 text-ink-secondary">{t('order.preparing')}</Text>
          )}
          {payment?.status === 'Failed' && <View className="mt-2"><Alert tone="danger">{t('order.failedDetail')}</Alert></View>}
          {waiting && (
            <View className="mt-3"><Button onPress={pay} loading={starting}>{starting ? t('order.opening') : t('order.payNow')}</Button></View>
          )}
          {!!error && <View className="mt-2"><Alert tone="danger">{error}</Alert></View>}
        </Card>
      </View>

      <View className="mt-6">
        <Button variant="ghost" onPress={() => router.replace('/(tabs)/orders')}>{t('order.viewOrders')}</Button>
      </View>
    </Screen>
  )
}
