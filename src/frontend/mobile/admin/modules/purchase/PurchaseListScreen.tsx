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
import { PO_STATUSES, purchaseApi, type PurchaseOrder } from '@/modules/purchase/api'

/** Purchase orders: filtered by status, order days / period and PO number or supplier, all by the server. */
export default function PurchaseListScreen() {
  const { t } = useTranslation()
  const router = useRouter()
  const params = useLocalSearchParams<{ status?: string }>()
  const filters = useListFilters({ status: PO_STATUSES.includes(params.status as never) ? params.status : '' })
  const query = usePagedList(['po', 'list', filters.params], (page) => purchaseApi.list(filters.params, page))

  return (
    <PagedListView<PurchaseOrder>
      query={query}
      keyOf={(o) => o.id}
      permission="purchase_order:read"
      emptyTitle={t('po.empty')}
      header={
        <ListFilters
          filters={filters}
          searchLabel={t('po.searchLabel')}
          statuses={PO_STATUSES.map((s) => ({ value: s, label: t(`status.po.${s}`) }))}
        />
      }
      renderItem={(o) => (
        <Card
          onPress={() => router.push(`/purchase/${o.id}`)}
          accessibilityLabel={`${o.poNumber}, ${o.supplier?.name ?? ''}, ${formatOrderMoney(o.totalAmount, o.currency)}, ${t(`status.po.${o.status}`, { defaultValue: o.status })}`}
        >
          <View className="flex-row items-center justify-between gap-2">
            <Text className="text-base font-semibold text-ink">{o.poNumber}</Text>
            <StatusBadge domain="po" status={o.status} />
          </View>
          <View className="mt-1 flex-row justify-between gap-2">
            <Text className="flex-1 text-sm text-ink-secondary">{o.supplier?.name ?? ''}</Text>
            <Text className="text-sm font-semibold text-ink">{formatOrderMoney(o.totalAmount, o.currency)}</Text>
          </View>
          <Text className="mt-1 text-xs text-ink-muted">{formatDate(o.orderDate)}</Text>
        </Card>
      )}
    />
  )
}
