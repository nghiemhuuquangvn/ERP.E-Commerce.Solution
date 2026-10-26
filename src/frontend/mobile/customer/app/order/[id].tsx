import { useEffect, useState } from 'react'
import { ActivityIndicator, AppState, Linking, Text, TouchableOpacity, View } from 'react-native'
import { useLocalSearchParams, useRouter } from 'expo-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { apiClient } from '@/api/client'
import { fetchPaymentStatus, startCheckout } from '@/api/payments'
import type { Order } from '@/api/payments'

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
    return <View className="flex-1 items-center justify-center bg-white"><ActivityIndicator /></View>
  }
  if (!order) {
    return <View className="flex-1 items-center justify-center bg-white"><Text className="text-gray-400">{t('order.notFound')}</Text></View>
  }

  const money = (n: number, currency = 'USD') =>
    new Intl.NumberFormat(currency === 'VND' ? 'vi-VN' : 'en-US', { style: 'currency', currency, maximumFractionDigits: currency === 'VND' ? 0 : 2 }).format(n)
  const isCod = order.paymentMethod === 'COD'
  const waiting = payment?.status === 'Pending' && payment.provider !== 'COD'

  return (
    <View className="flex-1 bg-white p-4">
      <Text className="text-xl font-bold text-gray-900">{order.orderNo}</Text>
      <Text className="mb-4 text-gray-500">{order.status}</Text>

      {order.lineItems.map((l) => (
        <View key={l.productId} className="mb-1 flex-row justify-between">
          <Text className="text-sm text-gray-700">{l.productName} ×{l.quantity}</Text>
          <Text className="text-sm text-gray-700">{money(l.unitPrice * l.quantity, payment?.currency)}</Text>
        </View>
      ))}
      <View className="mt-2 flex-row justify-between border-t border-gray-100 pt-2">
        <Text className="font-semibold">{t('cart.total')}</Text>
        <Text className="font-semibold">{money(order.totalAmount, payment?.currency)}</Text>
      </View>

      <View className="mt-6 rounded-xl border border-gray-100 bg-gray-50 p-4">
        <Text className="text-xs uppercase text-gray-400">{t('order.payment')}</Text>
        {isCod ? (
          <Text className="mt-1 text-gray-700">{t('order.payOnDelivery')}</Text>
        ) : payment ? (
          <Text className="mt-1 font-medium text-gray-800">{payment.provider ?? ''} · {t(`order.status.${payment.status}`)}</Text>
        ) : (
          <Text className="mt-1 text-gray-400">{t('order.preparing')}</Text>
        )}
        {payment?.status === 'Failed' && <Text className="mt-1 text-sm text-red-600">{t('order.failedDetail')}</Text>}
        {waiting && (
          <TouchableOpacity onPress={pay} disabled={starting} className="mt-3 items-center rounded-xl bg-brand-500 py-3">
            <Text className="font-bold text-white">{starting ? t('order.opening') : t('order.payNow')}</Text>
          </TouchableOpacity>
        )}
        {!!error && <Text className="mt-2 text-sm text-red-600">{error}</Text>}
      </View>

      <TouchableOpacity onPress={() => router.replace('/(tabs)/orders')} className="mt-6 items-center">
        <Text className="text-brand-500">{t('order.viewOrders')}</Text>
      </TouchableOpacity>
    </View>
  )
}
