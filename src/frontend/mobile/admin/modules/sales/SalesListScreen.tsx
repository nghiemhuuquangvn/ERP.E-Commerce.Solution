import { Text, View } from 'react-native'
import { useRouter, useLocalSearchParams } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { Card } from '@/components/ui'
import { ListFilters } from '@/components/ListFilters'
import { StatusBadge } from '@/components/StatusBadge'
import { PagedListView } from '@/components/PagedListView'
import { useListFilters } from '@/hooks/useListFilters'
import { formatOrderMoney } from '@/lib/money'
import { formatDate } from '@/lib/dates'
import { usePagedList } from '@/modules/usePagedList'
import { SO_STATUSES, salesApi, type SalesOrderSummary } from '@/modules/sales/api'

/** Sales orders: filtered by status, order days / period and order number or customer, all by the server. */
export default function SalesListScreen() {
  const { t } = useTranslation()
  const router = useRouter()
  const params = useLocalSearchParams<{ status?: string }>()
  const filters = useListFilters({ status: SO_STATUSES.includes(params.status as never) ? params.status : '' })
  const query = usePagedList(['so', 'list', filters.params], (page) => salesApi.list(filters.params, page))

  return (
    <PagedListView<SalesOrderSummary>
      query={query}
      keyOf={(o) => o.orderId}
      permission="sales_order:read"
      emptyTitle={t('so.empty')}
      header={
        <ListFilters
          filters={filters}
          searchLabel={t('so.searchLabel')}
          statuses={SO_STATUSES.map((s) => ({ value: s, label: t(`status.so.${s}`) }))}
        />
      }
      renderItem={(o) => (
        <Card
          onPress={() => router.push(`/sales/${o.orderId}`)}
          accessibilityLabel={`${o.orderNo}, ${o.customerName}, ${formatOrderMoney(o.totalAmount)}, ${t(`status.so.${o.status}`, { defaultValue: o.status })}`}
        >
          <View className="flex-row items-center justify-between gap-2">
            <Text className="text-base font-semibold text-ink">{o.orderNo}</Text>
            <StatusBadge domain="so" status={o.status} />
          </View>
          <View className="mt-1 flex-row justify-between gap-2">
            <Text className="flex-1 text-sm text-ink-secondary">{o.customerName}</Text>
            <Text className="text-sm font-semibold text-ink">{formatOrderMoney(o.totalAmount)}</Text>
          </View>
          <Text className="mt-1 text-xs text-ink-muted">{formatDate(o.orderDate)}</Text>
          {o.isAnomalous ? <Text className="mt-1 text-xs text-warning">{t('so.flagged')}</Text> : null}
        </Card>
      )}
    />
  )
}
