import { Text, View } from 'react-native'
import { useLocalSearchParams, useRouter } from 'expo-router'
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
  ATTENDANCE_STATUS, EMPLOYMENT_STATUS, hrmApi, hrmExtrasApi, LEAVE_STATUS,
  type Attendance, type Employee, type LeaveRequest, type PaySlip, type WorkShift,
} from '@/modules/hrm/api'
import { useEmployeeNames } from '@/modules/hrm/useEmployeeNames'

const keys = <T extends object>(o: T) => Object.keys(o) as (keyof T & string)[]

/** Employees: search by name, number or e-mail; employment status. */
export function EmployeesScreen() {
  const { t } = useTranslation()
  const router = useRouter()
  const filters = useListFilters()
  const status = filters.params.status ? EMPLOYMENT_STATUS[filters.params.status as keyof typeof EMPLOYMENT_STATUS] : undefined
  const query = usePagedList(['hrm', 'employees', filters.params], (page) => hrmApi.employees(filters.params.search ?? '', page, undefined, status))
  return (
    <PagedListView<Employee>
      query={query}
      keyOf={(e) => e.id}
      permission="hrm:employees:view"
      emptyTitle={t('hr.noEmployees')}
      header={<ListFilters filters={filters} noDates searchLabel={t('hr.searchEmployees')} statuses={keys(EMPLOYMENT_STATUS).map((s) => ({ value: s, label: t(`status.employee.${s}`) }))} />}
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

/** Leave requests: status, days / period (a request counts when its leave touches it), request number. Opens on Pending unless a link says otherwise. */
export function LeaveScreen() {
  const { t } = useTranslation()
  const router = useRouter()
  const names = useEmployeeNames()
  const route = useLocalSearchParams<{ status?: string }>()
  const initial = route.status && route.status in LEAVE_STATUS ? route.status : 'Pending'
  const filters = useListFilters({ status: initial })
  const { status, dateFrom, dateTo, search } = filters.params
  const query = usePagedList(['hrm', 'leaves', filters.params], (page) =>
    hrmApi.leaves({ status: status ? LEAVE_STATUS[status as keyof typeof LEAVE_STATUS] : undefined, dateFrom, dateTo, search }, page))
  return (
    <PagedListView<LeaveRequest>
      query={query}
      keyOf={(l) => l.id}
      permission="hrm:leave:manage"
      emptyTitle={t('hr.noLeave')}
      header={<ListFilters filters={filters} searchLabel={t('hr.searchLeave')} statuses={keys(LEAVE_STATUS).map((s) => ({ value: s, label: t(`status.leave.${s}`) }))} />}
      renderItem={(l) => {
        const who = names[l.employeeId] ?? l.leaveRequestNumber
        return (
          <Card onPress={() => router.push(`/hrm/leave/${l.id}`)} accessibilityLabel={`${who}, ${t(`leaveType.${l.leaveType}`, { defaultValue: l.leaveType })}, ${formatDate(l.startDate)} - ${formatDate(l.endDate)}`}>
            <View className="flex-row items-center justify-between gap-2">
              <Text className="flex-1 text-base font-semibold text-ink">{who}</Text>
              <StatusBadge domain="leave" status={l.leaveStatus} />
            </View>
            <Text className="mt-1 text-sm text-ink-secondary">{l.leaveRequestNumber} · {t(`leaveType.${l.leaveType}`, { defaultValue: l.leaveType })}</Text>
            <Text className="mt-1 text-xs text-ink-muted">
              {formatDate(l.startDate)} - {formatDate(l.endDate)} · {t('hr.days', { count: l.daysRequested })}
            </Text>
          </Card>
        )
      }}
    />
  )
}

/** Attendance: attendance status, days / period. */
export function AttendanceScreen() {
  const { t } = useTranslation()
  const names = useEmployeeNames()
  const filters = useListFilters({ kind: 'month' })
  const { status, dateFrom, dateTo } = filters.params
  const query = usePagedList(['hrm', 'attendance', filters.params], (page) =>
    hrmApi.attendance({ dateFrom, dateTo, status: status ? ATTENDANCE_STATUS[status as keyof typeof ATTENDANCE_STATUS] : undefined }, page))
  return (
    <PagedListView<Attendance>
      query={query}
      keyOf={(a) => a.id}
      permission="hrm:attendance:view"
      emptyTitle={t('hr.noAttendance')}
      header={<ListFilters filters={filters} noSearch searchLabel={t('hr.noAttendance')} statuses={keys(ATTENDANCE_STATUS).map((s) => ({ value: s, label: t(`status.attendance.${s}`) }))} />}
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

/** Pay slips of a period (month, quarter, year or a custom range; a period is required). Search narrows the slips of the period by number or name. */
export function PaySlipsScreen() {
  const { t } = useTranslation()
  const names = useEmployeeNames()
  const filters = useListFilters({ kind: 'month' })
  const { dateFrom, dateTo, search } = filters.params
  const query = usePagedList(['hrm', 'payslips', dateFrom, dateTo], () => hrmExtrasApi.paySlips({ dateFrom, dateTo }))
  const needle = search?.toLowerCase()
  const shown = needle ? query.items.filter((p) => `${p.paySlipNumber} ${names[p.employeeId] ?? ''}`.toLowerCase().includes(needle)) : query.items
  return (
    <PagedListView<PaySlip>
      query={{ ...query, items: shown }}
      keyOf={(p) => p.id}
      permission="hrm:payroll:manage"
      emptyTitle={t('hr.noPaySlips')}
      header={<ListFilters filters={filters} searchLabel={t('hr.searchPaySlips')} kinds={['month', 'quarter', 'year', 'custom']} />}
      renderItem={(p) => (
        <Card accessibilityLabel={`${names[p.employeeId] ?? p.paySlipNumber}, ${p.year}-${String(p.month).padStart(2, '0')}, ${formatOrderMoney(p.netSalary, 'VND')}`}>
          <View className="flex-row items-center justify-between gap-2">
            <Text className="flex-1 text-base font-semibold text-ink">{names[p.employeeId] ?? p.paySlipNumber}</Text>
            <StatusBadge domain="payslip" status={p.paymentStatus} />
          </View>
          <Text className="mt-1 text-sm text-ink-secondary">{p.paySlipNumber} · {p.year}-{String(p.month).padStart(2, '0')}</Text>
          <View className="mt-1 flex-row justify-between gap-2">
            <Text className="text-xs text-ink-muted">{t('hr.gross')}: {formatOrderMoney(p.grossSalary, 'VND')}</Text>
            <Text className="text-sm font-semibold text-ink">{t('hr.net')}: {formatOrderMoney(p.netSalary, 'VND')}</Text>
          </View>
        </Card>
      )}
    />
  )
}

/** Work shifts: active / inactive (the list is short, so the filter runs on the phone). */
export function ShiftsScreen() {
  const { t } = useTranslation()
  const filters = useListFilters({ status: 'active' })
  const query = usePagedList(['hrm', 'shifts'], () => hrmExtrasApi.shifts())
  const want = filters.params.status
  const shown = want ? query.items.filter((s) => s.isActive === (want === 'active')) : query.items
  return (
    <PagedListView<WorkShift>
      query={{ ...query, items: shown }}
      keyOf={(s) => s.id}
      permission="hrm:shifts:view"
      emptyTitle={t('hr.noShifts')}
      header={<ListFilters filters={filters} noDates noSearch searchLabel={t('hr.noShifts')} statuses={[{ value: 'active', label: t('lists.suppliers.active') }, { value: 'inactive', label: t('lists.suppliers.inactive') }]} />}
      renderItem={(s) => (
        <Card accessibilityLabel={`${s.name}, ${s.startTime} - ${s.endTime}`}>
          <View className="flex-row items-center justify-between gap-2">
            <Text className="flex-1 text-base font-semibold text-ink">{s.name}</Text>
            <Badge tone={s.isActive ? 'success' : 'neutral'} label={s.isActive ? t('lists.suppliers.active') : t('lists.suppliers.inactive')} />
          </View>
          <Text className="mt-1 text-sm text-ink-secondary">{s.startTime} - {s.endTime} · {t('hr.hours', { count: s.shiftHours })}</Text>
        </Card>
      )}
    />
  )
}
