import { Text, View } from 'react-native'
import { useRouter } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { Card } from '@/components/ui'
import { ListFilters } from '@/components/ListFilters'
import { PagedListView } from '@/components/PagedListView'
import { StatusBadge } from '@/components/StatusBadge'
import { useListFilters } from '@/hooks/useListFilters'
import { formatDate } from '@/lib/dates'
import { formatOrderMoney } from '@/lib/money'
import { usePagedList } from '@/modules/usePagedList'
import { QUOTATION_STATUSES, salesExtrasApi, type CommissionRecord, type QuotationSummary } from '@/modules/sales/api'

/** Quotations: status, days / period the quotation was made, quotation number or customer. */
export function QuotationsScreen() {
  const { t } = useTranslation()
  const router = useRouter()
  const filters = useListFilters()
  const query = usePagedList(['so', 'quotations', filters.params], (page) => salesExtrasApi.quotations(filters.params, page))
  return (
    <PagedListView<QuotationSummary>
      query={query}
      keyOf={(q) => q.quotationId}
      permission="sales_order:read"
      emptyTitle={t('lists.quotations.empty')}
      header={<ListFilters filters={filters} searchLabel={t('lists.quotations.search')} statuses={QUOTATION_STATUSES.map((s) => ({ value: s, label: t(`status.quotation.${s}`) }))} />}
      renderItem={(q) => (
        <Card onPress={() => router.push(`/sales/quotation/${q.quotationId}`)} accessibilityLabel={`${q.quotationNo}, ${q.customerName}, ${formatOrderMoney(q.totalAmount)}, ${t(`status.quotation.${q.status}`, { defaultValue: q.status })}`}>
          <View className="flex-row items-center justify-between gap-2">
            <Text className="text-base font-semibold text-ink">{q.quotationNo}</Text>
            <StatusBadge domain="quotation" status={q.status} />
          </View>
          <View className="mt-1 flex-row justify-between gap-2">
            <Text className="flex-1 text-sm text-ink-secondary">{q.customerName}</Text>
            <Text className="text-sm font-semibold text-ink">{formatOrderMoney(q.totalAmount)}</Text>
          </View>
          <Text className="mt-1 text-xs text-ink-muted">
            {q.quotationDate ? `${formatDate(q.quotationDate)} · ` : ''}{t('lists.quotations.validUntil', { date: formatDate(q.validUntil) })}
          </Text>
        </Card>
      )}
    />
  )
}

/** Sales commissions: the days / period they were calculated in. */
export function CommissionsScreen() {
  const { t } = useTranslation()
  const filters = useListFilters({ kind: 'month' })
  const query = usePagedList(['so', 'commissions', filters.params], (page) => salesExtrasApi.commissions(filters.params, page))
  return (
    <PagedListView<CommissionRecord>
      query={query}
      keyOf={(c) => c.id}
      permission="sales_order:read"
      emptyTitle={t('lists.commissions.empty')}
      header={<ListFilters filters={filters} noSearch searchLabel={t('lists.commissions.search')} />}
      renderItem={(c) => (
        <Card accessibilityLabel={`${formatOrderMoney(c.amount)}, ${t(`status.commission.${c.status}`, { defaultValue: c.status })}`}>
          <View className="flex-row items-center justify-between gap-2">
            <Text className="text-base font-semibold text-ink">{formatOrderMoney(c.amount)}</Text>
            <StatusBadge domain="commission" status={c.status} />
          </View>
          <Text className="mt-1 text-xs text-ink-muted">{t('lists.commissions.calculated', { date: formatDate(c.calculatedAt) })}</Text>
        </Card>
      )}
    />
  )
}
