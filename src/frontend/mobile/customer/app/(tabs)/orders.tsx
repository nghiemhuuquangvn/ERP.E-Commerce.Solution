import { FlatList, Text, View } from 'react-native'
import { useRouter } from 'expo-router'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { format } from 'date-fns'
import { Package } from 'lucide-react-native'
import { colors, icon } from '@/theme/theme'
import { apiClient } from '@/api/client'
import { formatMoney } from '@/lib/money'
import { Alert, Badge, Button, Card, EmptyState, Screen, Skeleton } from '@/components/ui'

// GET /v1/orders/my returns SalesOrderSummaryDto: no line items (those are on GET /v1/orders/{id}, the order screen).
interface Order {
  orderId: string
  orderNo: string
  status: string
  totalAmount: number
  orderDate: string
}

const STATUS_TONE: Record<string, 'neutral' | 'info' | 'warning' | 'brand' | 'success' | 'danger'> = {
  Draft: 'neutral',
  Confirmed: 'info',
  Processing: 'warning',
  Shipped: 'brand',
  Delivered: 'success',
  Cancelled: 'danger',
}

export default function OrdersScreen() {
  const router = useRouter()
  const { t } = useTranslation()

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['my-orders'],
    queryFn: async () => {
      const { data } = await apiClient.get('/v1/orders/my')
      return data as Order[]
    },
  })

  return (
    <Screen scroll={false}>
      {isLoading ? (
        <View accessibilityLabel={t('orders.loading')} accessibilityLiveRegion="polite" className="gap-3 p-4">
          {[0, 1, 2].map((i) => <Skeleton key={i} className="h-24 w-full" />)}
        </View>
      ) : isError ? (
        <View className="gap-3 p-4">
          <Alert tone="danger">{t('common.loadError')}</Alert>
          <Button variant="outline" onPress={() => refetch()}>{t('common.retry')}</Button>
        </View>
      ) : (
        <FlatList
          data={data ?? []}
          keyExtractor={(o) => o.orderId}
          contentContainerClassName="p-4 gap-3"
          renderItem={({ item }) => {
            const date = format(new Date(item.orderDate), 'dd MMM yyyy')
            const status = t(`orders.status.${item.status}`, { defaultValue: item.status })
            return (
              <Card onPress={() => router.push(`/order/${item.orderId}`)} accessibilityLabel={`${item.orderNo}, ${status}, ${formatMoney(item.totalAmount)}, ${date}`}>
                <View className="mb-2 flex-row items-start justify-between">
                  <Text className="font-semibold text-ink">{item.orderNo}</Text>
                  <Badge tone={STATUS_TONE[item.status] ?? 'neutral'} label={status} />
                </View>
                <Text className="mb-1 text-sm text-ink-secondary">{date}</Text>
                <Text className="font-bold text-brand-action">{formatMoney(item.totalAmount)}</Text>
              </Card>
            )
          }}
          ListEmptyComponent={
            <EmptyState
              icon={<Package size={40} strokeWidth={icon.stroke} color={colors.inkMuted} />}
              title={t('orders.empty')}
              hint={t('orders.emptyHint')}
              action={<Button onPress={() => router.push('/(tabs)/search')}>{t('cart.browseProducts')}</Button>}
            />
          }
        />
      )}
    </Screen>
  )
}
