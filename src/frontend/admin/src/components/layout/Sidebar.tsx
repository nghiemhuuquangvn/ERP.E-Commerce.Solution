import { useMemo, useState } from 'react'
import { NavLink, useLocation } from 'react-router-dom'
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
  // Present only on group headers (e.g. "Settings"). A group renders as an
  // expand/collapse toggle instead of a link; `path` is unused for routing
  // but kept so the group has a stable React key.
  children?: NavItem[]
}

const SETTINGS_CHILDREN: NavItem[] = [
  {
    labelKey: 'nav.generalSettings',
    path: '/settings/general',
    permissions: ['settings:general:system:view', 'settings:general:tenant:view'],
    icon: '🛠️',
  },
  {
    labelKey: 'nav.companySettings',
    path: '/settings/company',
    permissions: ['settings:company:tenant:view'],
    icon: '🏢',
  },
  {
    labelKey: 'nav.authSettings',
    path: '/settings/auth',
    permissions: ['settings:system:view', 'settings:tenant:view'],
    icon: '🔐',
  },
  {
    labelKey: 'nav.notificationSettings',
    path: '/settings/notification',
    permissions: ['settings:notification:system:view', 'settings:notification:tenant:view'],
    icon: '🔔',
  },
  {
    labelKey: 'nav.emailSettings',
    path: '/settings/email',
    permissions: ['settings:email:system:view', 'settings:email:tenant:view'],
    icon: '✉️',
  },
  {
    labelKey: 'nav.smsSettings',
    path: '/settings/sms',
    permissions: ['settings:sms:system:view', 'settings:sms:tenant:view'],
    icon: '📱',
  },
  {
    labelKey: 'nav.workflowSettings',
    path: '/settings/workflow',
    permissions: ['settings:workflow:system:view', 'settings:workflow:tenant:view'],
    icon: '🔀',
  },
  {
    labelKey: 'nav.fileStorageSettings',
    path: '/settings/file-storage',
    permissions: ['settings:file-storage:system:view', 'settings:file-storage:tenant:view'],
    icon: '🗄️',
  },
  {
    labelKey: 'nav.schedulerSettings',
    path: '/settings/scheduler',
    permissions: ['settings:scheduler:system:view', 'settings:scheduler:tenant:view'],
    icon: '⏱️',
  },
  {
    labelKey: 'nav.integrationSettings',
    path: '/settings/integration',
    permissions: ['settings:integration:system:view', 'settings:integration:tenant:view'],
    icon: '🔗',
  },
  {
    labelKey: 'nav.apiSettings',
    path: '/settings/api',
    permissions: ['settings:api:system:view', 'settings:api:tenant:view'],
    icon: '🧩',
  },
  {
    labelKey: 'nav.auditSettings',
    path: '/settings/audit',
    permissions: ['settings:audit:system:view', 'settings:audit:tenant:view'],
    icon: '🕵️',
  },
  {
    labelKey: 'nav.advancedSettings',
    path: '/settings/advanced',
    permissions: ['settings:advanced:system:view', 'settings:advanced:tenant:view'],
    icon: '🧪',
  },
  {
    labelKey: 'nav.localizationSettings',
    path: '/settings/localization',
    permissions: ['settings:localization:system:view', 'settings:localization:tenant:view'],
    icon: '🌍',
  },
  {
    labelKey: 'nav.featureFlagsSettings',
    path: '/settings/feature-flags',
    permissions: ['settings:feature-flags:system:view', 'settings:feature-flags:tenant:view'],
    icon: '🚩',
  },
  {
    labelKey: 'nav.licenseSettings',
    path: '/settings/license',
    // License is not a normal self-service Settings category — gate the nav item on
    // holding any of the license:* permissions (modules/plans catalog management, or
    // viewing/issuing a tenant's license), not a system/tenant view pair.
    permissions: [
      'license:modules:manage',
      'license:plans:view',
      'license:plans:manage',
      'license:tenant:view',
      'license:tenant:issue',
    ],
    icon: '🔑',
  },
  {
    labelKey: 'nav.storeProductSettings',
    path: '/settings/store-product',
    permissions: ['settings:store-product:system:view', 'settings:store-product:tenant:view'],
    icon: '🏬',
  },
  {
    labelKey: 'nav.cartCheckoutSettings',
    path: '/settings/cart-checkout',
    permissions: ['settings:cart-checkout:system:view', 'settings:cart-checkout:tenant:view'],
    icon: '🛒',
  },
  {
    labelKey: 'nav.orderSettings',
    path: '/settings/order',
    permissions: ['settings:order:system:view', 'settings:order:tenant:view'],
    icon: '🧾',
  },
  {
    labelKey: 'nav.inventorySettings',
    path: '/settings/inventory',
    permissions: ['settings:inventory:system:view', 'settings:inventory:tenant:view'],
    icon: '📦',
  },
  {
    labelKey: 'nav.paymentSettings',
    path: '/settings/payment',
    permissions: ['settings:payment:system:view', 'settings:payment:tenant:view'],
    icon: '💰',
  },
  {
    labelKey: 'nav.shippingSettings',
    path: '/settings/shipping',
    permissions: ['settings:shipping:system:view', 'settings:shipping:tenant:view'],
    icon: '🚚',
  },
  {
    labelKey: 'nav.taxSettings',
    path: '/settings/tax',
    permissions: ['settings:tax:system:view', 'settings:tax:tenant:view'],
    icon: '🧮',
  },
  {
    labelKey: 'nav.currencySettings',
    path: '/settings/currency',
    permissions: ['settings:currency:system:view', 'settings:currency:tenant:view'],
    icon: '💱',
  },
]

