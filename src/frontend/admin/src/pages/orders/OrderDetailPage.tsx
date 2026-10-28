import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useParams, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import axios from 'axios'
import { ordersApi } from '@/api/orders.api'
import { shippingApi } from '@/api/shipping.api'
import { OrderSagaPanel } from '@/components/orders/OrderSagaPanel'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { usePermission } from '@/hooks/usePermission'
import { format } from 'date-fns'

export function OrderDetailPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const { hasPermission } = usePermission()
  const queryClient = useQueryClient()
  const [actionError, setActionError] = useState<string | null>(null)
  const [trackingNumber, setTrackingNumber] = useState('')
  const [carrier, setCarrier] = useState('')
  const [cancelReason, setCancelReason] = useState('')
  const [rejectReason, setRejectReason] = useState('')

  const { data: order, isLoading } = useQuery({
    queryKey: ['orders', id],
    queryFn: () => ordersApi.get(id!),
    enabled: Boolean(id),
  })

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['orders', id] })
    queryClient.invalidateQueries({ queryKey: ['order-returns', id] })
  }

  // The reverse parcels booked for a returned order (needs shipping:view; without it the panel just shows the order's own fields).
  const { data: returnParcels } = useQuery({
    queryKey: ['order-returns', id],
    queryFn: () => shippingApi.orderReturns(id!),
    enabled: Boolean(id) && hasPermission('shipping:view'),
    retry: false,
  })

  const runAction = (fn: () => Promise<unknown>) => {
    setActionError(null)
    fn().then(invalidate).catch((err) => {
      const message = axios.isAxiosError(err) ? (err.response?.data as { message?: string })?.message : undefined
      setActionError(message ?? t('common.failedToSave'))
    })
  }

  const submitMutation = useMutation({ mutationFn: () => ordersApi.submit(id!) })
  const approveMutation = useMutation({ mutationFn: () => ordersApi.approve(id!) })
  const pickMutation = useMutation({ mutationFn: () => ordersApi.pick(id!) })
  const shipMutation = useMutation({ mutationFn: () => ordersApi.ship(id!, trackingNumber, carrier) })
  const markDeliveredMutation = useMutation({ mutationFn: () => ordersApi.markDelivered(id!) })
  const cancelMutation = useMutation({ mutationFn: () => ordersApi.cancel(id!, cancelReason) })
  const rejectMutation = useMutation({ mutationFn: () => ordersApi.reject(id!, rejectReason) })
  const requestReturnMutation = useMutation({ mutationFn: () => ordersApi.requestReturn(id!, 'Return requested by admin') })
  // The goods are what a return gives back by default; the delivery fee only when the approver ticks it (ROADMAP P1-11c).
  const goodsTotal = order ? order.totalAmount - (order.shippingFee ?? 0) : 0
  const [refundAmount, setRefundAmount] = useState<string>('')
  const [refundFee, setRefundFee] = useState(false)
  const approveReturnMutation = useMutation({ mutationFn: () => ordersApi.approveReturn(id!) })
  const completeReturnMutation = useMutation({
    mutationFn: () => ordersApi.completeReturn(id!, refundAmount.trim() === '' ? goodsTotal + (refundFee ? order?.shippingFee ?? 0 : 0) : Number(refundAmount), refundFee),
  })

  if (isLoading) return <div className="text-gray-400">{t('common.loading')}</div>
  if (!order) return <div className="text-gray-400">{t('orders.orderNotFound')}</div>

  const fmt = (n: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n)
  const canCancel = !['Cancelled', 'Delivered', 'Shipped', 'PartiallyShipped', 'Returned'].includes(order.status)

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/orders" className="text-sm text-gray-500 hover:text-gray-700">
          {t('orders.backToOrders')}
        </Link>
        <h1 className="text-xl font-semibold">{order.orderNo || order.orderId}</h1>
        <StatusBadge status={order.status} />
        {order.hasPriceChangeWarning && (
          <span className="text-xs text-orange-600">{t('orders.priceChangeWarning')}</span>
        )}
        {order.isAnomalous && (
          <span
            className="inline-flex items-center rounded-full bg-red-100 px-2 py-0.5 text-xs font-medium text-red-700"
            title={order.anomalyReason ?? undefined}
          >
            ⚠ {order.anomalyReason ?? t('orders.anomalyFlag')}
          </span>
        )}
      </div>

      {actionError && <p className="text-sm text-red-600">{actionError}</p>}

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
              <dt className="text-gray-500">{t('orders.orderSource')}</dt>
              <dd>{order.orderSource}</dd>
            </div>
            {order.salesRepId && (
              <div className="flex justify-between">
                <dt className="text-gray-500">{t('orders.salesRep')}</dt>
                <dd className="font-mono text-xs">{order.salesRepId}</dd>
              </div>
            )}
            {order.departmentId && (
              <div className="flex justify-between">
                <dt className="text-gray-500">{t('orders.department')}</dt>
                <dd className="font-mono text-xs">{order.departmentId}</dd>
              </div>
            )}
            {order.quotationId && (
              <div className="flex justify-between">
                <dt className="text-gray-500">{t('orders.fromQuotation')}</dt>
                <dd>
                  <Link to={`/sales/quotations/${order.quotationId}`} className="text-primary-600 hover:underline">
                    {order.quotationId.slice(0, 8)}
                  </Link>
                </dd>
              </div>
            )}
            {order.discountAmount > 0 && (
              <div className="flex justify-between">
                <dt className="text-gray-500">{t('orders.discount')}</dt>
                <dd className="text-green-700">-{fmt(order.discountAmount)}</dd>
              </div>
            )}
            {order.shippingFee != null && (
              <div className="flex justify-between">
                <dt className="text-gray-500">{t('orders.shippingFee')}</dt>
                <dd>{order.shippingFee > 0 ? fmt(order.shippingFee) : t('orders.freeDelivery')}</dd>
              </div>
            )}
            <div className="flex justify-between">
              <dt className="text-gray-500">{t('orders.total')}</dt>
              <dd className="font-semibold text-gray-900">{fmt(order.totalAmount)}</dd>
            </div>
          </dl>
        </div>

        <div className="card p-5">
          <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">{t('orders.statusActions.title')}</h2>
          <div className="flex flex-wrap gap-2">
            {hasPermission('sales_order:update') && order.status === 'Draft' && (
              <button onClick={() => runAction(() => submitMutation.mutateAsync())} className="btn-outline text-sm">
                {t('orders.statusActions.submit')}
              </button>
            )}
            {hasPermission('sales_order:approve') && order.status === 'Submitted' && (
              <>
                <button onClick={() => runAction(() => approveMutation.mutateAsync())} className="btn-primary text-sm">
                  {t('orders.approve')}
                </button>
                <input
                  className="input w-48 text-sm"
                  placeholder={t('orders.statusActions.rejectReasonPlaceholder')}
                  value={rejectReason}
                  onChange={(e) => setRejectReason(e.target.value)}
                />
                <button
                  disabled={!rejectReason.trim()}
                  onClick={() => runAction(() => rejectMutation.mutateAsync())}
                  className="btn-outline text-sm text-red-600"
                >
                  {t('orders.statusActions.reject')}
                </button>
              </>
            )}
            {hasPermission('sales_order:update') && order.status === 'Approved' && (
              <button onClick={() => runAction(() => pickMutation.mutateAsync())} className="btn-outline text-sm">
                {t('orders.statusActions.pick')}
              </button>
            )}
            {hasPermission('sales_order:update') && order.status === 'Picked' && (
              <div className="flex flex-wrap items-center gap-2">
                <input
                  className="input w-40 text-sm"
                  placeholder={t('orders.statusActions.trackingNumber')}
                  value={trackingNumber}
                  onChange={(e) => setTrackingNumber(e.target.value)}
                />
                <input
                  className="input w-32 text-sm"
                  placeholder={t('orders.statusActions.carrier')}
                  value={carrier}
                  onChange={(e) => setCarrier(e.target.value)}
                />
                <button
                  disabled={!trackingNumber.trim() || !carrier.trim()}
                  onClick={() => runAction(() => shipMutation.mutateAsync())}
                  className="btn-primary text-sm"
                >
                  {t('orders.statusActions.ship')}
                </button>
              </div>
            )}
            {hasPermission('sales_order:update') && order.status === 'Shipped' && (
              <button onClick={() => runAction(() => markDeliveredMutation.mutateAsync())} className="btn-primary text-sm">
                {t('orders.statusActions.markDelivered')}
              </button>
            )}
            {hasPermission('sales_order:update') && order.status === 'Delivered' && (
              <button onClick={() => runAction(() => requestReturnMutation.mutateAsync())} className="btn-outline text-sm">
                {t('orders.statusActions.requestReturn')}
              </button>
            )}
            {hasPermission('sales_order:approve') && order.status === 'ReturnRequested' && (!order.returnApprovedAt || !order.returnPickupBooked) && (
              <button onClick={() => runAction(() => approveReturnMutation.mutateAsync())} className="btn-outline text-sm">
                {order.returnApprovedAt ? t('orders.return.retryPickup') : t('orders.return.approve')}
              </button>
            )}
            {order.status === 'ReturnRequested' && order.returnApprovedAt && (
              <div className="w-full rounded bg-gray-50 p-2 text-xs text-gray-700">
                <div>{order.returnPickupNote}</div>
                {(returnParcels ?? []).map((p) => (
                  <div key={p.id} className="mt-1 flex flex-wrap items-center gap-2">
                    <span className="font-mono">{p.trackingNumber ?? '—'}</span>
                    <span>{p.carrierName}</span>
                    <StatusBadge status={p.status} />
                    {p.bookingError && <span className="text-red-600">{p.bookingError}</span>}
                    {hasPermission('shipping:update') && p.status !== 'Received' && p.status !== 'Cancelled' && (
                      <button className="btn-outline px-2 py-0.5 text-xs" onClick={() => runAction(() => shippingApi.receiveReturn(p.originalShipmentId, p.id))}>
                        {t('orders.return.receiveByHand')}
                      </button>
                    )}
                  </div>
                ))}
                {order.returnPickupBooked && !order.returnGoodsReceivedAt && <div className="mt-1 text-amber-700">{t('orders.return.waitingForGoods')}</div>}
              </div>
            )}
            {hasPermission('sales_order:approve') && order.status === 'ReturnRequested' && (
              <div className="flex flex-wrap items-center gap-2">
                <input
                  type="number"
                  className="input w-40 text-sm"
                  title={t('orders.return.amount')}
                  placeholder={t('orders.return.amountPlaceholder', { goods: fmt(goodsTotal + (refundFee ? order.shippingFee ?? 0 : 0)) })}
                  value={refundAmount}
                  onChange={(e) => setRefundAmount(e.target.value)}
                />
                {(order.shippingFee ?? 0) > 0 && (
                  <label className="flex items-center gap-1 text-sm text-gray-700">
                    <input type="checkbox" checked={refundFee} onChange={(e) => setRefundFee(e.target.checked)} />
                    {t('orders.return.refundFee', { fee: fmt(order.shippingFee ?? 0) })}
                  </label>
                )}
                <button
                  disabled={Boolean(order.returnPickupBooked && !order.returnGoodsReceivedAt)}
                  title={order.returnPickupBooked && !order.returnGoodsReceivedAt ? t('orders.return.waitingForGoods') : undefined}
                  onClick={() => runAction(() => completeReturnMutation.mutateAsync())}
                  className="btn-primary text-sm disabled:opacity-50"
                >
                  {t('orders.statusActions.completeReturn')}
                </button>
              </div>
            )}
            {hasPermission('sales_order:cancel') && canCancel && (
              <div className="flex flex-wrap items-center gap-2">
                <input
                  className="input w-48 text-sm"
                  placeholder={t('orders.statusActions.cancelReasonPlaceholder')}
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                />
                <button
                  disabled={!cancelReason.trim()}
                  onClick={() => runAction(() => cancelMutation.mutateAsync())}
                  className="btn-outline text-sm text-red-600"
                >
                  {t('orders.cancel')}
                </button>
              </div>
            )}
          </div>
        </div>
      </div>

      <OrderSagaPanel orderId={order.orderId} />

      <div className="card p-5">
        <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">{t('orders.lineItems')}</h2>
        <table className="min-w-full text-sm">
          <thead>
            <tr className="border-b border-gray-100 text-left text-xs text-gray-400 uppercase">
              <th className="pb-2">{t('orders.product')}</th>
              <th className="pb-2 text-right">{t('orders.qty')}</th>
              <th className="pb-2 text-right">{t('orders.shipped')}</th>
              <th className="pb-2 text-right">{t('orders.unitPrice')}</th>
              <th className="pb-2 text-right">{t('orders.total')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {order.lineItems.map((line) => (
              <tr key={line.productId}>
                <td className="py-2">{line.productName}</td>
                <td className="py-2 text-right">{line.quantity}</td>
                <td className="py-2 text-right">{line.shippedQuantity}</td>
                <td className="py-2 text-right">{fmt(line.unitPrice)}</td>
                <td className="py-2 text-right font-medium">{fmt(line.quantity * line.unitPrice)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t border-gray-200">
              <td colSpan={4} className="pt-3 text-right font-semibold">{t('orders.total')}</td>
              <td className="pt-3 text-right font-semibold text-gray-900">{fmt(order.totalAmount)}</td>
            </tr>
          </tfoot>
        </table>
      </div>

      {order.statusHistory.length > 0 && (
        <div className="card p-5">
          <h2 className="mb-3 text-sm font-semibold text-gray-500 uppercase tracking-wide">{t('orders.statusHistory')}</h2>
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-gray-100 text-left text-xs text-gray-400 uppercase">
                <th className="pb-2">{t('common.date')}</th>
                <th className="pb-2">{t('orders.statusHistoryFrom')}</th>
                <th className="pb-2">{t('orders.statusHistoryTo')}</th>
                <th className="pb-2">{t('orders.statusHistoryReason')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-50">
              {order.statusHistory.map((h, idx) => (
                <tr key={idx}>
                  <td className="py-2">{format(new Date(h.changedAt), 'dd MMM yyyy HH:mm')}</td>
                  <td className="py-2"><StatusBadge status={h.fromStatus} /></td>
                  <td className="py-2"><StatusBadge status={h.toStatus} /></td>
                  <td className="py-2 text-gray-500">{h.reason ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
