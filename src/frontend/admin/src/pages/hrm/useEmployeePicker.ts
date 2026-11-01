import { useQuery } from '@tanstack/react-query'
import { employeeSnapshotsApi } from '@/api/hrm/employeeSnapshots.api'

/** The employees an HRM form can pick from (one query key, shared by Attendance, Leave requests and Payroll). */
export function useEmployeePicker() {
  return useQuery({
    queryKey: ['hrm', 'employees', 'picker'],
    queryFn: () => employeeSnapshotsApi.list({ page: 1, pageSize: 200 }),
    staleTime: 60_000,
  })
}
