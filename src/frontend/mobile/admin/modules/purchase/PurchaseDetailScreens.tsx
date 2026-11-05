import { RefreshControl, ScrollView, Text, View } from 'react-native'
import { Stack, useLocalSearchParams } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Badge } from '@/components/ui'
import { ApprovalBar, parseAmount, type ApprovalAction } from '@/components/ApprovalBar'
import { DetailHeader, DetailSection } from '@/components/DetailSection'
import { DetailRows } from '@/components/DetailRows'
import { DetailState } from '@/components/DetailState'
import { LineItemRow } from '@/components/LineItemRow'
import { StatusBadge } from '@/components/StatusBadge'
import { formatDate } from '@/lib/dates'
import { formatOrderMoney } from '@/lib/money'
import { useAuthStore } from '@/stores/auth.store'
import { invoiceActions, returnActions } from '@/modules/purchase/actions'
import { procurementApi, type RfqComparison, type SupplierInvoice, type VendorReturn } from '@/modules/purchase/procurementApi'

const useId = () => useLocalSearchParams<{ id: string }>().id

/** Supplier: contact, address, terms, ratings and its items. Read only. */
export function SupplierDetailScreen() {
  const { t } = useTranslation()
  const id = useId()
  const supplier = useQuery({ queryKey: ['po', 'supplier', id], queryFn: () => procurementApi.supplier(id) })
  const ratings = useQuery({ queryKey: ['po', 'supplier', id, 'ratings'], queryFn: () => procurementApi.supplierRatings(id) })
  const items = useQuery({ queryKey: ['po', 'supplier', id, 'items'], queryFn: () => procurementApi.supplierItems(id) })
  const refresh = () => { void supplier.refetch(); void ratings.refetch(); void items.refetch() }

  return (
    <View className="flex-1 bg-page">
      <Stack.Screen options={{ title: t('sup.title') }} />
      <DetailState query={supplier} permission="supplier:read">
        {(s) => {
          const a = s.address
          const address = [a?.street, a?.city, a?.state, a?.postalCode, a?.country].filter(Boolean).join(', ')
          return (
            <ScrollView contentContainerClassName="gap-3 p-4" refreshControl={<RefreshControl refreshing={supplier.isRefetching} onRefresh={refresh} />}>
              <DetailHeader title={s.name} badge={<Badge tone={s.isActive ? 'success' : 'neutral'} label={s.isActive ? t('lists.suppliers.active') : t('lists.suppliers.inactive')} />} />
              <DetailSection title={t('sup.details')}>
                <DetailRows
                  rows={[
                    { label: t('sup.code'), value: s.code },
                    { label: t('sup.contact'), value: s.contactPerson },
                    { label: t('sup.phone'), value: s.phone },
                    { label: t('sup.email'), value: s.email },
                    { label: t('sup.address'), value: address },
                    { label: t('sup.taxCode'), value: s.taxCode },
                    { label: t('sup.terms'), value: s.paymentTermDays ? t('sup.termsDays', { count: s.paymentTermDays }) : null },
                    { label: t('sup.currency'), value: s.currencyCode },
                  ]}
                />
              </DetailSection>
              {ratings.data ? (
                <DetailSection title={t('sup.ratings')}>
                  <Text className="text-sm text-ink">{ratings.data.totalRatings ? t('sup.average', { avg: ratings.data.averageRating.toFixed(1), count: ratings.data.totalRatings }) : t('sup.noRatings')}</Text>
                  {ratings.data.ratings.slice(0, 5).map((r) => (
                    <View key={r.id} accessible accessibilityLabel={`${r.rating} / 5${r.comment ? `, ${r.comment}` : ''}`} className="border-t border-line py-2">
                      <Text className="text-sm font-semibold text-ink">{r.rating} / 5</Text>
                      {r.comment ? <Text className="text-sm text-ink-secondary">{r.comment}</Text> : null}
                    </View>
                  ))}
                </DetailSection>
              ) : null}
              {items.data && items.data.length > 0 ? (
                <DetailSection title={t('sup.items', { count: items.data.length })}>
                  {items.data.map((i) => (
                    <LineItemRow
                      key={i.id}
                      name={i.supplierItemCode || i.productId.slice(0, 8)}
                      detail={`${t('sup.leadTime', { count: i.leadTimeDays })}${i.isPreferred ? ` · ${t('sup.preferred')}` : ''}`}
                      amount={formatOrderMoney(i.lastPurchasePrice, s.currencyCode ?? 'USD')}
                    />
                  ))}
                </DetailSection>
              ) : null}
            </ScrollView>
          )
        }}
      </DetailState>
    </View>
  )
}