const HRM_CHILDREN: NavItem[] = [
  {
    labelKey: 'nav.attendance',
    path: '/hrm/attendance',
    permission: 'hrm:attendance:view',
    icon: '🕒',
  },
  {
    labelKey: 'nav.leaveRequests',
    path: '/hrm/leave-requests',
    permission: 'hrm:leave:manage',
    icon: '🏖️',
  },
  {
    labelKey: 'nav.payroll',
    path: '/hrm/payroll',
    permission: 'hrm:payroll:manage',
    icon: '💵',
  },
  {
    labelKey: 'nav.workShifts',
    path: '/hrm/work-shifts',
    permission: 'hrm:shifts:view',
    icon: '⏰',
  },
]

const ACCOUNTING_CHILDREN: NavItem[] = [
  {
    labelKey: 'nav.accountingAccounts',
    path: '/accounting/accounts',
    permission: 'accounting:accounts:view',
    icon: '📒',
  },
  {
    labelKey: 'nav.accountingJournalEntries',
    path: '/accounting/journal-entries',
    permission: 'accounting:journals:view',
    icon: '📓',
  },
  {
    labelKey: 'nav.accountingInvoices',
    path: '/accounting/invoices',
    permission: 'accounting:invoices:view',
    icon: '🧾',
  },
  {
    labelKey: 'nav.accountingBills',
    path: '/accounting/bills',
    permission: 'accounting:bills:view',
    icon: '💵',
  },
  {
    labelKey: 'nav.accountingFiscalPeriods',
    path: '/accounting/fiscal-periods',
    permission: 'accounting:periods:view',
    icon: '🗓️',
  },
  {
    labelKey: 'nav.accountingTaxRates',
    path: '/accounting/tax-rates',
    permission: 'accounting:tax:view',
    icon: '🧮',
  },
  {
    labelKey: 'nav.accountingBankAccounts',
    path: '/accounting/bank-accounts',
    permission: 'accounting:bank:view',
    icon: '🏦',
  },
  {
    labelKey: 'nav.accountingReports',
    path: '/accounting/reports',
    permission: 'accounting:reports:view',
    icon: '📊',
  },
]

