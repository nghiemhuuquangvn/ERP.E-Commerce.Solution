import { Building2, FileText, PackageX, ReceiptText, ShoppingCart, ClipboardList } from 'lucide-react-native'
import type { ErpModule } from '@/modules/types'
import { purchaseApi } from '@/modules/purchase/api'

export const purchaseModule: ErpModule = {
  id: 'purchase',
  titleKey: 'modules.purchase',
  icon: ShoppingCart,
  route: 'purchase',
  permissions: ['purchase_order:read', 'supplier:read', 'supplier_invoice:read', 'vendor_return:read', 'rfq:read'],
  sections: [
    { id: 'purchase-orders', titleKey: 'sections.purchaseOrders', icon: ClipboardList, route: 'purchase', permissions: ['purchase_order:read'], order: 10 },
    { id: 'purchase-suppliers', titleKey: 'sections.suppliers', icon: Building2, route: 'purchase-suppliers', permissions: ['supplier:read'], order: 20 },
    { id: 'purchase-invoices', titleKey: 'sections.supplierInvoices', icon: ReceiptText, route: 'purchase-invoices', permissions: ['supplier_invoice:read'], order: 30 },
    { id: 'purchase-returns', titleKey: 'sections.vendorReturns', icon: PackageX, route: 'purchase-returns', permissions: ['vendor_return:read'], order: 40 },
    { id: 'purchase-rfqs', titleKey: 'sections.rfqs', icon: FileText, route: 'purchase-rfqs', permissions: ['rfq:read'], order: 50 },
  ],
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
