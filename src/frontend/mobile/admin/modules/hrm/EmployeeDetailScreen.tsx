import { ScrollView, Text, View } from 'react-native'
import { Stack, useLocalSearchParams } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { useQuery } from '@tanstack/react-query'
import { Card } from '@/components/ui'
import { DetailRows } from '@/components/DetailRows'
import { DetailState } from '@/components/DetailState'
import { StatusBadge } from '@/components/StatusBadge'
import { hrmApi } from '@/modules/hrm/api'

/** An employee's record, read only (hire / terminate / transfer stay on the web: they belong to Master data). */
export default function EmployeeDetailScreen() {
  const { t } = useTranslation()
  const { id } = useLocalSearchParams<{ id: string }>()
  const query = useQuery({ queryKey: ['hrm', 'employee', id], queryFn: () => hrmApi.employee(id) })
  return (
    <View className="flex-1 bg-page">
      <Stack.Screen options={{ title: query.data?.fullName ?? t('hr.employeeDetailTitle') }} />
      <DetailState query={query} permission="hrm:employees:view">
        {(e) => (
          <ScrollView contentContainerClassName="gap-3 p-4">
            <View className="flex-row items-center justify-between gap-2">
              <Text accessibilityRole="header" className="flex-1 text-xl font-semibold text-ink">{e.fullName}</Text>
              <StatusBadge domain="employee" status={e.employmentStatus} />
            </View>
            <Card>
              <DetailRows rows={[{ label: t('hr.number'), value: e.employeeNumber }, { label: t('hr.email'), value: e.email }]} />
            </Card>
          </ScrollView>
        )}
      </DetailState>
    </View>
  )
}
