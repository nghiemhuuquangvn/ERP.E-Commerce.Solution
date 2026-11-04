import { ShoppingCart } from 'lucide-react-native'
import type { ErpModule } from '@/modules/types'
import { purchaseApi } from '@/modules/purchase/api'

export const purchaseModule: ErpModule = {
  id: 'purchase',
  titleKey: 'modules.purchase',
  icon: ShoppingCart,
  route: 'purchase',
  permissions: ['purchase_order:read'],
  licensedModule: 'PO',
  order: 20,
  attention: [
    {
      key: 'po-to-approve',
      labelKey: 'home.poToApprove',
      permission: 'purchase_order:approve',
      count: () => purchaseApi.count('SUBMITTED'),
      href: { pathname: '/purchase', params: { status: 'SUBMITTED' } },
    },
  ],
}
