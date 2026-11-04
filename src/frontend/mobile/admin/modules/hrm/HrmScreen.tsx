import { useState } from 'react'
import { Text, View } from 'react-native'
import { useLocalSearchParams, useRouter } from 'expo-router'
import { useTranslation } from 'react-i18next'
import { Card } from '@/components/ui'
import { FilterChips, type Chip } from '@/components/FilterChips'
import { NoAccess } from '@/components/NoAccess'
import { PagedListView } from '@/components/PagedListView'
import { SearchField } from '@/components/SearchField'
import { SegmentedControl, type Segment } from '@/components/SegmentedControl'
import { StatusBadge } from '@/components/StatusBadge'
import { formatDate } from '@/lib/dates'
import { useAuthStore } from '@/stores/auth.store'
import { usePagedList } from '@/modules/usePagedList'
import { hrmApi, LEAVE_STATUS, type Attendance, type Employee, type LeaveRequest } from '@/modules/hrm/api'
import { useEmployeeNames } from '@/modules/hrm/useEmployeeNames'

type Tab = 'employees' | 'leave' | 'attendance'
const PERMISSION: Record<Tab, string> = { employees: 'hrm:employees:view', leave: 'hrm:leave:manage', attendance: 'hrm:attendance:view' }

function EmployeesTab() {
  const { t } = useTranslation()
  const router = useRouter()
  const [search, setSearch] = useState('')
  const query = usePagedList(['hrm', 'employees', search], (page) => hrmApi.employees(search.trim(), page))
  return (
    <PagedListView<Employee>
      query={query}
      keyOf={(e) => e.id}
      permission={PERMISSION.employees}
      emptyTitle={t('hr.noEmployees')}
      header={<SearchField value={search} onChangeText={setSearch} placeholder={t('common.search')} label={t('hr.searchEmployees')} />}
      renderItem={(e) => (
        <Card onPress={() => router.push(`/hrm/employee/${e.id}`)} accessibilityLabel={`${e.fullName}, ${e.employeeNumber}`}>
          <View className="flex-row items-center justify-between gap-2">
            <Text className="flex-1 text-base font-semibold text-ink">{e.fullName}</Text>
            <StatusBadge domain="employee" status={e.employmentStatus} />
          </View>
          <Text className="mt-1 text-sm text-ink-secondary">{e.employeeNumber}</Text>
        </Card>
      )}
    />
  )
}

type LeaveFilter = 'Pending' | 'Approved' | 'ALL'

function LeaveTab({ initial }: { initial?: LeaveFilter }) {
  const { t } = useTranslation()
  const router = useRouter()
  const names = useEmployeeNames()
  const [filter, setFilter] = useState<LeaveFilter>(initial ?? 'Pending')
  const status = filter === 'ALL' ? undefined : LEAVE_STATUS[filter]
  const query = usePagedList(['hrm', 'leaves', filter], (page) => hrmApi.leaves(status, page))
  const chips: Chip<LeaveFilter>[] = [
    { value: 'Pending', label: t('status.leave.Pending') },
    { value: 'Approved', label: t('status.leave.Approved') },
    { value: 'ALL', label: t('common.all') },
  ]
  return (
    <PagedListView<LeaveRequest>
      query={query}
      keyOf={(l) => l.id}
      permission={PERMISSION.leave}
      emptyTitle={t('hr.noLeave')}
      header={<FilterChips chips={chips} value={filter} onChange={setFilter} />}
      renderItem={(l) => {
        const who = names[l.employeeId] ?? l.leaveRequestNumber
        return (
          <Card onPress={() => router.push(`/hrm/leave/${l.id}`)} accessibilityLabel={`${who}, ${t(`leaveType.${l.leaveType}`, { defaultValue: l.leaveType })}, ${formatDate(l.startDate)} - ${formatDate(l.endDate)}`}>
            <View className="flex-row items-center justify-between gap-2">
              <Text className="flex-1 text-base font-semibold text-ink">{who}</Text>
              <StatusBadge domain="leave" status={l.leaveStatus} />
            </View>
            <Text className="mt-1 text-sm text-ink-secondary">{t(`leaveType.${l.leaveType}`, { defaultValue: l.leaveType })}</Text>
            <Text className="mt-1 text-xs text-ink-muted">
              {formatDate(l.startDate)} - {formatDate(l.endDate)} · {t('hr.days', { count: l.daysRequested })}
            </Text>
          </Card>
        )
      }}
    />
  )
}

function AttendanceTab() {
  const { t } = useTranslation()
  const names = useEmployeeNames()
  const query = usePagedList(['hrm', 'attendance'], (page) => hrmApi.attendance(page))
  return (
    <PagedListView<Attendance>
      query={query}
      keyOf={(a) => a.id}
      permission={PERMISSION.attendance}
      emptyTitle={t('hr.noAttendance')}
      renderItem={(a) => (
        <Card>
          <View className="flex-row items-center justify-between gap-2">
            <Text className="flex-1 text-base font-semibold text-ink">{names[a.employeeId] ?? formatDate(a.date)}</Text>
            <StatusBadge domain="attendance" status={a.status} />
          </View>
          <Text className="mt-1 text-sm text-ink-secondary">
            {formatDate(a.date)} · {t('hr.hours', { count: a.totalHours })}
          </Text>
        </Card>
      )}
    />
  )
}

/** HR: Employees | Leave | Attendance. A segment shows only when the user holds its permission. */
export default function HrmScreen() {
  const { t } = useTranslation()
  const params = useLocalSearchParams<{ tab?: string }>()
  const permissions = useAuthStore((s) => s.permissions)
  const allowed = (['employees', 'leave', 'attendance'] as Tab[]).filter((k) => permissions.includes(PERMISSION[k]))
  const first = allowed.includes(params.tab as Tab) ? (params.tab as Tab) : allowed[0]
  const [tab, setTab] = useState<Tab | undefined>(first)
  if (allowed.length === 0 || !tab) return <NoAccess permission={PERMISSION.employees} />
  const segments: Segment<Tab>[] = allowed.map((k) => ({ value: k, label: t(`hr.tab.${k}`) }))
  return (
    <View className="flex-1 bg-page">
      <View className="px-4 pt-3">
        <SegmentedControl segments={segments} value={tab} onChange={setTab} />
      </View>
      {tab === 'employees' ? <EmployeesTab /> : tab === 'leave' ? <LeaveTab /> : <AttendanceTab />}
    </View>
  )
}
