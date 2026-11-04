import { Package } from 'lucide-react-native'
import type { ErpModule } from '@/modules/types'
import { salesApi } from '@/modules/sales/api'

export const salesModule: ErpModule = {
  id: 'sales',
  titleKey: 'modules.sales',
  icon: Package,
  route: 'sales',
  permissions: ['sales_order:read'],
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
