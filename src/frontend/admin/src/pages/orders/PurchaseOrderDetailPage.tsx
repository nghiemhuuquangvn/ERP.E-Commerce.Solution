import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { purchaseOrdersApi } from '@/api/purchaseOrders.api'
import { procurementApi, serverMessage } from '@/api/purchasing/procurement.api'
import { usePermission } from '@/hooks/usePermission'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { format } from 'date-fns'

function ReceiveGoodsModal({ poId, lines, onClose }: { poId: string; lines: { id: string; productName: string; remainingQuantity: number }[]; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [accepted, setAccepted] = useState<Record<string, string>>({})
  const [rejected, setRejected] = useState<Record<string, string>>({})
  const receive = useMutation({
    mutationFn: () => {
      const body: Record<string, { quantityAccepted: number; quantityRejected: number }> = {}
      for (const l of lines) {
        const a = Number(accepted[l.id]) || 0
        const r = Number(rejected[l.id]) || 0
        if (a + r > 0) body[l.id] = { quantityAccepted: a, quantityRejected: r }
      }
      return procurementApi.receiveGoods(poId, body)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['purchaseOrders', poId] })
      queryClient.invalidateQueries({ queryKey: ['po', 'procurement', poId] })
      onClose()
    },
  })
  const any = lines.some((l) => (Number(accepted[l.id]) || 0) + (Number(rejected[l.id]) || 0) > 0)
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40">
      <div className="card max-h-[90vh] w-full max-w-2xl overflow-y-auto p-6">
        <h3 className="mb-4 text-base font-semibold text-gray-900">{t('procurement.receive.title')}</h3>
        <table className="min-w-full text-sm">
          <thead><tr className="border-b border-gray-100 text-left text-xs uppercase text-gray-400"><th className="pb-2">{t('orders.product')}</th><th className="pb-2 text-right">{t('procurement.receive.remaining')}</th><th className="pb-2 text-right">{t('procurement.receive.accepted')}</th><th className="pb-2 text-right">{t('procurement.receive.rejected')}</th></tr></thead>
          <tbody className="divide-y divide-gray-50">
            {lines.map((l) => (
              <tr key={l.id}>
                <td className="py-2">{l.productName}</td>
                <td className="py-2 text-right">{l.remainingQuantity}</td>
                <td className="py-2 text-right"><input type="number" min={0} max={l.remainingQuantity} disabled={l.remainingQuantity === 0} className="input w-24 text-right" value={accepted[l.id] ?? ''} onChange={(e) => setAccepted({ ...accepted, [l.id]: e.target.value })} /></td>
                <td className="py-2 text-right"><input type="number" min={0} max={l.remainingQuantity} disabled={l.remainingQuantity === 0} className="input w-24 text-right" value={rejected[l.id] ?? ''} onChange={(e) => setRejected({ ...rejected, [l.id]: e.target.value })} /></td>
              </tr>
            ))}
          </tbody>
        </table>
        {receive.isError && <p className="mt-3 text-xs text-red-600">{serverMessage(receive.error, t('common.error'))}</p>}
        <div className="mt-4 flex justify-end gap-3">
          <button className="btn-outline" onClick={onClose}>{t('common.cancel')}</button>
          <button className="btn-primary" disabled={!any || receive.isPending} onClick={() => receive.mutate()}>{t('procurement.receive.confirm')}</button>
        </div>
      </div>
    </div>
  )
}

export function PurchaseOrderDetailPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const { hasPermission } = usePermission()
  const [receiving, setReceiving] = useState(false)
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
        <div className="ml-auto flex gap-2">
          {hasPermission('purchase_order:receive') && /approved|ordered|partially/i.test(String(po.status).replace(/_/g, '')) && (
            <button className="btn-primary" onClick={() => setReceiving(true)}>{t('procurement.receive.button')}</button>
          )}
          {hasPermission('supplier_invoice:read') && <Link className="btn-outline" to={`/purchasing/supplier-invoices?po=${po.id}`}>{t('procurement.links.invoices')}</Link>}
          {hasPermission('vendor_return:read') && <Link className="btn-outline" to={`/purchasing/vendor-returns?po=${po.id}`}>{t('procurement.links.returns')}</Link>}
        </div>
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
      {receiving && <ReceiveGoodsModal poId={po.id} lines={po.items} onClose={() => setReceiving(false)} />}
    </div>
  )
}
