import { useState } from 'react'
import { Text, View } from 'react-native'
import { useRouter, useLocalSearchParams } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { Card } from '@/components/ui'
import { FilterChips, type Chip } from '@/components/FilterChips'
import { SearchField } from '@/components/SearchField'
import { StatusBadge } from '@/components/StatusBadge'
import { PagedListView } from '@/components/PagedListView'
import { formatOrderMoney } from '@/lib/money'
import { formatDate } from '@/lib/dates'
import { usePagedList } from '@/modules/usePagedList'
import { salesApi, type SalesOrderSummary, type SoStatus } from '@/modules/sales/api'

type Filter = 'ALL' | 'Submitted' | 'Approved' | 'Shipped'
const FILTERS: Filter[] = ['ALL', 'Submitted', 'Approved', 'Shipped']

export default function SalesListScreen() {
  const { t } = useTranslation()
  const router = useRouter()
  const params = useLocalSearchParams<{ status?: string }>()
  const [filter, setFilter] = useState<Filter>(FILTERS.includes(params.status as Filter) ? (params.status as Filter) : 'ALL')
  const [search, setSearch] = useState('')
  const status = filter === 'ALL' ? undefined : (filter as SoStatus)
  const query = usePagedList(['so', 'list', filter], (page) => salesApi.list(status, page))

  const chips: Chip<Filter>[] = FILTERS.map((f) => ({ value: f, label: f === 'ALL' ? t('common.all') : t(`status.so.${f}`) }))
  // The server has no text search for orders: this narrows the rows already loaded.
  const needle = search.trim().toLowerCase()
  const shown = needle ? query.items.filter((o) => `${o.orderNo} ${o.customerName}`.toLowerCase().includes(needle)) : query.items

  return (
    <PagedListView<SalesOrderSummary>
      query={{ ...query, items: shown }}
      keyOf={(o) => o.orderId}
      permission="sales_order:read"
      emptyTitle={t('so.empty')}
      header={
        <>
          <SearchField value={search} onChangeText={setSearch} placeholder={t('common.search')} label={t('so.searchLabel')} />
          <FilterChips chips={chips} value={filter} onChange={setFilter} />
        </>
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