const MASTERDATA_CHILDREN: NavItem[] = [
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
  {
    labelKey: 'nav.companies',
    path: '/masterdata/companies',
    permission: 'masterdata:companies:view',
    icon: '🏛️',
  },
  {
    labelKey: 'nav.branches',
    path: '/masterdata/branches',
    permission: 'masterdata:branches:view',
    icon: '🏬',
  },
  {
    labelKey: 'nav.divisions',
    path: '/masterdata/divisions',
    permission: 'masterdata:divisions:view',
    icon: '🧭',
  },
  {
    labelKey: 'nav.teams',
    path: '/masterdata/teams',
    permission: 'masterdata:teams:view',
    icon: '🧑‍🤝‍🧑',
  },
  {
    labelKey: 'nav.businessUnits',
    path: '/masterdata/business-units',
    permission: 'masterdata:business-units:view',
    icon: '🏭',
  },
  {
    labelKey: 'nav.costCenters',
    path: '/masterdata/cost-centers',
    permission: 'masterdata:cost-centers:view',
    icon: '💰',
  },
  {
    labelKey: 'nav.profitCenters',
    path: '/masterdata/profit-centers',
    permission: 'masterdata:profit-centers:view',
    icon: '📈',
  },
  {
    labelKey: 'nav.organizationCalendars',
    path: '/masterdata/organization-calendars',
    permission: 'masterdata:organization-calendars:view',
    icon: '📅',
  },
  {
    labelKey: 'nav.employeeTeamAssignments',
    path: '/masterdata/employee-team-assignments',
    permission: 'masterdata:employee-team-assignments:view',
    icon: '🔗',
  },
  {
    labelKey: 'nav.currencies',
    path: '/masterdata/currencies',
    permission: 'masterdata:currencies:view',
    icon: '💲',
  },
  {
    labelKey: 'nav.paymentTerms',
    path: '/masterdata/payment-terms',
    permission: 'masterdata:payment-terms:view',
    icon: '🗒️',
  },
]

const PRODUCTS_CHILDREN: NavItem[] = [
  { labelKey: 'nav.products', path: '/products', permission: 'products:view', icon: '🏷️' },
  { labelKey: 'nav.brands', path: '/products/brands', permission: 'product:brands:view', icon: '🏷️' },
  { labelKey: 'nav.categories', path: '/products/categories', permission: 'products:view', icon: '📁' },
  { labelKey: 'nav.priceLists', path: '/products/pricing', permission: 'products:view', icon: '💲' },
]

const PURCHASING_CHILDREN: NavItem[] = [
  {
    labelKey: 'nav.purchaseOrders',
    path: '/orders/purchase',
    permission: 'purchase_order:read',
    icon: '🧾',
  },
  {
    labelKey: 'nav.suppliers',
    path: '/purchasing/suppliers',
    permission: 'supplier:read',
    icon: '🏭',
  },
]

const NAV_ITEMS: NavItem[] = [
  { labelKey: 'nav.dashboard', path: '/dashboard', permission: null, icon: '⊞' },
  { labelKey: 'nav.salesOrders', path: '/orders', permission: 'orders:view', icon: '📦' },
  { labelKey: 'nav.purchasing', path: '/purchasing', icon: '🧾', children: PURCHASING_CHILDREN },
  { labelKey: 'nav.productsGroup', path: '/products', icon: '🏷️', children: PRODUCTS_CHILDREN },
  { labelKey: 'nav.inventory', path: '/inventory', permission: 'inventory:view', icon: '🏭' },
  {
    labelKey: 'nav.customers',
    path: '/customers',
    permissions: [
      'crm:contacts:view',
      'crm:leads:view',
      'crm:opportunities:view',
      'crm:cases:view',
      'crm:activities:view',
    ],
    icon: '👥',
  },
  { labelKey: 'nav.payments', path: '/payments', permission: 'payments:view', icon: '💳' },
  { labelKey: 'nav.hrm', path: '/hrm', icon: '🧑‍💼', children: HRM_CHILDREN },
  { labelKey: 'nav.accounting', path: '/accounting', icon: '💼', children: ACCOUNTING_CHILDREN },
  { labelKey: 'nav.masterData', path: '/masterdata', icon: '🗂️', children: MASTERDATA_CHILDREN },
  { labelKey: 'nav.reports', path: '/reports', permission: 'reports:view', icon: '📊' },
  { labelKey: 'nav.settings', path: '/settings', icon: '⚙️', children: SETTINGS_CHILDREN },
  { labelKey: 'nav.monitoring', path: '/monitoring', permission: 'monitoring:view', icon: '🖥' },
  { labelKey: 'nav.logs', path: '/logs', permission: 'logging:logs:view', icon: '📜' },
  { labelKey: 'nav.users', path: '/identity/users', permission: 'identity:users:view', icon: '🧑‍💼' },
  {
    labelKey: 'nav.rolesPermissions',
    path: '/identity/roles',
    permission: 'identity:roles:view',
    icon: '🛡️',
  },
]

