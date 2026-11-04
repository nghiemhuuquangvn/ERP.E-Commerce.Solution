import { ScrollView, Text, View } from 'react-native'
import { Stack, useLocalSearchParams } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Card } from '@/components/ui'
import { ApprovalBar, type ApprovalAction } from '@/components/ApprovalBar'
import { DetailRows } from '@/components/DetailRows'
import { DetailState } from '@/components/DetailState'
import { LineItemRow } from '@/components/LineItemRow'
import { StatusBadge } from '@/components/StatusBadge'
import { formatMoney } from '@/lib/money'
import { formatDate } from '@/lib/dates'
import { useAuthStore } from '@/stores/auth.store'
import { salesApi, soActions, type SalesOrder } from '@/modules/sales/api'

const addressText = (a: SalesOrder['shippingAddress'] | undefined) =>
  a ? [a.street, a.ward, a.city, a.state, a.country].filter(Boolean).join(', ') : ''

export default function SalesDetailScreen() {
  const { t } = useTranslation()
  const { id } = useLocalSearchParams<{ id: string }>()
  const qc = useQueryClient()
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const query = useQuery({ queryKey: ['so', 'detail', id], queryFn: () => salesApi.get(id) })

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['so'] })
  }

  function actionsFor(o: SalesOrder): ApprovalAction[] {
    const n = o.orderNo
    const all: Record<string, ApprovalAction> = {
      approve: { key: 'approve', label: t('so.approve'), variant: 'primary', confirmTitle: t('so.confirmApprove', { number: n }), run: () => salesApi.approve(o.orderId), successMessage: t('so.approved', { number: n }) },
      reject: { key: 'reject', label: t('so.reject'), variant: 'danger', confirmTitle: t('so.rejectTitle', { number: n }), reasonLabel: t('common.reason'), run: (r) => salesApi.reject(o.orderId, r ?? ''), successMessage: t('so.rejected', { number: n }) },
      cancel: { key: 'cancel', label: t('so.cancel'), variant: 'outline', confirmTitle: t('so.cancelTitle', { number: n }), confirmLabel: t('so.cancel'), reasonLabel: t('common.reason'), run: (r) => salesApi.cancel(o.orderId, r ?? ''), successMessage: t('so.cancelled', { number: n }) },
    }
    return soActions(o.status, hasPermission).map((k) => all[k])
  }

  return (
    <View className="flex-1 bg-page">
      <Stack.Screen options={{ title: query.data?.orderNo ?? t('so.detailTitle') }} />
      <DetailState query={query} permission="sales_order:read">
        {(o) => (
          <>
            <ScrollView contentContainerClassName="gap-3 p-4">
              <View className="flex-row items-center justify-between">
                <Text accessibilityRole="header" className="text-xl font-semibold text-ink">{o.orderNo}</Text>
                <StatusBadge domain="so" status={o.status} />
              </View>
              {o.isAnomalous ? <Alert tone="warning">{o.anomalyReason ?? t('so.flagged')}</Alert> : null}
              <Card>
                <DetailRows
                  rows={[
                    { label: t('so.customer'), value: o.customerName },
                    { label: t('so.orderDate'), value: formatDate(o.orderDate) },
                    { label: t('so.shipTo'), value: addressText(o.shippingAddress) },
                    { label: t('so.tracking'), value: o.trackingNumber ? `${o.carrier ?? ''} ${o.trackingNumber}`.trim() : null },
                  ]}
                />
              </Card>
              <Card>
                <Text accessibilityRole="header" className="mb-1 font-semibold text-ink">{t('so.lines', { count: o.lineItems.length })}</Text>
                {o.lineItems.map((l) => (
                  <LineItemRow key={`${l.productId}-${l.productName}`} name={l.productName} detail={`${l.quantity} x ${formatMoney(l.unitPrice)}`} amount={formatMoney(l.quantity * l.unitPrice)} />
                ))}
                {o.discountAmount ? <LineItemRow name={t('so.discount')} amount={`-${formatMoney(o.discountAmount)}`} /> : null}
                {o.shippingFee ? <LineItemRow name={t('so.shipping')} amount={formatMoney(o.shippingFee)} /> : null}
                <LineItemRow name={t('so.total')} amount={formatMoney(o.totalAmount)} strong />
              </Card>
              {o.notes ? (
                <Card>
                  <Text className="text-xs text-ink-muted">{t('so.notes')}</Text>
                  <Text className="mt-1 text-sm text-ink">{o.notes}</Text>
                </Card>
              ) : null}
            </ScrollView>
            <ApprovalBar actions={actionsFor(o)} onDone={refresh} />
          </>
        )}
      </DetailState>
    </View>
  )
}
