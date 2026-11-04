import { ScrollView, Text, View } from 'react-native'
import { Stack, useLocalSearchParams } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { Card } from '@/components/ui'
import { ApprovalBar, type ApprovalAction } from '@/components/ApprovalBar'
import { DetailRows } from '@/components/DetailRows'
import { DetailState } from '@/components/DetailState'
import { StatusBadge } from '@/components/StatusBadge'
import { formatDate } from '@/lib/dates'
import { useAuthStore } from '@/stores/auth.store'
import { hrmApi, leaveActions, type LeaveRequest } from '@/modules/hrm/api'
import { useEmployeeNames } from '@/modules/hrm/useEmployeeNames'

export default function LeaveDetailScreen() {
  const { t } = useTranslation()
  const { id } = useLocalSearchParams<{ id: string }>()
  const qc = useQueryClient()
  const names = useEmployeeNames()
  const hasPermission = useAuthStore((s) => s.hasPermission)
  const query = useQuery({ queryKey: ['hrm', 'leave', id], queryFn: () => hrmApi.leave(id) })

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['hrm'] })
  }

  function actionsFor(l: LeaveRequest): ApprovalAction[] {
    const who = names[l.employeeId] ?? l.leaveRequestNumber
    const all: Record<string, ApprovalAction> = {
      approve: { key: 'approve', label: t('hr.approve'), variant: 'primary', confirmTitle: t('hr.confirmApprove', { name: who }), run: () => hrmApi.approveLeave(l.id), successMessage: t('hr.approved', { name: who }) },
      reject: { key: 'reject', label: t('hr.reject'), variant: 'danger', confirmTitle: t('hr.rejectTitle', { name: who }), reasonLabel: t('common.reason'), run: (r) => hrmApi.rejectLeave(l.id, r ?? ''), successMessage: t('hr.rejected', { name: who }) },
      cancel: { key: 'cancel', label: t('hr.cancelLeave'), variant: 'outline', confirmTitle: t('hr.cancelTitle', { name: who }), run: () => hrmApi.cancelLeave(l.id), successMessage: t('hr.cancelled', { name: who }) },
    }
    return leaveActions(l.leaveStatus, hasPermission).map((k) => all[k])
  }

  return (
    <View className="flex-1 bg-page">
      <Stack.Screen options={{ title: t('hr.leaveDetailTitle') }} />
      <DetailState query={query} permission="hrm:leave:manage">
        {(l) => (
          <>
            <ScrollView contentContainerClassName="gap-3 p-4">
              <View className="flex-row items-center justify-between gap-2">
                <Text accessibilityRole="header" className="flex-1 text-xl font-semibold text-ink">{names[l.employeeId] ?? l.leaveRequestNumber}</Text>
                <StatusBadge domain="leave" status={l.leaveStatus} />
              </View>
              <Card>
                <DetailRows
                  rows={[
                    { label: t('hr.number'), value: l.leaveRequestNumber },
                    { label: t('hr.type'), value: t(`leaveType.${l.leaveType}`, { defaultValue: l.leaveType }) },
                    { label: t('hr.from'), value: formatDate(l.startDate) },
                    { label: t('hr.to'), value: formatDate(l.endDate) },
                    { label: t('hr.daysLabel'), value: String(l.daysRequested) },
                    { label: t('hr.reason'), value: l.reason },
                  ]}
                />
              </Card>
            </ScrollView>
            <ApprovalBar actions={actionsFor(l)} onDone={refresh} />
          </>
        )}
      </DetailState>
    </View>
  )
}