/** RFQ: lines, who was invited, the quotations received with their totals. Read only. */
export function RfqDetailScreen() {
  const { t } = useTranslation()
  const id = useId()
  const rfq = useQuery({ queryKey: ['po', 'rfq', id], queryFn: () => procurementApi.rfq(id) })
  const comparison = useQuery({ queryKey: ['po', 'rfq', id, 'comparison'], queryFn: () => procurementApi.rfqComparison(id) })
  const refresh = () => { void rfq.refetch(); void comparison.refetch() }
  const lowest = lowestTotal(comparison.data)

  return (
    <View className="flex-1 bg-page">
      <Stack.Screen options={{ title: t('rfq.title') }} />
      <DetailState query={rfq} permission="rfq:read">
        {(r) => (
          <ScrollView contentContainerClassName="gap-3 p-4" refreshControl={<RefreshControl refreshing={rfq.isRefetching} onRefresh={refresh} />}>
            <DetailHeader title={r.rfqNumber} badge={<StatusBadge domain="rfq" status={r.status} />} />
            <DetailSection title={r.title}>
              <DetailRows
                rows={[
                  { label: t('rfq.neededBy'), value: r.neededBy ? formatDate(r.neededBy) : null },
                  { label: t('rfq.sentAt'), value: r.sentAt ? formatDate(r.sentAt) : null },
                  { label: t('rfq.created'), value: formatDate(r.createdAt) },
                  { label: t('rfq.notes'), value: r.notes },
                ]}
              />
            </DetailSection>
            <DetailSection title={t('rfq.lines', { count: r.lines.length })}>
              {r.lines.map((l) => <LineItemRow key={l.id} name={l.productName} detail={l.productCode} amount={`${l.quantity} ${l.unit}`} />)}
            </DetailSection>
            <DetailSection title={t('rfq.suppliers', { count: r.invitations.length })}>
              {r.invitations.map((i) => <LineItemRow key={i.supplierId} name={i.supplierName} amount={t(`rfq.state.${i.state}`, { defaultValue: i.state })} />)}
            </DetailSection>
            {comparison.data && comparison.data.suppliers.length > 0 ? (
              <DetailSection title={t('rfq.quotes')}>
                {comparison.data.suppliers.map((q) => (
                  <LineItemRow
                    key={q.supplierId}
                    name={q.supplierName}
                    detail={[q.quoteNumber, t('sup.leadTime', { count: q.leadTimeDays }), q.expired ? t('rfq.expired') : null, q.isWinner ? t('rfq.winner') : null, q.total === lowest ? t('rfq.lowest') : null].filter(Boolean).join(' · ')}
                    amount={formatOrderMoney(q.total, q.currencyCode)}
                    strong={q.isWinner}
                  />
                ))}
              </DetailSection>
            ) : null}
          </ScrollView>
        )}
      </DetailState>
    </View>
  )
}

/** The smallest total among the quotations that are still valid (null when there is none). */
export function lowestTotal(c?: RfqComparison): number | null {
  const open = (c?.suppliers ?? []).filter((s) => !s.expired)
  return open.length ? Math.min(...open.map((s) => s.total)) : null
}

