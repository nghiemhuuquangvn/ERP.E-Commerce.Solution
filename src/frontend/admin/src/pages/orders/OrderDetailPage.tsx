import { useQuery } from '@tanstack/react-query'
import { useParams, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ordersApi } from '@/api/orders.api'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { format } from 'date-fns'

export function OrderDetailPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const { data: order, isLoading } = useQuery({
    queryKey: ['orders', id],
    queryFn: () => ordersApi.get(id!),
    enabled: Boolean(id),
  })

  if (isLoading) return <div className="text-gray-400">{t('common.loading')}</div>
  if (!order) return <div className="text-gray-400">{t('orders.orderNotFound')}</div>

  const fmt = (n: number) =>
    new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/orders" className="text-sm text-gray-500 hover:text-gray-700">
          {t('orders.backToOrders')}
        </Link>
        <h1 className="text-xl font-semibold">Order {order.orderNumber}</h1>
        <StatusBadge status={order.status} />
      </div>

      <div className="grid grid-cols-2 gap-6">
        <div className="card p-5">
          <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">{t('orders.orderInfo')}</h2>
          <dl className="space-y-2 text-sm">
            <div className="flex justify-between">
              <dt className="text-gray-500">{t('orders.customer')}</dt>
              <dd className="font-medium">{order.customerName}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-gray-500">{t('common.date')}</dt>
              <dd>{format(new Date(order.orderDate), 'dd MMM yyyy HH:mm')}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-gray-500">{t('orders.total')}</dt>
              <dd className="font-semibold text-gray-900">{fmt(order.totalAmount)}</dd>
            </div>
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
              <th className="pb-2 text-right">{t('orders.unitPrice')}</th>
              <th className="pb-2 text-right">{t('orders.total')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {order.lines.map((line) => (
              <tr key={line.id}>
                <td className="py-2">{line.productName}</td>
                <td className="py-2 text-right">{line.quantity}</td>
                <td className="py-2 text-right">{fmt(line.unitPrice)}</td>
                <td className="py-2 text-right font-medium">{fmt(line.lineTotal)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t border-gray-200">
              <td colSpan={3} className="pt-3 text-right font-semibold">{t('orders.total')}</td>
              <td className="pt-3 text-right font-semibold text-gray-900">{fmt(order.totalAmount)}</td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  )
}