function isItemVisible(item: NavItem, hasPermission: (p: string) => boolean, hasAnyPermission: (...p: string[]) => boolean) {
  if (item.permissions) return hasAnyPermission(...item.permissions)
  return item.permission === null || item.permission === undefined || hasPermission(item.permission)
}

export function Sidebar() {
  const { t } = useTranslation()
  const { sidebarCollapsed } = useUiStore()
  const { hasPermission, hasAnyPermission } = usePermission()
  const { pathname } = useLocation()
  const [expandedGroups, setExpandedGroups] = useState<Set<string>>(new Set())

  // Recompute only when user permissions change (hasPermission is stable via useCallback).
  // Groups (e.g. Settings) are visible when at least one child passes the permission check;
  // the children themselves are pre-filtered here too so rendering doesn't repeat the check.
  const visible = useMemo(
    () =>
      NAV_ITEMS.map((item) => {
        if (!item.children) return item
        const visibleChildren = item.children.filter((child) => isItemVisible(child, hasPermission, hasAnyPermission))
        return { ...item, children: visibleChildren }
      }).filter((item) => {
        if (item.children) return item.children.length > 0
        return isItemVisible(item, hasPermission, hasAnyPermission)
      }),
    [hasPermission, hasAnyPermission],
  )

  const isGroupActive = (item: NavItem) => item.children?.some((child) => pathname.startsWith(child.path)) ?? false

  const toggleGroup = (labelKey: string) => {
    setExpandedGroups((prev) => {
      const next = new Set(prev)
      if (next.has(labelKey)) next.delete(labelKey)
      else next.add(labelKey)
      return next
    })
  }

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
        {visible.map((item) => {
          if (!item.children) {
            return (
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
            )
          }

          const active = isGroupActive(item)
          const expanded = expandedGroups.has(item.labelKey) || active

          return (
            <div key={item.labelKey}>
              <button
                type="button"
                onClick={() => toggleGroup(item.labelKey)}
                className={`flex w-full items-center gap-3 px-4 py-2.5 text-sm font-medium transition-colors hover:bg-gray-50 ${
                  active ? 'text-primary-700' : 'text-gray-600'
                }`}
              >
                <span className="text-base">{item.icon}</span>
                {!sidebarCollapsed && (
                  <>
                    <span className="flex-1 text-left">{t(item.labelKey)}</span>
                    <span className="text-xs">{expanded ? '▾' : '▸'}</span>
                  </>
                )}
              </button>
              {!sidebarCollapsed && expanded && (
                <div>
                  {item.children.map((child) => (
                    <NavLink
                      key={child.path}
                      to={child.path}
                      className={({ isActive }) =>
                        `flex items-center gap-3 py-2 pl-11 pr-4 text-sm font-medium transition-colors hover:bg-gray-50 ${
                          isActive
                            ? 'bg-primary-50 text-primary-700 border-r-2 border-primary-600'
                            : 'text-gray-600'
                        }`
                      }
                    >
                      <span className="text-sm">{child.icon}</span>
                      <span>{t(child.labelKey)}</span>
                    </NavLink>
                  ))}
                </div>
              )}
            </div>
          )
        })}
      </nav>
    </aside>
  )
}