function invoiceActionList(i: SupplierInvoice, has: (p: string) => boolean, t: (k: string, o?: Record<string, unknown>) => string): ApprovalAction[] {
  const all: Record<string, ApprovalAction> = {
    approve: {
      key: 'approve', label: t('inv.approve'), variant: 'primary', confirmTitle: t('inv.confirmApprove', { number: i.invoiceNumber }),
      run: () => procurementApi.approveInvoice(i.id), successMessage: t('inv.approved', { number: i.invoiceNumber }),
    },
    approveVariance: {
      key: 'approveVariance', label: t('inv.approveVariance'), variant: 'primary', confirmTitle: t('inv.confirmVariance', { number: i.invoiceNumber }),
      reasonLabel: t('common.reason'), run: (reason) => procurementApi.approveInvoice(i.id, reason), successMessage: t('inv.approved', { number: i.invoiceNumber }),
    },
    reject: {
      key: 'reject', label: t('inv.reject'), variant: 'danger', confirmTitle: t('inv.rejectTitle', { number: i.invoiceNumber }),
      reasonLabel: t('common.reason'), run: (reason) => procurementApi.rejectInvoice(i.id, reason ?? ''), successMessage: t('inv.rejected', { number: i.invoiceNumber }),
    },
  }
  return invoiceActions(i.status, has).map((k) => all[k])
}

/** Supplier invoice: totals, why it is on hold, the three-way match, and approve / reject. */
export function InvoiceDetailScreen() {
  const { t } = useTranslation()
  const id = useId()
  const qc = useQueryClient()
  const has = useAuthStore((s) => s.hasPermission)
  const query = useQuery({ queryKey: ['po', 'invoice', id], queryFn: () => procurementApi.invoice(id) })
  const done = () => { void qc.invalidateQueries({ queryKey: ['po'] }) }

  return (
    <View className="flex-1 bg-page">
      <Stack.Screen options={{ title: t('inv.title') }} />
      <DetailState query={query} permission="supplier_invoice:read">
        {(i) => (
          <>
            <ScrollView contentContainerClassName="gap-3 p-4" refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => void query.refetch()} />}>
              <DetailHeader title={i.invoiceNumber} badge={<StatusBadge domain="invoice" status={i.status} />} />
              {i.holdReasons ? <Alert tone="warning">{i.holdReasons}</Alert> : null}
              {i.approvedAt ? <Alert tone="success">{t('inv.approvedBy', { by: i.approvedBy ?? '', date: formatDate(i.approvedAt) })}{i.approvalReason ? ` ${i.approvalReason}` : ''}</Alert> : null}
              {i.rejectionReason ? <Alert tone="danger">{i.rejectionReason}</Alert> : null}
              <DetailSection title={t('inv.summary')}>
                <DetailRows
                  rows={[
                    { label: t('inv.supplier'), value: i.supplierName },
                    { label: t('inv.po'), value: i.poNumber },
                    { label: t('inv.invoiceDate'), value: formatDate(i.invoiceDate) },
                    { label: t('inv.dueDate'), value: i.dueDate ? formatDate(i.dueDate) : null },
                    { label: t('inv.net'), value: formatOrderMoney(i.netAmount, i.currencyCode) },
                    { label: t('inv.tax'), value: formatOrderMoney(i.taxAmount, i.currencyCode) },
                    { label: t('inv.total'), value: formatOrderMoney(i.totalAmount, i.currencyCode) },
                    { label: t('inv.received'), value: formatOrderMoney(i.receivedValue, i.currencyCode) },
                  ]}
                />
              </DetailSection>
              {i.matchLines.length > 0 ? (
                <DetailSection title={t('inv.matchTitle')}>
                  {i.matchLines.map((m) => (
                    <LineItemRow
                      key={m.productCode}
                      name={m.productCode}
                      detail={t('inv.matchDetail', { ordered: m.ordered, received: m.received, returned: m.returned, invoiced: m.invoiced, variance: m.priceVariancePercent.toFixed(1) })}
                      amount={t(`inv.verdict.${m.verdict}`, { defaultValue: m.verdict })}
                    />
                  ))}
                </DetailSection>
              ) : null}
              <DetailSection title={t('inv.lines', { count: i.lines.length })}>
                {i.lines.map((l) => <LineItemRow key={l.productCode} name={l.productCode} detail={`${l.quantity} × ${formatOrderMoney(l.unitPrice, i.currencyCode)}`} amount={formatOrderMoney(l.quantity * l.unitPrice, i.currencyCode)} />)}
              </DetailSection>
            </ScrollView>
            <ApprovalBar actions={invoiceActionList(i, has, t as never)} onDone={done} />
          </>
        )}
      </DetailState>
    </View>
  )
}

