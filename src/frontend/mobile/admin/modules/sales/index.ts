import { BadgePercent, FilePen, Gauge, Package, ClipboardList } from 'lucide-react-native'
import type { ErpModule } from '@/modules/types'
import { salesApi } from '@/modules/sales/api'

export const salesModule: ErpModule = {
  id: 'sales',
  titleKey: 'modules.sales',
  icon: Package,
  route: 'sales',
  permissions: ['sales_order:read'],
  sections: [
    { id: 'sales-orders', titleKey: 'sections.salesOrders', icon: ClipboardList, route: 'sales', permissions: ['sales_order:read'], order: 10 },
    { id: 'sales-quotations', titleKey: 'sections.quotations', icon: FilePen, route: 'sales-quotations', permissions: ['sales_order:read'], order: 20 },
    { id: 'sales-credit-limits', titleKey: 'sections.creditLimits', icon: Gauge, route: 'sales-credit-limits', permissions: ['sales_order:read'], order: 25 },
    { id: 'sales-commissions', titleKey: 'sections.commissions', icon: BadgePercent, route: 'sales-commissions', permissions: ['sales_order:read'], order: 30 },
  ],
  licensedModule: 'SO',
  order: 30,
  attention: [
    {
      key: 'so-to-approve',
      labelKey: 'home.soToApprove',
      permission: 'sales_order:approve',
      count: () => salesApi.count('Submitted'),
      href: { pathname: '/sales', params: { status: 'Submitted' } },
    },
  ],
}
