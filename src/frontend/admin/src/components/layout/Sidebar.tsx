import { useMemo } from 'react'
import { NavLink } from 'react-router-dom'
import { useUiStore } from '@/stores/ui.store'
import { usePermission } from '@/hooks/usePermission'

const NAV_ITEMS = [
  { label: 'Dashboard', path: '/dashboard', permission: null, icon: '⊞' },
  { label: 'Orders', path: '/orders', permission: 'orders:view', icon: '📦' },
  { label: 'Products', path: '/products', permission: 'products:view', icon: '🏷️' },
  { label: 'Inventory', path: '/inventory', permission: 'inventory:view', icon: '🏭' },
  { label: 'Customers', path: '/customers', permission: 'crm:view', icon: '👥' },
  { label: 'Payments', path: '/payments', permission: 'payments:view', icon: '💳' },
  { label: 'Employees', path: '/employees', permission: 'hrm:view', icon: '👤' },
  { label: 'Reports', path: '/reports', permission: 'reports:view', icon: '📊' },
  { label: 'Settings', path: '/settings', permission: 'settings:view', icon: '⚙️' },
  { label: 'Monitoring', path: '/monitoring', permission: 'monitoring:view', icon: '🖥' },
]

export function Sidebar() {
  const { sidebarCollapsed } = useUiStore()
  const { hasPermission } = usePermission()

  // Recompute only when user permissions change (hasPermission is stable via useCallback)
  const visible = useMemo(
    () => NAV_ITEMS.filter((item) => item.permission === null || hasPermission(item.permission)),
    [hasPermission],
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
            {!sidebarCollapsed && <span>{item.label}</span>}
          </NavLink>
        ))}
      </nav>
    </aside>
  )
}
