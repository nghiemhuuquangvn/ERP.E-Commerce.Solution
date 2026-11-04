import { Text, View } from 'react-native'
import { useRouter } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { Badge, Card } from '@/components/ui'
import { ListFilters } from '@/components/ListFilters'
import { PagedListView } from '@/components/PagedListView'
import { StatusBadge } from '@/components/StatusBadge'
import { useListFilters } from '@/hooks/useListFilters'
import { formatDate } from '@/lib/dates'
import { formatOrderMoney } from '@/lib/money'
import { usePagedList } from '@/modules/usePagedList'
import {
  procurementApi, RFQ_STATUSES, SUPPLIER_INVOICE_STATUSES, VENDOR_RETURN_STATUSES,
  type RfqSummary, type Supplier, type SupplierInvoiceSummary, type VendorReturnSummary,
} from '@/modules/purchase/procurementApi'

/** Suppliers: search by code, name, e-mail or tax code; active / inactive. */
export function SuppliersScreen() {
  const { t } = useTranslation()
  const router = useRouter()
  const filters = useListFilters({ status: 'active' })
  const query = usePagedList(['po', 'suppliers', filters.params], (page) => procurementApi.suppliers(filters.params, page))
  return (
    <PagedListView<Supplier>
      query={query}
      keyOf={(s) => s.id}
      permission="supplier:read"
      emptyTitle={t('lists.suppliers.empty')}
      header={
        <ListFilters
          filters={filters}
          noDates
          searchLabel={t('lists.suppliers.search')}
          statuses={[{ value: 'active', label: t('lists.suppliers.active') }, { value: 'inactive', label: t('lists.suppliers.inactive') }]}
        />
      }
      renderItem={(s) => (
        <Card onPress={() => router.push(`/purchase/supplier/${s.id}`)} accessibilityLabel={`${s.name}, ${s.code}`}>
          <View className="flex-row items-center justify-between gap-2">
            <Text className="flex-1 text-base font-semibold text-ink">{s.name}</Text>
            <Badge tone={s.isActive ? 'success' : 'neutral'} label={s.isActive ? t('lists.suppliers.active') : t('lists.suppliers.inactive')} />
          </View>
          <Text className="mt-1 text-sm text-ink-secondary">{s.code}{s.contactPerson ? ` · ${s.contactPerson}` : ''}</Text>
          {s.phone || s.email ? <Text className="mt-1 text-xs text-ink-muted">{[s.phone, s.email].filter(Boolean).join(' · ')}</Text> : null}
          {s.paymentTermDays ? <Text className="mt-1 text-xs text-ink-muted">{t('lists.suppliers.terms', { count: s.paymentTermDays })}</Text> : null}
        </Card>
      )}
    />
  )
}

/** Supplier invoices (view only; matching and approval stay on the web): status, invoice days / period, invoice or PO number. */
export function SupplierInvoicesScreen() {
  const { t } = useTranslation()
  const router = useRouter()
  const filters = useListFilters()
  const query = usePagedList(['po', 'invoices', filters.params], (page) => procurementApi.invoices(filters.params, page))
  return (
    <PagedListView<SupplierInvoiceSummary>
      query={query}
      keyOf={(i) => i.id}
      permission="supplier_invoice:read"
      emptyTitle={t('lists.invoices.empty')}
      header={
        <ListFilters filters={filters} searchLabel={t('lists.invoices.search')} statuses={SUPPLIER_INVOICE_STATUSES.map((s) => ({ value: s, label: t(`status.invoice.${s}`) }))} />
      }
      renderItem={(i) => (
        <Card onPress={() => router.push(`/purchase/invoice/${i.id}`)} accessibilityLabel={`${i.invoiceNumber}, ${i.supplierName}, ${formatOrderMoney(i.totalAmount, i.currencyCode)}, ${t(`status.invoice.${i.status}`, { defaultValue: i.status })}`}>
          <View className="flex-row items-center justify-between gap-2">
            <Text className="text-base font-semibold text-ink">{i.invoiceNumber}</Text>
            <StatusBadge domain="invoice" status={i.status} />
          </View>
          <View className="mt-1 flex-row justify-between gap-2">
            <Text className="flex-1 text-sm text-ink-secondary">{i.supplierName} · {i.poNumber}</Text>
            <Text className="text-sm font-semibold text-ink">{formatOrderMoney(i.totalAmount, i.currencyCode)}</Text>
          </View>
          <Text className="mt-1 text-xs text-ink-muted">
            {formatDate(i.invoiceDate)}{i.dueDate ? ` · ${t('lists.invoices.due', { date: formatDate(i.dueDate) })}` : ''}
          </Text>
          {i.holdReasons ? <Text className="mt-1 text-xs text-warning">{i.holdReasons}</Text> : null}
        </Card>
      )}
    />
  )
}

