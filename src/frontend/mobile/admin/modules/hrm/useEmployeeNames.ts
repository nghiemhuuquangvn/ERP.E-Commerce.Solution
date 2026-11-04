import { useQuery } from '@tanstack/react-query'
import { useAuthStore } from '@/stores/auth.store'
import { hrmApi } from '@/modules/hrm/api'

/**
 * employeeId -> full name, so a leave or attendance row can say who it is about (those DTOs only carry the id).
 * Needs hrm:employees:view; without it (or while it loads) the map is empty and rows fall back to their own number.
 */
export function useEmployeeNames(): Record<string, string> {
  const allowed = useAuthStore((s) => s.permissions.includes('hrm:employees:view'))
  const { data } = useQuery({
    queryKey: ['hrm', 'employee-names'],
    enabled: allowed,
    staleTime: 5 * 60_000,
    queryFn: () => hrmApi.employees('', 1, 200),
  })
  return Object.fromEntries((data?.items ?? []).map((e) => [e.id, e.fullName]))
}
