import { useCallback } from 'react'
import { useAuthStore } from '@/stores/auth.store'

export function usePermission() {
  const user = useAuthStore((s) => s.user)

  const hasPermission = useCallback(
    (permission: string): boolean => {
      if (!user) return false
      if (user.roles.includes('Admin') || user.roles.includes('SuperAdmin')) return true
      return user.permissions.includes(permission)
    },
    [user],
  )

  const hasAnyPermission = useCallback(
    (...permissions: string[]): boolean => permissions.some((p) => hasPermission(p)),
    [hasPermission],
  )

  const hasAllPermissions = useCallback(
    (...permissions: string[]): boolean => permissions.every((p) => hasPermission(p)),
    [hasPermission],
  )

  return { hasPermission, hasAnyPermission, hasAllPermissions }
}