/** Returns to vendors: status, days / period, return or PO number. */
export function VendorReturnsScreen() {
  const { t } = useTranslation()
  const router = useRouter()
  const filters = useListFilters()
  const query = usePagedList(['po', 'returns', filters.params], (page) => procurementApi.returns(filters.params, page))
  return (
    <PagedListView<VendorReturnSummary>
      query={query}
      keyOf={(r) => r.id}
      permission="vendor_return:read"
      emptyTitle={t('lists.returns.empty')}
      header={<ListFilters filters={filters} searchLabel={t('lists.returns.search')} statuses={VENDOR_RETURN_STATUSES.map((s) => ({ value: s, label: t(`status.vendorReturn.${s}`) }))} />}
      renderItem={(r) => (
        <Card onPress={() => router.push(`/purchase/return/${r.id}`)} accessibilityLabel={`${r.returnNumber}, ${r.supplierName}, ${formatOrderMoney(r.value, r.currencyCode)}, ${t(`status.vendorReturn.${r.status}`, { defaultValue: r.status })}`}>
          <View className="flex-row items-center justify-between gap-2">
            <Text className="text-base font-semibold text-ink">{r.returnNumber}</Text>
            <StatusBadge domain="vendorReturn" status={r.status} />
          </View>
          <View className="mt-1 flex-row justify-between gap-2">
            <Text className="flex-1 text-sm text-ink-secondary">{r.supplierName} · {r.poNumber}</Text>
            <Text className="text-sm font-semibold text-ink">{formatOrderMoney(r.value, r.currencyCode)}</Text>
          </View>
          <Text className="mt-1 text-xs text-ink-muted">{formatDate(r.createdAt)}</Text>
        </Card>
      )}
    />
  )
}

/** Requests for quotation: status, days / period, RFQ number or title. */
export function RfqsScreen() {
  const { t } = useTranslation()
  const router = useRouter()
  const filters = useListFilters()
  const query = usePagedList(['po', 'rfqs', filters.params], (page) => procurementApi.rfqs(filters.params, page))
  return (
    <PagedListView<RfqSummary>
      query={query}
      keyOf={(r) => r.id}
      permission="rfq:read"
      emptyTitle={t('lists.rfqs.empty')}
      header={<ListFilters filters={filters} searchLabel={t('lists.rfqs.search')} statuses={RFQ_STATUSES.map((s) => ({ value: s, label: t(`status.rfq.${s}`) }))} />}
      renderItem={(r) => (
        <Card onPress={() => router.push(`/purchase/rfq/${r.id}`)} accessibilityLabel={`${r.rfqNumber}, ${r.title}, ${t(`status.rfq.${r.status}`, { defaultValue: r.status })}`}>
          <View className="flex-row items-center justify-between gap-2">
            <Text className="text-base font-semibold text-ink">{r.rfqNumber}</Text>
            <StatusBadge domain="rfq" status={r.status} />
          </View>
          <Text className="mt-1 text-sm text-ink-secondary">{r.title}</Text>
          <Text className="mt-1 text-xs text-ink-muted">
            {t('lists.rfqs.counts', { lines: r.lines, invited: r.invited, quoted: r.quoted })}
            {r.neededBy ? ` · ${t('lists.rfqs.needed', { date: formatDate(r.neededBy) })}` : ''}
          </Text>
          <Text className="mt-1 text-xs text-ink-muted">{formatDate(r.createdAt)}</Text>
        </Card>
      )}
    />
  )
}
