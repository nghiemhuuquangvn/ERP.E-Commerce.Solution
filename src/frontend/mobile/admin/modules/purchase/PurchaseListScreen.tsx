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
import { purchaseApi, type PoStatus, type PurchaseOrder } from '@/modules/purchase/api'

type Filter = 'ALL' | 'SUBMITTED' | 'PENDING_FINANCE_APPROVAL' | 'APPROVED'
const FILTERS: Filter[] = ['ALL', 'SUBMITTED', 'PENDING_FINANCE_APPROVAL', 'APPROVED']

export default function PurchaseListScreen() {
  const { t } = useTranslation()
  const router = useRouter()
  const params = useLocalSearchParams<{ status?: string }>()
  const [filter, setFilter] = useState<Filter>(FILTERS.includes(params.status as Filter) ? (params.status as Filter) : 'ALL')
  const [search, setSearch] = useState('')
  const status = filter === 'ALL' ? undefined : (filter as PoStatus)
  const query = usePagedList(['po', 'list', filter], (page) => purchaseApi.list(status, page))

  const chips: Chip<Filter>[] = FILTERS.map((f) => ({ value: f, label: f === 'ALL' ? t('common.all') : t(`status.po.${f}`) }))
  // The server has no text search for purchase orders: this narrows the rows already loaded.
  const needle = search.trim().toLowerCase()
  const shown = needle ? query.items.filter((o) => `${o.poNumber} ${o.supplier?.name ?? ''}`.toLowerCase().includes(needle)) : query.items

  return (
    <PagedListView<PurchaseOrder>
      query={{ ...query, items: shown }}
      keyOf={(o) => o.id}
      permission="purchase_order:read"
      emptyTitle={t('po.empty')}
      header={
        <>
          <SearchField value={search} onChangeText={setSearch} placeholder={t('common.search')} label={t('po.searchLabel')} />
          <FilterChips chips={chips} value={filter} onChange={setFilter} />
        </>
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
