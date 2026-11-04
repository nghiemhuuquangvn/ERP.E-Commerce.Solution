import { useMemo } from 'react'
import type { LucideIcon } from 'lucide-react-native'
import { useAuthStore } from '@/stores/auth.store'
import { MODULES } from '@/modules'
import type { ErpModule, ModuleSection } from '@/modules/types'

/** The bottom bar holds this many items (a "More" item counts); what does not fit goes behind "More". */
export const MAX_BAR_ITEMS = 5

const allows = (needed: string[], held: string[]) => needed.length === 0 || needed.some((p) => held.includes(p))

/** Sections of a module this user may open (the token holds one of the section's permissions - never a role name). */
export function visibleSections(module: ErpModule, permissions: string[]): ModuleSection[] {
  return (module.sections ?? []).filter((s) => allows(s.permissions, permissions)).sort((a, b) => a.order - b.order)
}

/**
 * Modules this user may open: with sections, at least one section is allowed; without, the token holds one of the module's
 * permissions. When the token lists licensed modules at all, the module is licensed. A token without licensed-module claims
 * does not hide anything (the server still enforces the licence on every call).
 */
export function visibleModules(permissions: string[], licensedModules: string[], all: ErpModule[] = MODULES): ErpModule[] {
  return all
    .filter((m) => (m.sections?.length ? visibleSections(m, permissions).length > 0 : allows(m.permissions, permissions)))
    .filter((m) => !m.licensedModule || licensedModules.length === 0 || licensedModules.includes(m.licensedModule))
    .sort((a, b) => a.order - b.order)
}

/** Where opening a module lands: its first section the user may open (else the module's own route). */
export function moduleLanding(module: ErpModule, permissions: string[]): string {
  return visibleSections(module, permissions)[0]?.route ?? module.route
}

/** The module a screen file belongs to (the module route or one of its sections). */
export function owningModule(route: string, all: ErpModule[] = MODULES): ErpModule | undefined {
  return all.find((m) => m.route === route || m.sections?.some((s) => s.route === route))
}

export interface BarItem {
  key: string
  titleKey: string
  icon: LucideIcon
  /** Screen file to open. */
  route: string
}

export interface Bar {
  /** modules: the bar of Home (one item per module); module: the bar inside a module (Home + its sections). */
  mode: 'modules' | 'module'
  module?: ErpModule
  items: BarItem[]
  /** Sections behind "More" (module mode only; in modules mode "More" opens the side menu and this is empty). */
  overflow: BarItem[]
  /** The bar ends with a "More" item. */
  hasMore: boolean
}

/**
 * The bottom bar for the screen the user is on. On Home (and on any screen of a module without sections) it lists the modules, as
 * many as fit - "More" opens the side menu with all of them. Inside a module it lists Home (the way back) and the module's sections;
 * "More" opens the sections that did not fit.
 */
export function barFor(route: string, modules: ErpModule[], permissions: string[], max = MAX_BAR_ITEMS): Bar {
  const owner = owningModule(route, modules)
  const sections = owner ? visibleSections(owner, permissions) : []

  if (!owner || sections.length === 0) {
    const fits = modules.length <= max
    const shown = fits ? modules : modules.slice(0, max - 1)
    return {
      mode: 'modules',
      items: shown.map((m) => ({ key: m.id, titleKey: m.titleKey, icon: m.icon, route: moduleLanding(m, permissions) })),
      overflow: [],
      hasMore: !fits,
    }
  }

  const home = modules.find((m) => !m.sections?.length && m.route === 'index')
  const homeItems: BarItem[] = home ? [{ key: home.id, titleKey: home.titleKey, icon: home.icon, route: home.route }] : []
  const budget = max - homeItems.length
  const fits = sections.length <= budget
  const shown = fits ? sections : sections.slice(0, budget - 1)
  const toItem = (s: ModuleSection): BarItem => ({ key: s.id, titleKey: s.titleKey, icon: s.icon, route: s.route })
  return {
    mode: 'module',
    module: owner,
    items: [...homeItems, ...shown.map(toItem)],
    overflow: fits ? [] : sections.slice(budget - 1).map(toItem),
    hasMore: !fits,
  }
}

export function useVisibleModules() {
  const permissions = useAuthStore((s) => s.permissions)
  const licensed = useAuthStore((s) => s.licensedModules)
  return useMemo(() => ({ visible: visibleModules(permissions, licensed) }), [permissions, licensed])
}
