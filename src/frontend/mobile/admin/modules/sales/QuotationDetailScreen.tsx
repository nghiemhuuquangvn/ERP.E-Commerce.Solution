import { RefreshControl, ScrollView, View } from 'react-native'
import { Stack, useLocalSearchParams, useRouter } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ApprovalBar, type ApprovalAction } from '@/components/ApprovalBar'
import { DetailHeader, DetailSection } from '@/components/DetailSection'
import { DetailRows } from '@/components/DetailRows'
import { DetailState } from '@/components/DetailState'
import { LineItemRow } from '@/components/LineItemRow'
import { StatusBadge } from '@/components/StatusBadge'
import { Button } from '@/components/ui'
import { formatDate } from '@/lib/dates'
import { formatOrderMoney } from '@/lib/money'
import { useAuthStore } from '@/stores/auth.store'
import { quotationActions, salesExtrasApi, type Quotation } from '@/modules/sales/api'

function actionList(q: Quotation, has: (p: string) => boolean, t: (k: string, o?: Record<string, unknown>) => string): ApprovalAction[] {
  const all: Record<string, ApprovalAction> = {
    send: { key: 'send', label: t('qt.send'), variant: 'primary', confirmTitle: t('qt.confirmSend', { number: q.quotationNo }), run: () => salesExtrasApi.sendQuotation(q.quotationId), successMessage: t('qt.sent', { number: q.quotationNo }) },
    accept: { key: 'accept', label: t('qt.accept'), variant: 'primary', confirmTitle: t('qt.confirmAccept', { number: q.quotationNo }), run: () => salesExtrasApi.acceptQuotation(q.quotationId), successMessage: t('qt.accepted', { number: q.quotationNo }) },
    reject: { key: 'reject', label: t('qt.reject'), variant: 'danger', confirmTitle: t('qt.confirmReject', { number: q.quotationNo }), run: () => salesExtrasApi.rejectQuotation(q.quotationId), successMessage: t('qt.rejected', { number: q.quotationNo }) },
    convert: {
      key: 'convert', label: t('qt.convert'), variant: 'primary', confirmTitle: t('qt.convertTitle', { number: q.quotationNo }),
      fields: [
        { key: 'street', label: t('qt.street'), required: true },
        { key: 'city', label: t('qt.city'), required: true },
        { key: 'state', label: t('qt.state'), required: true },
        { key: 'postalCode', label: t('qt.postalCode'), required: true },
        { key: 'country', label: t('qt.country'), required: true },
      ],
      run: (_reason, v) => salesExtrasApi.convertQuotation(q.quotationId, {
        street: v?.street ?? '', city: v?.city ?? '', state: v?.state ?? '', postalCode: v?.postalCode ?? '', country: v?.country ?? '',
      }),
      successMessage: t('qt.converted', { number: q.quotationNo }),
    },
  }
  return quotationActions(q.status, has).map((k) => all[k])
}

/** Quotation: customer, validity, lines, the order it became, and send / accept / reject / convert to an order. */
export default function QuotationDetailScreen() {
  const { t } = useTranslation()
  const { id } = useLocalSearchParams<{ id: string }>()
  const router = useRouter()
  const qc = useQueryClient()
  const has = useAuthStore((s) => s.hasPermission)
  const query = useQuery({ queryKey: ['so', 'quotation', id], queryFn: () => salesExtrasApi.quotation(id) })
  const done = () => { void qc.invalidateQueries({ queryKey: ['so'] }) }

  return (
    <View className="flex-1 bg-page">
      <Stack.Screen options={{ title: t('qt.title') }} />
      <DetailState query={query} permission="sales_order:read">
        {(q) => (
          <>
            <ScrollView contentContainerClassName="gap-3 p-4" refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => void query.refetch()} />}>
              <DetailHeader title={q.quotationNo} badge={<StatusBadge domain="quotation" status={q.status} />} />
              <DetailSection title={t('qt.summary')}>
                <DetailRows
                  rows={[
                    { label: t('qt.customer'), value: q.customerName },
                    { label: t('qt.email'), value: q.customerEmail },
                    { label: t('qt.validUntil'), value: formatDate(q.validUntil) },
                    { label: t('qt.total'), value: formatOrderMoney(q.totalAmount) },
                  ]}
                />
                {q.convertedOrderId ? <Button variant="outline" onPress={() => router.push(`/sales/${q.convertedOrderId}`)}>{t('qt.openOrder')}</Button> : null}
              </DetailSection>
              <DetailSection title={t('qt.lines', { count: q.lineItems.length })}>
                {q.lineItems.map((l) => <LineItemRow key={l.productId} name={l.productName} detail={`${l.quantity} × ${formatOrderMoney(l.unitPrice)}`} amount={formatOrderMoney(l.quantity * l.unitPrice)} />)}
              </DetailSection>
            </ScrollView>
            <ApprovalBar actions={actionList(q, has, t as never)} onDone={done} />
          </>
        )}
      </DetailState>
    </View>
  )
}
