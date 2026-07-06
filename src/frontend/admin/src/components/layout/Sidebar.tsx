import { useMemo } from 'react'
import { NavLink } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useUiStore } from '@/stores/ui.store'
import { usePermission } from '@/hooks/usePermission'

interface NavItem {
  labelKey: string
  path: string
  icon: string
  // Single-permission items use `permission` (null = always visible). Items
  // visible under any of several permissions (OR) use `permissions` instead.
  permission?: string | null
  permissions?: string[]
}

const NAV_ITEMS: NavItem[] = [
  { labelKey: 'nav.dashboard', path: '/dashboard', permission: null, icon: '⊞' },
  { labelKey: 'nav.orders', path: '/orders', permission: 'orders:view', icon: '📦' },
  { labelKey: 'nav.products', path: '/products', permission: 'products:view', icon: '🏷️' },
  { labelKey: 'nav.inventory', path: '/inventory', permission: 'inventory:view', icon: '🏭' },
  { labelKey: 'nav.customers', path: '/customers', permission: 'crm:view', icon: '👥' },
  { labelKey: 'nav.payments', path: '/payments', permission: 'payments:view', icon: '💳' },
  {
    labelKey: 'nav.departments',
    path: '/masterdata/departments',
    permission: 'masterdata:departments:view',
    icon: '🏢',
  },
  {
    labelKey: 'nav.positions',
    path: '/masterdata/positions',
    permission: 'masterdata:positions:view',
    icon: '📋',
  },
  {
    labelKey: 'nav.employees',
    path: '/masterdata/employees',
    permission: 'masterdata:employees:view',
    icon: '👤',
  },
  {
    labelKey: 'nav.unitsOfMeasure',
    path: '/masterdata/units-of-measure',
    permission: 'masterdata:units-of-measure:view',
    icon: '📏',
  },
  {
    labelKey: 'nav.geography',
    path: '/masterdata/geography',
    permission: 'masterdata:geography:view',
    icon: '🗺️',
  },
  {
    labelKey: 'nav.languages',
    path: '/masterdata/languages',
    permission: 'masterdata:languages:view',
    icon: '🌐',
  },
  { labelKey: 'nav.reports', path: '/reports', permission: 'reports:view', icon: '📊' },
  { labelKey: 'nav.settings', path: '/settings', permission: 'settings:view', icon: '⚙️' },
  {
    labelKey: 'nav.authSettings',
    path: '/settings/auth',
    permissions: ['settings:system:view', 'settings:tenant:view'],
    icon: '🔐',
  },
  { labelKey: 'nav.monitoring', path: '/monitoring', permission: 'monitoring:view', icon: '🖥' },
  { labelKey: 'nav.logs', path: '/logs', permission: 'logging:logs:view', icon: '📜' },
]

export function Sidebar() {
  const { t } = useTranslation()
  const { sidebarCollapsed } = useUiStore()
  const { hasPermission, hasAnyPermission } = usePermission()

  // Recompute only when user permissions change (hasPermission is stable via useCallback)
  const visible = useMemo(
    () =>
      NAV_ITEMS.filter((item) => {
        if (item.permissions) return hasAnyPermission(...item.permissions)
        return item.permission === null || item.permission === undefined || hasPermission(item.permission)
      }),
    [hasPermission, hasAnyPermission],
  )

  return (
    <aside
      className={`flex h-full flex-col border-r border-gray-200 bg-white transition-all duration-200 ${
        sidebarCollapsed ? 'w-16' : 'w-60'
      }`}
    >
      <div className="flex h-16 items-center px-4 border-b border-gray-200">
        {sidebarCollapsed ? (
          <span className="text-xl font-bold text-primary-600">E</span>
        ) : (
          <span className="text-lg font-bold text-primary-600">ERP Admin</span>
        )}
      </div>

      <nav className="flex-1 overflow-y-auto py-4">
        {visible.map((item) => (
          <NavLink
            key={item.path}
            to={item.path}
            className={({ isActive }) =>
              `flex items-center gap-3 px-4 py-2.5 text-sm font-medium transition-colors hover:bg-gray-50 ${
                isActive ? 'bg-primary-50 text-primary-700 border-r-2 border-primary-600' : 'text-gray-600'
              }`
            }
          >
            <span className="text-base">{item.icon}</span>
            {!sidebarCollapsed && <span>{t(item.labelKey)}</span>}
          </NavLink>
        ))}
      </nav>
    </aside>
  )
}
