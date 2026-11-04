import { useMemo } from 'react'
import { useAuthStore } from '@/stores/auth.store'
import { MODULES } from '@/modules'
import type { ErpModule } from '@/modules/types'

/** The tab bar holds this many modules; every visible module is also in the side menu, which is the only way to the others. */
export const MAX_TAB_MODULES = 3

/**
 * Modules this user may open: the token has one of the module's permissions (never a role name), and, when the token lists
 * licensed modules at all, the module is licensed. A token without licensed-module claims does not hide anything (the server
 * still enforces the licence on every call).
 */
export function visibleModules(permissions: string[], licensedModules: string[], all: ErpModule[] = MODULES): ErpModule[] {
  return all
    .filter((m) => m.permissions.length === 0 || m.permissions.some((p) => permissions.includes(p)))
    .filter((m) => !m.licensedModule || licensedModules.length === 0 || licensedModules.includes(m.licensedModule))
    .sort((a, b) => a.order - b.order)
}

export function splitTabs(visible: ErpModule[], max = MAX_TAB_MODULES): { tabs: ErpModule[]; overflow: ErpModule[] } {
  return { tabs: visible.slice(0, max), overflow: visible.slice(max) }
}

export function useVisibleModules() {
  const permissions = useAuthStore((s) => s.permissions)
  const licensed = useAuthStore((s) => s.licensedModules)
  return useMemo(() => {
    const visible = visibleModules(permissions, licensed)
    return { visible, ...splitTabs(visible) }
  }, [permissions, licensed])
}
