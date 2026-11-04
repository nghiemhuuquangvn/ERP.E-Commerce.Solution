import { ScrollView, Text, View } from 'react-native'
import { Stack, useLocalSearchParams } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Card } from '@/components/ui'
import { ApprovalBar, type ApprovalAction } from '@/components/ApprovalBar'
import { DetailRows } from '@/components/DetailRows'
import { DetailState } from '@/components/DetailState'
import { LineItemRow } from '@/components/LineItemRow'
import { StatusBadge } from '@/components/StatusBadge'
import { formatOrderMoney } from '@/lib/money'
import { formatDate } from '@/lib/dates'
import { useAuthStore } from '@/stores/auth.store'
import { poActions, purchaseApi, type PurchaseOrder } from '@/modules/purchase/api'

export default function PurchaseDetailScreen() {
  const { t } = useTranslation()
  const { id } = useLocalSearchParams<{ id: string }>()
  const qc = useQueryClient()
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const query = useQuery({ queryKey: ['po', 'detail', id], queryFn: () => purchaseApi.get(id) })

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['po'] })
  }

  function actionsFor(po: PurchaseOrder): ApprovalAction[] {
    const n = po.poNumber
    const all: Record<string, ApprovalAction> = {
      approve: { key: 'approve', label: t('po.approve'), variant: 'primary', confirmTitle: t('po.confirmApprove', { number: n }), run: () => purchaseApi.approve(po.id), successMessage: t('po.approved', { number: n }) },
      financeApprove: { key: 'financeApprove', label: t('po.financeApprove'), variant: 'primary', confirmTitle: t('po.confirmFinanceApprove', { number: n }), run: () => purchaseApi.approveFinance(po.id), successMessage: t('po.approved', { number: n }) },
      reject: { key: 'reject', label: t('po.reject'), variant: 'danger', confirmTitle: t('po.rejectTitle', { number: n }), reasonLabel: t('common.reason'), run: (r) => purchaseApi.reject(po.id, r ?? ''), successMessage: t('po.rejected', { number: n }) },
      cancel: { key: 'cancel', label: t('po.cancel'), variant: 'outline', confirmTitle: t('po.cancelTitle', { number: n }), confirmLabel: t('po.cancel'), run: () => purchaseApi.cancel(po.id), successMessage: t('po.cancelled', { number: n }) },
    }
    return poActions(po.status, hasPermission).map((k) => all[k])
  }

  return (
    <View className="flex-1 bg-page">
      <Stack.Screen options={{ title: query.data?.poNumber ?? t('po.detailTitle') }} />
      <DetailState query={query} permission="purchase_order:read">
        {(po) => (
          <>
            <ScrollView contentContainerClassName="gap-3 p-4">
              <View className="flex-row items-center justify-between">
                <Text accessibilityRole="header" className="text-xl font-semibold text-ink">{po.poNumber}</Text>
                <StatusBadge domain="po" status={po.status} />
              </View>
              <Card>
                <DetailRows
                  rows={[
                    { label: t('po.supplier'), value: po.supplier?.name },
                    { label: t('po.orderDate'), value: formatDate(po.orderDate) },
                    { label: t('po.delivery'), value: formatDate(po.expectedDeliveryDate) },
                  ]}
                />
              </Card>
              <Card>
                <Text accessibilityRole="header" className="mb-1 font-semibold text-ink">{t('po.lines', { count: po.items.length })}</Text>
                {po.items.map((i) => (
                  <LineItemRow key={i.id} name={i.productName} detail={`${i.quantity} ${i.unit} x ${formatOrderMoney(i.unitPrice, po.currency)}`} amount={formatOrderMoney(i.totalPrice, po.currency)} />
                ))}
                <LineItemRow name={t('po.total')} amount={formatOrderMoney(po.totalAmount, po.currency)} strong />
              </Card>
              {po.notes ? (
                <Card>
                  <Text className="text-xs text-ink-muted">{t('po.notes')}</Text>
                  <Text className="mt-1 text-sm text-ink">{po.notes}</Text>
                </Card>
              ) : null}
            </ScrollView>
            <ApprovalBar actions={actionsFor(po)} onDone={refresh} />
          </>
        )}
      </DetailState>
    </View>
  )
}
