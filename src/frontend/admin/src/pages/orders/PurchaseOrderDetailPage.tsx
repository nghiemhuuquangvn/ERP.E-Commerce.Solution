import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { purchaseOrdersApi } from '@/api/purchaseOrders.api'
import { procurementApi } from '@/api/purchasing/procurement.api'
import { usePermission } from '@/hooks/usePermission'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { Alert, Button, Input, Modal, Spinner, useToast } from '@/components/ui'
import { serverMessage } from '@/lib/serverMessage'
import { format } from 'date-fns'

type ReceiveLine = { id: string; productName: string; remainingQuantity: number }

function ReceiveGoodsModal({ poId, lines, onClose }: { poId: string; lines: ReceiveLine[]; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const toast = useToast()
  const [accepted, setAccepted] = useState<Record<string, string>>({})
  const [rejected, setRejected] = useState<Record<string, string>>({})
  const [confirming, setConfirming] = useState(false)

  const num = (v: string | undefined) => Number(v) || 0
  const totalAccepted = lines.reduce((s, l) => s + num(accepted[l.id]), 0)
  const totalRejected = lines.reduce((s, l) => s + num(rejected[l.id]), 0)
  // accepted + rejected of a line may not pass what is still open on the order
  const exceeding = new Set(lines.filter((l) => num(accepted[l.id]) + num(rejected[l.id]) > l.remainingQuantity).map((l) => l.id))

  const receive = useMutation({
    mutationFn: () => {
      const body: Record<string, { quantityAccepted: number; quantityRejected: number }> = {}
      for (const l of lines) {
        const a = num(accepted[l.id])
        const r = num(rejected[l.id])
        if (a + r > 0) body[l.id] = { quantityAccepted: a, quantityRejected: r }
      }
      return procurementApi.receiveGoods(poId, body)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['purchaseOrders', poId] })
      queryClient.invalidateQueries({ queryKey: ['po', 'procurement', poId] })
      toast.success(t('common.saved'))
      onClose()
    },
    onError: () => setConfirming(false),
  })

  return (
    <>
      <Modal
        open
        size="lg"
        title={t('procurement.receive.title')}
        onClose={onClose}
        closeOnEsc={!receive.isPending && !confirming}
        footer={
          <>
            <Button variant="outline" onClick={onClose}>{t('common.cancel')}</Button>
            <Button disabled={totalAccepted + totalRejected === 0 || exceeding.size > 0} onClick={() => setConfirming(true)}>
              {t('procurement.receive.confirm')}
            </Button>
          </>
        }
      >
        <div className="space-y-3">
          <table className="min-w-full text-sm">
            <thead>
              <tr className="border-b border-line text-left text-xs uppercase text-ink-secondary">
                <th className="pb-2">{t('orders.product')}</th>
                <th className="pb-2 text-right">{t('procurement.receive.remaining')}</th>
                <th className="pb-2 text-right">{t('procurement.receive.accepted')}</th>
                <th className="pb-2 text-right">{t('procurement.receive.rejected')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {lines.map((l) => (
                <tr key={l.id}>
                  <td className="py-2">{l.productName}</td>
                  <td className="py-2 text-right tabular-nums">{l.remainingQuantity}</td>
                  <td className="py-2 text-right">
                    <Input
                      type="number"
                      min={0}
                      max={l.remainingQuantity}
                      aria-label={`${t('procurement.receive.accepted')}: ${l.productName}`}
                      error={exceeding.has(l.id)}
                      disabled={l.remainingQuantity === 0}
                      className="ml-auto w-24 text-right"
                      value={accepted[l.id] ?? ''}
                      onChange={(e) => setAccepted({ ...accepted, [l.id]: e.target.value })}
                    />
                  </td>
                  <td className="py-2 text-right">
                    <Input
                      type="number"
                      min={0}
                      max={l.remainingQuantity}
                      aria-label={`${t('procurement.receive.rejected')}: ${l.productName}`}
                      error={exceeding.has(l.id)}
                      disabled={l.remainingQuantity === 0}
                      className="ml-auto w-24 text-right"
                      value={rejected[l.id] ?? ''}
                      onChange={(e) => setRejected({ ...rejected, [l.id]: e.target.value })}
                    />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <p className="text-right text-sm text-ink-secondary" aria-live="polite">
            {t('procurement.receive.totals', { accepted: totalAccepted, rejected: totalRejected })}
          </p>
          {exceeding.size > 0 && <Alert variant="warning">{t('procurement.receive.exceeds')}</Alert>}
          {receive.isError && <Alert variant="danger">{serverMessage(receive.error, t('common.error'))}</Alert>}
        </div>
      </Modal>
      <ConfirmDialog
        open={confirming}
        title={t('procurement.receive.confirmTitle')}
        message={t('procurement.receive.confirmMessage', { accepted: totalAccepted, rejected: totalRejected })}
        confirmLabel={t('procurement.receive.confirmAction')}
        onConfirm={() => receive.mutate()}
        onCancel={() => setConfirming(false)}
      />
    </>
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

  if (isLoading) return <Spinner label={t('common.loading')} />
  if (!po) return <div className="text-ink-secondary">{t('purchaseOrders.notFound')}</div>

  const fmt = (n: number) =>
    new Intl.NumberFormat('en-US', { style: 'currency', currency: po.currency || 'USD' }).format(n)

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/orders/purchase" className="text-sm text-ink-secondary hover:text-ink">
          {t('purchaseOrders.backToPurchaseOrders')}
        </Link>
        <h1 className="text-xl font-semibold">{po.poNumber}</h1>
        <StatusBadge status={po.status} />
        <div className="ml-auto flex gap-2">
          {hasPermission('purchase_order:receive') && /approved|ordered|partially/i.test(String(po.status).replace(/_/g, '')) && (
            <Button onClick={() => setReceiving(true)}>{t('procurement.receive.button')}</Button>
          )}
          {hasPermission('supplier_invoice:read') && (
            <Link className="inline-flex h-9 items-center rounded-md border border-line-strong bg-surface px-4 text-sm font-medium text-ink hover:bg-surface-muted" to={`/purchasing/supplier-invoices?po=${po.id}`}>
              {t('procurement.links.invoices')}
            </Link>
          )}
          {hasPermission('vendor_return:read') && (
            <Link className="inline-flex h-9 items-center rounded-md border border-line-strong bg-surface px-4 text-sm font-medium text-ink hover:bg-surface-muted" to={`/purchasing/vendor-returns?po=${po.id}`}>
              {t('procurement.links.returns')}
            </Link>
          )}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-6">
        <div className="card p-5">
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-ink-secondary">{t('orders.orderInfo')}</h2>
          <dl className="space-y-2 text-sm">
            <div className="flex justify-between">
              <dt className="text-ink-secondary">{t('purchaseOrders.supplier')}</dt>
              <dd className="font-medium">{po.supplier?.name ?? po.supplierId}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-ink-secondary">{t('common.date')}</dt>
              <dd>{format(new Date(po.orderDate), 'dd MMM yyyy HH:mm')}</dd>
            </div>
            {po.expectedDeliveryDate && (
              <div className="flex justify-between">
                <dt className="text-ink-secondary">{t('purchaseOrders.expectedDelivery')}</dt>
                <dd>{format(new Date(po.expectedDeliveryDate), 'dd MMM yyyy')}</dd>
              </div>
            )}
            <div className="flex justify-between">
              <dt className="text-ink-secondary">{t('orders.total')}</dt>
              <dd className="font-semibold text-ink">{fmt(po.totalAmount)}</dd>
            </div>
            {po.notes && (
              <div className="flex justify-between gap-4">
                <dt className="text-ink-secondary">{t('purchaseOrders.notes')}</dt>
                <dd className="text-right">{po.notes}</dd>
              </div>
            )}
          </dl>
        </div>
      </div>

      <div className="card p-5">
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-ink-secondary">{t('orders.lineItems')}</h2>
        <table className="min-w-full text-sm">
          <thead>
            <tr className="border-b border-line text-left text-xs uppercase text-ink-secondary">
              <th className="pb-2">{t('orders.product')}</th>
              <th className="pb-2 text-right">{t('orders.qty')}</th>
              <th className="pb-2 text-right">{t('purchaseOrders.received')}</th>
              <th className="pb-2 text-right">{t('orders.unitPrice')}</th>
              <th className="pb-2 text-right">{t('orders.total')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
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
            <tr className="border-t border-line-strong">
              <td colSpan={4} className="pt-3 text-right font-semibold">{t('orders.total')}</td>
              <td className="pt-3 text-right font-semibold text-ink">{fmt(po.totalAmount)}</td>
            </tr>
          </tfoot>
        </table>
      </div>
      {receiving && <ReceiveGoodsModal poId={po.id} lines={po.items} onClose={() => setReceiving(false)} />}
    </div>
  )
}
