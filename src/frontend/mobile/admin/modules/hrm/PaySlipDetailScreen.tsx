import { RefreshControl, ScrollView, View } from 'react-native'
import { Stack, useLocalSearchParams } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert } from '@/components/ui'
import { ApprovalBar, type ApprovalAction } from '@/components/ApprovalBar'
import { DetailHeader, DetailSection } from '@/components/DetailSection'
import { DetailRows } from '@/components/DetailRows'
import { DetailState } from '@/components/DetailState'
import { StatusBadge } from '@/components/StatusBadge'
import { formatDate } from '@/lib/dates'
import { formatOrderMoney } from '@/lib/money'
import { useAuthStore } from '@/stores/auth.store'
import { hrmExtrasApi, paySlipActions, type PaySlip } from '@/modules/hrm/api'
import { useEmployeeNames } from '@/modules/hrm/useEmployeeNames'

const vnd = (n?: number) => formatOrderMoney(n ?? 0, 'VND')

/** A slip calculated by the payroll run carries the base salary and the insurance / tax breakdown; a slip typed in by hand has only the lump sums. */
export const isCalculated = (p: PaySlip) => (p.baseSalary ?? 0) > 0 || (p.workedDays ?? 0) > 0

/** The employer's extra cost on top of the gross pay (insurance paid by the company). */
export const employerCost = (p: PaySlip) => (p.employerSocial ?? 0) + (p.employerHealth ?? 0) + (p.employerUnemployment ?? 0)

function actionList(p: PaySlip, has: (perm: string) => boolean, who: string, t: (k: string, o?: Record<string, unknown>) => string): ApprovalAction[] {
  const all: Record<string, ApprovalAction> = {
    pay: {
      key: 'pay', label: t('slip.pay'), variant: 'primary', confirmTitle: t('slip.payTitle', { name: who }),
      fields: [{ key: 'paymentMethod', label: t('slip.method') }],
      run: (_reason, v) => hrmExtrasApi.payPaySlip(p.id, v?.paymentMethod), successMessage: t('slip.paid', { name: who }),
    },
    cancel: {
      key: 'cancel', label: t('slip.cancel'), variant: 'danger', confirmTitle: t('slip.cancelTitle', { name: who }),
      reasonLabel: t('common.reason'), run: (reason) => hrmExtrasApi.cancelPaySlip(p.id, reason ?? ''), successMessage: t('slip.cancelled', { name: who }),
    },
  }
  return paySlipActions(p.paymentStatus, has).map((k) => all[k])
}

/** Pay slip: pay, the calculation (days, gross, insurance, tax) and the net amount; pay or cancel while pending. */
export default function PaySlipDetailScreen() {
  const { t } = useTranslation()
  const { id } = useLocalSearchParams<{ id: string }>()
  const qc = useQueryClient()
  const names = useEmployeeNames()
  const has = useAuthStore((s) => s.hasPermission)
  const query = useQuery({ queryKey: ['hrm', 'payslip', id], queryFn: () => hrmExtrasApi.paySlip(id) })
  const done = () => { void qc.invalidateQueries({ queryKey: ['hrm'] }) }

  return (
    <View className="flex-1 bg-page">
      <Stack.Screen options={{ title: t('slip.title') }} />
      <DetailState query={query} permission="hrm:payroll:manage">
        {(p) => {
          const who = names[p.employeeId] ?? p.paySlipNumber
          const gross = p.grossSalary + (p.overtimePay ?? 0)
          return (
            <>
              <ScrollView contentContainerClassName="gap-3 p-4" refreshControl={<RefreshControl refreshing={query.isRefetching} onRefresh={() => void query.refetch()} />}>
                <DetailHeader title={who} badge={<StatusBadge domain="payslip" status={p.paymentStatus} />} />
                <DetailSection title={t('slip.summary')}>
                  <DetailRows
                    rows={[
                      { label: t('slip.number'), value: p.paySlipNumber },
                      { label: t('slip.period'), value: `${p.year}-${String(p.month).padStart(2, '0')}` },
                      { label: t('slip.paymentDate'), value: p.paymentDate ? formatDate(p.paymentDate) : null },
                      { label: t('slip.net'), value: vnd(p.netSalary) },
                    ]}
                  />
                </DetailSection>
                {isCalculated(p) ? (
                  <>
                    <DetailSection title={t('slip.earnings')}>
                      <DetailRows
                        rows={[
                          { label: t('slip.base'), value: vnd(p.baseSalary) },
                          { label: t('slip.days'), value: `${p.workedDays ?? 0} / ${p.standardDays ?? 0}` },
                          { label: t('slip.allowances'), value: vnd(p.allowances) },
                          { label: t('slip.overtime', { hours: p.overtimeHours ?? 0 }), value: vnd(p.overtimePay) },
                          { label: t('slip.gross'), value: vnd(gross) },
                        ]}
                      />
                    </DetailSection>
                    <DetailSection title={t('slip.withheld')}>
                      <DetailRows
                        rows={[
                          { label: t('slip.social'), value: `− ${vnd(p.employeeSocial)}` },
                          { label: t('slip.health'), value: `− ${vnd(p.employeeHealth)}` },
                          { label: t('slip.unemployment'), value: `− ${vnd(p.employeeUnemployment)}` },
                          { label: t('slip.taxable', { count: p.dependentCount ?? 0 }), value: vnd(p.taxableIncome) },
                          { label: t('slip.tax'), value: `− ${vnd(p.personalIncomeTax)}` },
                        ]}
                      />
                    </DetailSection>
                    <DetailSection title={t('slip.employer')}>
                      <DetailRows rows={[{ label: t('slip.employerCost'), value: vnd(employerCost(p)) }]} />
                    </DetailSection>
                  </>
                ) : (
                  <>
                    <Alert tone="info">{t('slip.manual')}</Alert>
                    <DetailSection title={t('slip.earnings')}>
                      <DetailRows
                        rows={[
                          { label: t('slip.gross'), value: vnd(gross) },
                          { label: t('slip.overtime', { hours: p.overtimeHours ?? 0 }), value: vnd(p.overtimePay) },
                          { label: t('slip.deductions'), value: `− ${vnd(p.deductions)}` },
                        ]}
                      />
                    </DetailSection>
                  </>
                )}
              </ScrollView>
              <ApprovalBar actions={actionList(p, has, who, t as never)} onDone={done} />
            </>
          )
        }}
      </DetailState>
    </View>
  )
}