function returnActionList(r: VendorReturn, has: (p: string) => boolean, t: (k: string, o?: Record<string, unknown>) => string): ApprovalAction[] {
  const all: Record<string, ApprovalAction> = {
    ship: { key: 'ship', label: t('ret.ship'), variant: 'primary', confirmTitle: t('ret.confirmShip', { number: r.returnNumber }), run: () => procurementApi.shipReturn(r.id), successMessage: t('ret.shipped', { number: r.returnNumber }) },
    cancel: { key: 'cancel', label: t('ret.cancel'), variant: 'outline', confirmTitle: t('ret.cancelTitle', { number: r.returnNumber }), run: () => procurementApi.cancelReturn(r.id), successMessage: t('ret.cancelled', { number: r.returnNumber }) },
    credit: {
      key: 'credit', label: t('ret.credit'), variant: 'primary', confirmTitle: t('ret.creditTitle', { number: r.returnNumber }),
      fields: [
        { key: 'creditNoteNumber', label: t('ret.creditNote'), required: true },
        { key: 'creditAmount', label: t('ret.creditAmount'), kind: 'number', required: true, initial: String(r.value) },
      ],
      run: (_reason, v) => procurementApi.creditReturn(r.id, v?.creditNoteNumber ?? '', parseAmount(v?.creditAmount ?? '') ?? 0),
      successMessage: t('ret.credited', { number: r.returnNumber }),
    },
  }
  return returnActions(r.status, has).map((k) => all[k])
}

/** Vendor return: reason, lines, credit note, and ship / cancel / record credit. */
export function ReturnDetailScreen() {
  const { t } = useTranslation()
  const id = useId()
  const qc = useQueryClient()
  const has = useAuthStore((s) => s.hasPermission)
  const query = useQuery({ queryKey: ['po', 'return', id], queryFn: () => procurementApi.vendorReturn(id) })
  const done = () => { void qc.invalidateQueries({ queryKey: ['po'] }) }

  return (
    <View className="flex-1 bg-page">
      <Stack.Screen options={{ title: t('ret.title') }} />
      <DetailState query={query} permission="vendor_return:read">
        {(r) => (
          <>
            <ScrollView contentContainerClassName="gap-3 p-4" refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => void query.refetch()} />}>
              <DetailHeader title={r.returnNumber} badge={<StatusBadge domain="vendorReturn" status={r.status} />} />
              <DetailSection title={t('ret.summary')}>
                <DetailRows
                  rows={[
                    { label: t('inv.supplier'), value: r.supplierName },
                    { label: t('inv.po'), value: r.poNumber },
                    { label: t('ret.reason'), value: r.reason },
                    { label: t('ret.value'), value: formatOrderMoney(r.value, r.currencyCode) },
                    { label: t('ret.created'), value: formatDate(r.createdAt) },
                    { label: t('ret.shippedAt'), value: r.shippedAt ? formatDate(r.shippedAt) : null },
                    { label: t('ret.creditNote'), value: r.creditNoteNumber },
                    { label: t('ret.creditAmount'), value: r.creditedAt ? formatOrderMoney(r.creditAmount, r.currencyCode) : null },
                    { label: t('ret.creditedAt'), value: r.creditedAt ? formatDate(r.creditedAt) : null },
                  ]}
                />
              </DetailSection>
              <DetailSection title={t('inv.lines', { count: r.lines.length })}>
                {r.lines.map((l) => <LineItemRow key={l.productCode} name={l.productName} detail={`${l.productCode} · ${l.quantity} × ${formatOrderMoney(l.unitPrice, r.currencyCode)}`} amount={formatOrderMoney(l.quantity * l.unitPrice, r.currencyCode)} />)}
              </DetailSection>
            </ScrollView>
            <ApprovalBar actions={returnActionList(r, has, t as never)} onDone={done} />
          </>
        )}
      </DetailState>
    </View>
  )
}
