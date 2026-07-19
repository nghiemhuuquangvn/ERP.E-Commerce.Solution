import { useQuery } from '@tanstack/react-query'
import { useParams, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { purchaseOrdersApi } from '@/api/purchaseOrders.api'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { format } from 'date-fns'

export function PurchaseOrderDetailPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const { data: po, isLoading } = useQuery({
    queryKey: ['purchaseOrders', id],
    queryFn: () => purchaseOrdersApi.get(id!),
    enabled: Boolean(id),
  })

  if (isLoading) return <div className="text-gray-400">{t('common.loading')}</div>
  if (!po) return <div className="text-gray-400">{t('purchaseOrders.notFound')}</div>

  const fmt = (n: number) =>
    new Intl.NumberFormat('en-US', { style: 'currency', currency: po.currency || 'USD' }).format(n)

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/orders/purchase" className="text-sm text-gray-500 hover:text-gray-700">
          {t('purchaseOrders.backToPurchaseOrders')}
        </Link>
        <h1 className="text-xl font-semibold">{po.poNumber}</h1>
        <StatusBadge status={po.status} />
      </div>

      <div className="grid grid-cols-2 gap-6">
        <div className="card p-5">
          <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">{t('orders.orderInfo')}</h2>
          <dl className="space-y-2 text-sm">
            <div className="flex justify-between">
              <dt className="text-gray-500">{t('purchaseOrders.supplier')}</dt>
              <dd className="font-medium">{po.supplier?.name ?? po.supplierId}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-gray-500">{t('common.date')}</dt>
              <dd>{format(new Date(po.orderDate), 'dd MMM yyyy HH:mm')}</dd>
            </div>
            {po.expectedDeliveryDate && (
              <div className="flex justify-between">
                <dt className="text-gray-500">{t('purchaseOrders.expectedDelivery')}</dt>
                <dd>{format(new Date(po.expectedDeliveryDate), 'dd MMM yyyy')}</dd>
              </div>
            )}
            <div className="flex justify-between">
              <dt className="text-gray-500">{t('orders.total')}</dt>
              <dd className="font-semibold text-gray-900">{fmt(po.totalAmount)}</dd>
            </div>
            {po.notes && (
              <div className="flex justify-between gap-4">
                <dt className="text-gray-500">{t('purchaseOrders.notes')}</dt>
                <dd className="text-right">{po.notes}</dd>
              </div>
            )}
          </dl>
        </div>
      </div>

      <div className="card p-5">
        <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">{t('orders.lineItems')}</h2>
        <table className="min-w-full text-sm">
          <thead>
            <tr className="border-b border-gray-100 text-left text-xs text-gray-400 uppercase">
              <th className="pb-2">{t('orders.product')}</th>
              <th className="pb-2 text-right">{t('orders.qty')}</th>
              <th className="pb-2 text-right">{t('purchaseOrders.received')}</th>
              <th className="pb-2 text-right">{t('orders.unitPrice')}</th>
              <th className="pb-2 text-right">{t('orders.total')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {po.items.map((line) => (
              <tr key={line.id}>
                <td className="py-2">{line.productName}</td>
                <td className="py-2 text-right">{line.quantity} {line.unit}</td>
                <td className="py-2 text-right">{line.receivedQuantity} / {line.quantity}</td>
                <td className="py-2 text-right">{fmt(line.unitPrice)}</td>
                <td className="py-2 text-right font-medium">{fmt(line.totalPrice)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t border-gray-200">
              <td colSpan={4} className="pt-3 text-right font-semibold">{t('orders.total')}</td>
              <td className="pt-3 text-right font-semibold text-gray-900">{fmt(po.totalAmount)}</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  )
}
